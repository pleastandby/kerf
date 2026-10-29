the gradient handles are not moving. it's fixed also, the gradient controls doesn't have to be active all the time, it should only be active when the gradient is editing/ selected from effects bar.
Also improve the liquid glass, add real liquid glass characteristics using graphics, use distortions, all the slider movements should be smoother and the gradient sliders too

use this sample:
use image::{imageops, Rgba, RgbaImage};

// ---------- parameters ----------
const BEZEL: f32 = 40.0;        // width of the refracting edge (px)
const BEZEL_HEIGHT: f32 = 32.0; // virtual height of the curved edge
const GLASS_THICKNESS: f32 = 60.0;
const FROST_SIGMA: f32 = 6.0;
const IOR: [f32; 3] = [1.48, 1.50, 1.52]; // R,G,B -> chromatic dispersion

// ---------- helpers ----------
fn sample(img: &RgbaImage, x: f32, y: f32) -> [f32; 3] {
    let (w, h) = (img.width() as i32, img.height() as i32);
    let x = x.clamp(0.0, (w - 1) as f32);
    let y = y.clamp(0.0, (h - 1) as f32);
    let (x0, y0) = (x.floor() as i32, y.floor() as i32);
    let (x1, y1) = ((x0 + 1).min(w - 1), (y0 + 1).min(h - 1));
    let (fx, fy) = (x - x0 as f32, y - y0 as f32);
    let px = |xx: i32, yy: i32| img.get_pixel(xx as u32, yy as u32).0;
    let (a, b, c, d) = (px(x0, y0), px(x1, y0), px(x0, y1), px(x1, y1));
    let mut out = [0.0; 3];
    for i in 0..3 {
        let top = a[i] as f32 * (1.0 - fx) + b[i] as f32 * fx;
        let bot = c[i] as f32 * (1.0 - fx) + d[i] as f32 * fx;
        out[i] = top * (1.0 - fy) + bot * fy;
    }
    out
}

/// Signed distance to a rounded rectangle (negative inside).
fn sdf(px: f32, py: f32, cx: f32, cy: f32, hw: f32, hh: f32, r: f32) -> f32 {
    let qx = (px - cx).abs() - (hw - r);
    let qy = (py - cy).abs() - (hh - r);
    let outside = (qx.max(0.0).powi(2) + qy.max(0.0).powi(2)).sqrt();
    outside + qx.max(qy).min(0.0) - r
}

/// Convex squircle height profile, x in [0,1] (0 = outer edge, 1 = flat top).
fn height(x: f32) -> f32 {
    (1.0 - (1.0 - x.clamp(0.0, 1.0)).powi(4)).powf(0.25)
}

fn height_slope(x: f32) -> f32 {
    let e = 0.001;
    (height(x + e) - height(x - e)) / (2.0 * e)
}

/// Snell refraction. `i` and `n` are unit vectors, eta = n1/n2.
fn refract(i: [f32; 3], n: [f32; 3], eta: f32) -> [f32; 3] {
    let d = i[0] * n[0] + i[1] * n[1] + i[2] * n[2];
    let k = 1.0 - eta * eta * (1.0 - d * d);
    if k < 0.0 {
        return [0.0, 0.0, -1.0]; // total internal reflection fallback
    }
    let s = eta * d + k.sqrt();
    [eta * i[0] - s * n[0], eta * i[1] - s * n[1], eta * i[2] - s * n[2]]
}

fn main() {
    let bg = image::open("bg.png").expect("bg.png").to_rgba8();
    let frosted = imageops::blur(&bg, FROST_SIGMA);
    let mut out = bg.clone();

    // glass panel geometry
    let (w, h) = (bg.width() as f32, bg.height() as f32);
    let (cx, cy) = (w / 2.0, h / 2.0);
    let (hw, hh) = (w * 0.30, h * 0.18);
    let radius = hh; // fully rounded "pill"
    let light = [-0.7071_f32, -0.7071]; // light from top-left

    for y in 0..bg.height() {
        for x in 0..bg.width() {
            let (fx, fy) = (x as f32 + 0.5, y as f32 + 0.5);
            let d = sdf(fx, fy, cx, cy, hw, hh, radius);
            let coverage = (0.5 - d).clamp(0.0, 1.0); // anti-aliased mask
            if coverage <= 0.0 {
                continue;
            }

            // outward 2D normal from SDF gradient
            let e = 0.5;
            let gx = sdf(fx + e, fy, cx, cy, hw, hh, radius) - sdf(fx - e, fy, cx, cy, hw, hh, radius);
            let gy = sdf(fx, fy + e, cx, cy, hw, hh, radius) - sdf(fx, fy - e, cx, cy, hw, hh, radius);
            let gl = (gx * gx + gy * gy).sqrt().max(1e-6);
            let n2 = [gx / gl, gy / gl];

            let depth = -d;                       // distance inside the edge
            let t = (depth / BEZEL).clamp(0.0, 1.0);
            let slope = height_slope(t) * (BEZEL_HEIGHT / BEZEL);
            let in_bezel = depth < BEZEL;

            // 3D surface normal (flat in the middle)
            let (nx, ny) = if in_bezel { (n2[0] * slope, n2[1] * slope) } else { (0.0, 0.0) };
            let nl = (nx * nx + ny * ny + 1.0).sqrt();
            let normal = [nx / nl, ny / nl, 1.0 / nl];
            let incident = [0.0, 0.0, -1.0];

            // refract per channel -> chromatic dispersion
            let mut rgb = [0.0_f32; 3];
            for c in 0..3 {
                let r = refract(incident, normal, 1.0 / IOR[c]);
                let travel = GLASS_THICKNESS / (-r[2]).max(0.05);
                let (sx, sy) = (fx + r[0] * travel, fy + r[1] * travel);
                rgb[c] = sample(&frosted, sx, sy)[c];
            }

            // slight white tint + rim specular highlight
            let mut col = [0.0_f32; 3];
            let spec = if in_bezel {
                let facing = (n2[0] * light[0] + n2[1] * light[1]).abs();
                facing.powf(1.5) * (1.0 - t).powi(2) * 0.9
            } else {
                0.0
            };
            for c in 0..3 {
                col[c] = (rgb[c] * 0.92 + 255.0 * 0.08 + 255.0 * spec).min(255.0);
            }

            // composite over original with AA coverage
            let base = bg.get_pixel(x, y).0;
            let mut px = [0u8; 4];
            for c in 0..3 {
                px[c] = (base[c] as f32 * (1.0 - coverage) + col[c] * coverage) as u8;
            }
            px[3] = 255;
            out.put_pixel(x, y, Rgba(px));
        }
    }
    out.save("liquid_glass.png").unwrap();
}