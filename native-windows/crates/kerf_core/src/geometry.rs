//! High-level vector geometry, path bounds, hit-testing, and polyline simplification.

use crate::bezier::{
    cubic_arc_length, cubic_exact_bounds, point_to_cubic_min_dist, BezierNodeC, Point2D, Rect2D,
};

#[repr(i32)]
#[derive(Debug, Clone, Copy, PartialEq)]
pub enum HitPartType {
    None = 0,
    Anchor = 1,
    InHandle = 2,
    OutHandle = 3,
    Segment = 4,
}

/// Computes the overall tight bounding box of a collection of Bézier nodes.
pub fn compute_path_bounds(nodes: &[BezierNodeC], is_closed: bool) -> Rect2D {
    if nodes.is_empty() {
        return Rect2D::new(0.0, 0.0, 0.0, 0.0);
    }

    if nodes.len() == 1 {
        return Rect2D::new(nodes[0].anchor_x, nodes[0].anchor_y, 0.0, 0.0);
    }

    let mut min_x = f64::MAX;
    let mut min_y = f64::MAX;
    let mut max_x = f64::MIN;
    let mut max_y = f64::MIN;

    let segment_count = if is_closed { nodes.len() } else { nodes.len() - 1 };

    for i in 0..segment_count {
        let curr = &nodes[i];
        let next = &nodes[(i + 1) % nodes.len()];

        let p0 = Point2D::new(curr.anchor_x, curr.anchor_y);
        let p1 = Point2D::new(curr.out_x, curr.out_y);
        let p2 = Point2D::new(next.in_x, next.in_y);
        let p3 = Point2D::new(next.anchor_x, next.anchor_y);

        let (b_min_x, b_min_y, b_max_x, b_max_y) = cubic_exact_bounds(p0, p1, p2, p3);

        if b_min_x < min_x { min_x = b_min_x; }
        if b_min_y < min_y { min_y = b_min_y; }
        if b_max_x > max_x { max_x = b_max_x; }
        if b_max_y > max_y { max_y = b_max_y; }
    }

    Rect2D::from_min_max(min_x, min_y, max_x, max_y)
}

/// Computes the total arc length of a path.
pub fn compute_path_length(nodes: &[BezierNodeC], is_closed: bool) -> f64 {
    if nodes.len() < 2 {
        return 0.0;
    }

    let mut total = 0.0;
    let segment_count = if is_closed { nodes.len() } else { nodes.len() - 1 };

    for i in 0..segment_count {
        let curr = &nodes[i];
        let next = &nodes[(i + 1) % nodes.len()];

        let p0 = Point2D::new(curr.anchor_x, curr.anchor_y);
        let p1 = Point2D::new(curr.out_x, curr.out_y);
        let p2 = Point2D::new(next.in_x, next.in_y);
        let p3 = Point2D::new(next.anchor_x, next.anchor_y);

        total += cubic_arc_length(p0, p1, p2, p3);
    }

    total
}

/// Hit-tests a point against a Bézier path.
/// Tests anchors, handles, and then curve segments.
pub fn hit_test_path(
    nodes: &[BezierNodeC],
    is_closed: bool,
    query: Point2D,
    threshold: f64,
) -> (HitPartType, i32, f64) {
    if nodes.is_empty() {
        return (HitPartType::None, -1, f64::MAX);
    }

    let thresh_sq = threshold * threshold;

    // 1. Check anchor points
    for (i, node) in nodes.iter().enumerate() {
        let anchor = Point2D::new(node.anchor_x, node.anchor_y);
        let d_sq = query.distance_sq(&anchor);
        if d_sq <= thresh_sq {
            return (HitPartType::Anchor, i as i32, d_sq.sqrt());
        }
    }

    // 2. Check tangent handles
    for (i, node) in nodes.iter().enumerate() {
        let in_h = Point2D::new(node.in_x, node.in_y);
        if in_h.distance_sq(&Point2D::new(node.anchor_x, node.anchor_y)) > 1.0 {
            let d_sq = query.distance_sq(&in_h);
            if d_sq <= thresh_sq {
                return (HitPartType::InHandle, i as i32, d_sq.sqrt());
            }
        }

        let out_h = Point2D::new(node.out_x, node.out_y);
        if out_h.distance_sq(&Point2D::new(node.anchor_x, node.anchor_y)) > 1.0 {
            let d_sq = query.distance_sq(&out_h);
            if d_sq <= thresh_sq {
                return (HitPartType::OutHandle, i as i32, d_sq.sqrt());
            }
        }
    }

    // 3. Check curve segments
    let segment_count = if is_closed { nodes.len() } else { nodes.len() - 1 };
    let mut closest_segment = -1;
    let mut min_segment_dist = f64::MAX;

    for i in 0..segment_count {
        let curr = &nodes[i];
        let next = &nodes[(i + 1) % nodes.len()];

        let p0 = Point2D::new(curr.anchor_x, curr.anchor_y);
        let p1 = Point2D::new(curr.out_x, curr.out_y);
        let p2 = Point2D::new(next.in_x, next.in_y);
        let p3 = Point2D::new(next.anchor_x, next.anchor_y);

        let dist = point_to_cubic_min_dist(query, p0, p1, p2, p3);
        if dist < min_segment_dist {
            min_segment_dist = dist;
            closest_segment = i as i32;
        }
    }

    if min_segment_dist <= threshold {
        (HitPartType::Segment, closest_segment, min_segment_dist)
    } else {
        (HitPartType::None, -1, min_segment_dist)
    }
}

/// Ramer-Douglas-Peucker simplification for 2D points.
pub fn simplify_rdp(points: &[Point2D], epsilon: f64) -> Vec<Point2D> {
    if points.len() <= 2 {
        return points.to_vec();
    }

    let mut keep = vec![true; points.len()];
    rdp_recursive(points, 0, points.len() - 1, epsilon * epsilon, &mut keep);

    points
        .iter()
        .enumerate()
        .filter(|&(i, _)| keep[i])
        .map(|(_, &p)| p)
        .collect()
}

fn rdp_recursive(
    points: &[Point2D],
    start: usize,
    end: usize,
    eps_sq: f64,
    keep: &mut [bool],
) {
    if end <= start + 1 {
        return;
    }

    let mut max_d_sq = 0.0;
    let mut index = start;

    let a = points[start];
    let b = points[end];
    let dx = b.x - a.x;
    let dy = b.y - a.y;
    let len_sq = dx * dx + dy * dy;

    for i in (start + 1)..end {
        let p = points[i];
        let d_sq = if len_sq < 1e-12 {
            p.distance_sq(&a)
        } else {
            let t = (((p.x - a.x) * dx + (p.y - a.y) * dy) / len_sq).clamp(0.0, 1.0);
            let proj = Point2D::new(a.x + t * dx, a.y + t * dy);
            p.distance_sq(&proj)
        };

        if d_sq > max_d_sq {
            max_d_sq = d_sq;
            index = i;
        }
    }

    if max_d_sq > eps_sq {
        rdp_recursive(points, start, index, eps_sq, keep);
        rdp_recursive(points, index, end, eps_sq, keep);
    } else {
        for i in (start + 1)..end {
            keep[i] = false;
        }
    }
}

/// Flattens a closed or open path of BezierNodeC into discrete points using adaptive de Casteljau recursion.
pub fn flatten_path_to_points(nodes: &[BezierNodeC], is_closed: bool, tolerance: f64) -> Vec<Point2D> {
    if nodes.is_empty() {
        return Vec::new();
    }
    if nodes.len() == 1 {
        return vec![Point2D::new(nodes[0].anchor_x, nodes[0].anchor_y)];
    }

    let mut points = Vec::with_capacity(nodes.len() * 8);
    let segment_count = if is_closed { nodes.len() } else { nodes.len() - 1 };

    for i in 0..segment_count {
        let curr = &nodes[i];
        let next = &nodes[(i + 1) % nodes.len()];

        let p0 = Point2D::new(curr.anchor_x, curr.anchor_y);
        let p1 = Point2D::new(curr.out_x, curr.out_y);
        let p2 = Point2D::new(next.in_x, next.in_y);
        let p3 = Point2D::new(next.anchor_x, next.anchor_y);

        let mut segment_points = Vec::with_capacity(16);
        crate::bezier::flatten_cubic(p0, p1, p2, p3, tolerance, &mut segment_points);

        if points.is_empty() {
            points.extend(segment_points);
        } else if !segment_points.is_empty() {
            // Avoid duplicate point at segment boundary
            points.extend_from_slice(&segment_points[1..]);
        }
    }

    points
}

/// Computes the signed polygon area using the shoelace formula.
/// Positive = counter-clockwise, Negative = clockwise.
pub fn compute_polygon_signed_area(pts: &[Point2D]) -> f64 {
    let n = pts.len();
    if n < 3 {
        return 0.0;
    }

    let mut area = 0.0;
    for i in 0..n {
        let j = (i + 1) % n;
        area += pts[i].x * pts[j].y - pts[j].x * pts[i].y;
    }
    area * 0.5
}

/// Computes the signed area of a closed Bézier path by adaptive subdivision.
pub fn compute_path_signed_area(nodes: &[BezierNodeC], tolerance: f64) -> f64 {
    if nodes.len() < 3 {
        return 0.0;
    }
    let pts = flatten_path_to_points(nodes, true, tolerance);
    compute_polygon_signed_area(&pts)
}

/// Tests whether an arbitrary point lies inside a closed Bézier path using the winding number rule.
pub fn path_contains_point(nodes: &[BezierNodeC], query: Point2D, tolerance: f64) -> bool {
    if nodes.len() < 3 {
        return false;
    }

    let pts = flatten_path_to_points(nodes, true, tolerance);
    if pts.len() < 3 {
        return false;
    }

    let mut winding = 0;
    let n = pts.len();

    for i in 0..n {
        let p1 = pts[i];
        let p2 = pts[(i + 1) % n];

        if p1.y <= query.y {
            if p2.y > query.y {
                let cross = (p2.x - p1.x) * (query.y - p1.y) - (query.x - p1.x) * (p2.y - p1.y);
                if cross > 0.0 {
                    winding += 1;
                }
            }
        } else if p2.y <= query.y {
            let cross = (p2.x - p1.x) * (query.y - p1.y) - (query.x - p1.x) * (p2.y - p1.y);
            if cross < 0.0 {
                winding -= 1;
            }
        }
    }

    winding != 0
}

/// Computes the 2D Convex Hull of a set of points using Andrew's Monotone Chain algorithm.
/// Returns points in counter-clockwise order.
pub fn compute_convex_hull(points: &[Point2D]) -> Vec<Point2D> {
    if points.len() <= 2 {
        return points.to_vec();
    }

    let mut pts = points.to_vec();
    // Sort primarily by X, secondarily by Y
    pts.sort_by(|a, b| {
        if (a.x - b.x).abs() > 1e-9 {
            a.x.partial_cmp(&b.x).unwrap_or(std::cmp::Ordering::Equal)
        } else {
            a.y.partial_cmp(&b.y).unwrap_or(std::cmp::Ordering::Equal)
        }
    });
    pts.dedup_by(|a, b| (a.x - b.x).abs() < 1e-9 && (a.y - b.y).abs() < 1e-9);

    if pts.len() <= 2 {
        return pts;
    }

    fn cross(o: &Point2D, a: &Point2D, b: &Point2D) -> f64 {
        (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x)
    }

    // Lower hull
    let mut lower = Vec::with_capacity(pts.len());
    for p in &pts {
        while lower.len() >= 2 && cross(&lower[lower.len() - 2], &lower[lower.len() - 1], p) <= 0.0 {
            lower.pop();
        }
        lower.push(*p);
    }

    // Upper hull
    let mut upper = Vec::with_capacity(pts.len());
    for p in pts.iter().rev() {
        while upper.len() >= 2 && cross(&upper[upper.len() - 2], &upper[upper.len() - 1], p) <= 0.0 {
            upper.pop();
        }
        upper.push(*p);
    }

    // Remove duplicates at endpoints
    lower.pop();
    upper.pop();

    lower.extend(upper);
    lower
}
