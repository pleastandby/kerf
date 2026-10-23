//! Cubic & Quadratic Bézier curve mathematics and adaptive subdivision.

#[repr(C)]
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct Point2D {
    pub x: f64,
    pub y: f64,
}

impl Point2D {
    pub const fn new(x: f64, y: f64) -> Self {
        Self { x, y }
    }

    #[inline]
    pub fn distance_to(&self, other: &Point2D) -> f64 {
        let dx = self.x - other.x;
        let dy = self.y - other.y;
        (dx * dx + dy * dy).sqrt()
    }

    #[inline]
    pub fn distance_sq(&self, other: &Point2D) -> f64 {
        let dx = self.x - other.x;
        let dy = self.y - other.y;
        dx * dx + dy * dy
    }

    #[inline]
    pub fn lerp(&self, other: &Point2D, t: f64) -> Point2D {
        Point2D {
            x: self.x + (other.x - self.x) * t,
            y: self.y + (other.y - self.y) * t,
        }
    }
}

#[repr(C)]
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct Rect2D {
    pub x: f64,
    pub y: f64,
    pub width: f64,
    pub height: f64,
}

impl Rect2D {
    pub const fn new(x: f64, y: f64, width: f64, height: f64) -> Self {
        Self { x, y, width, height }
    }

    pub fn from_min_max(min_x: f64, min_y: f64, max_x: f64, max_y: f64) -> Self {
        Self {
            x: min_x,
            y: min_y,
            width: (max_x - min_x).max(0.0),
            height: (max_y - min_y).max(0.0),
        }
    }
}

/// Node data matching Kerf Studio's BezierNode
#[repr(C)]
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct BezierNodeC {
    pub anchor_x: f64,
    pub anchor_y: f64,
    pub in_x: f64,
    pub in_y: f64,
    pub out_x: f64,
    pub out_y: f64,
    pub node_type: i32, // 0 = Corner, 1 = Smooth, 2 = Symmetric
}

/// Evaluates a cubic Bézier curve at parameter t in [0.0, 1.0].
#[inline]
pub fn eval_cubic(p0: Point2D, p1: Point2D, p2: Point2D, p3: Point2D, t: f64) -> Point2D {
    let u = 1.0 - t;
    let tt = t * t;
    let uu = u * u;
    let uuu = uu * u;
    let ttt = tt * t;

    Point2D {
        x: uuu * p0.x + 3.0 * uu * t * p1.x + 3.0 * u * tt * p2.x + ttt * p3.x,
        y: uuu * p0.y + 3.0 * uu * t * p1.y + 3.0 * u * tt * p2.y + ttt * p3.y,
    }
}

/// Computes the exact analytical bounding box of a cubic Bézier curve segment.
/// Uses quadratic roots of B'(t) = 0 to find exact mathematical extrema.
pub fn cubic_exact_bounds(p0: Point2D, p1: Point2D, p2: Point2D, p3: Point2D) -> (f64, f64, f64, f64) {
    let mut min_x = p0.x.min(p3.x);
    let mut max_x = p0.x.max(p3.x);
    let mut min_y = p0.y.min(p3.y);
    let mut max_y = p0.y.max(p3.y);

    // Dimension X roots: 3*a*t^2 + 2*b*t + c = 0
    let ax = -p0.x + 3.0 * p1.x - 3.0 * p2.x + p3.x;
    let bx = 2.0 * (p0.x - 2.0 * p1.x + p2.x);
    let cx = -p0.x + p1.x;

    find_extrema_roots(ax, bx, cx, |t| {
        let px = eval_cubic_1d(p0.x, p1.x, p2.x, p3.x, t);
        if px < min_x { min_x = px; }
        if px > max_x { max_x = px; }
    });

    // Dimension Y roots
    let ay = -p0.y + 3.0 * p1.y - 3.0 * p2.y + p3.y;
    let by = 2.0 * (p0.y - 2.0 * p1.y + p2.y);
    let cy = -p0.y + p1.y;

    find_extrema_roots(ay, by, cy, |t| {
        let py = eval_cubic_1d(p0.y, p1.y, p2.y, p3.y, t);
        if py < min_y { min_y = py; }
        if py > max_y { max_y = py; }
    });

    (min_x, min_y, max_x, max_y)
}

#[inline]
fn eval_cubic_1d(p0: f64, p1: f64, p2: f64, p3: f64, t: f64) -> f64 {
    let u = 1.0 - t;
    u * u * u * p0 + 3.0 * u * u * t * p1 + 3.0 * u * t * t * p2 + t * t * t * p3
}

#[inline]
fn find_extrema_roots<F: FnMut(f64)>(a: f64, b: f64, c: f64, mut callback: F) {
    if a.abs() < 1e-9 {
        // Linear equation: b*t + c = 0
        if b.abs() > 1e-9 {
            let t = -c / b;
            if t > 0.0 && t < 1.0 {
                callback(t);
            }
        }
    } else {
        // Quadratic equation: a*t^2 + b*t + c = 0
        let disc = b * b - 4.0 * a * c;
        if disc >= 0.0 {
            let sqrt_d = disc.sqrt();
            let t1 = (-b + sqrt_d) / (2.0 * a);
            let t2 = (-b - sqrt_d) / (2.0 * a);
            if t1 > 0.0 && t1 < 1.0 {
                callback(t1);
            }
            if t2 > 0.0 && t2 < 1.0 {
                callback(t2);
            }
        }
    }
}

/// Adaptive subdivision of a cubic curve using de Casteljau recursion.
pub fn flatten_cubic(
    p0: Point2D,
    p1: Point2D,
    p2: Point2D,
    p3: Point2D,
    tolerance: f64,
    points: &mut Vec<Point2D>,
) {
    let tol_sq = (tolerance.max(0.1)) * (tolerance.max(0.1));
    flatten_cubic_recursive(p0, p1, p2, p3, tol_sq, 0, points);
    points.push(p3);
}

fn flatten_cubic_recursive(
    p0: Point2D,
    p1: Point2D,
    p2: Point2D,
    p3: Point2D,
    tol_sq: f64,
    depth: u32,
    points: &mut Vec<Point2D>,
) {
    if depth >= 16 {
        points.push(p0);
        return;
    }

    // Measure deviation from baseline p0->p3
    let d1 = point_line_distance_sq(p1, p0, p3);
    let d2 = point_line_distance_sq(p2, p0, p3);

    if (d1 < tol_sq && d2 < tol_sq) || depth >= 12 {
        points.push(p0);
        return;
    }

    // de Casteljau split at t = 0.5
    let p01 = p0.lerp(&p1, 0.5);
    let p12 = p1.lerp(&p2, 0.5);
    let p23 = p2.lerp(&p3, 0.5);

    let p012 = p01.lerp(&p12, 0.5);
    let p123 = p12.lerp(&p23, 0.5);

    let p0123 = p012.lerp(&p123, 0.5);

    flatten_cubic_recursive(p0, p01, p012, p0123, tol_sq, depth + 1, points);
    flatten_cubic_recursive(p0123, p123, p23, p3, tol_sq, depth + 1, points);
}

#[inline]
fn point_line_distance_sq(pt: Point2D, a: Point2D, b: Point2D) -> f64 {
    let dx = b.x - a.x;
    let dy = b.y - a.y;
    let len_sq = dx * dx + dy * dy;
    if len_sq < 1e-12 {
        return pt.distance_sq(&a);
    }
    let t = (((pt.x - a.x) * dx + (pt.y - a.y) * dy) / len_sq).clamp(0.0, 1.0);
    let proj = Point2D::new(a.x + t * dx, a.y + t * dy);
    pt.distance_sq(&proj)
}

/// Estimates arc length of cubic segment by 10-point Gaussian quadrature.
pub fn cubic_arc_length(p0: Point2D, p1: Point2D, p2: Point2D, p3: Point2D) -> f64 {
    // 5-point subdivision sum
    let mut total = 0.0;
    let steps = 16;
    let mut prev = p0;
    for i in 1..=steps {
        let t = i as f64 / steps as f64;
        let curr = eval_cubic(p0, p1, p2, p3, t);
        total += prev.distance_to(&curr);
        prev = curr;
    }
    total
}

/// Finds the minimum distance from query point `q` to cubic Bézier curve segment.
pub fn point_to_cubic_min_dist(q: Point2D, p0: Point2D, p1: Point2D, p2: Point2D, p3: Point2D) -> f64 {
    let mut min_d_sq = f64::MAX;
    let steps = 20;
    let mut best_t = 0.0;

    for i in 0..=steps {
        let t = i as f64 / steps as f64;
        let pt = eval_cubic(p0, p1, p2, p3, t);
        let d_sq = q.distance_sq(&pt);
        if d_sq < min_d_sq {
            min_d_sq = d_sq;
            best_t = t;
        }
    }

    // Local refinement around best_t
    let dt = 1.0 / steps as f64;
    let t_start = (best_t - dt).max(0.0);
    let t_end = (best_t + dt).min(1.0);
    let fine_steps = 10;
    for i in 0..=fine_steps {
        let t = t_start + (t_end - t_start) * (i as f64 / fine_steps as f64);
        let pt = eval_cubic(p0, p1, p2, p3, t);
        let d_sq = q.distance_sq(&pt);
        if d_sq < min_d_sq {
            min_d_sq = d_sq;
        }
    }

    min_d_sq.sqrt()
}
