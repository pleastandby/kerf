/// Liquid Glass Optical Refraction Engine — FigCraft Optical Thick-Lens Model
/// Implements FigCraft's exact physical thick-lens curvature profile,
/// Euclidean distance bevel bending, chromatic dispersion window,
/// rim lighting, facing angle highlights, and sheen gradient.

pub const DEFAULT_IOR: [f32; 3] = [1.44, 1.50, 1.56];

pub const BEVEL: f32 = 1.0;
pub const PROFILE_EXP: f32 = 3.5;
pub const MAX_BEND: f32 = 0.999;
pub const RIM_BEND: f32 = 0.2;
pub const DISPERSION_SPREAD: f32 = 0.16;
pub const DISPERSION_STEPS: usize = 8;
pub const HL_LO: f32 = 0.35;
pub const HL_SECONDARY: f32 = 0.6;
pub const BROAD: f32 = 0.05;
pub const ONE_SAMPLE_SPLIT_PX: f32 = 1.0 / 256.0;

#[inline(always)]
pub fn lin(a: f32, b: f32, x: f32) -> f32 {
    ((x - a) / (b - a).abs().max(1e-5)).clamp(0.0, 1.0)
}

#[inline(always)]
pub fn smoothstep(a: f32, b: f32, x: f32) -> f32 {
    let t = ((x - a) / (b - a).abs().max(1e-5)).clamp(0.0, 1.0);
    t * t * (3.0 - 2.0 * t)
}

/// How much of the rim light falls x pixels in from the edge, for a bevel pixels deep.
#[inline(always)]
pub fn rim_light_at(x: f32, bevel: f32) -> f32 {
    smoothstep(-bevel - 0.001, -0.2 * bevel, -x)
}

/// How much light an edge catches, facing being the cosine between outward normal and light.
#[inline(always)]
pub fn catches(facing: f32) -> f32 {
    smoothstep(HL_LO, 1.0, facing) + HL_SECONDARY * smoothstep(HL_LO, 1.0, -facing)
}

/// How strongly the gradient across the curve shows x pixels in from the edge.
#[inline(always)]
pub fn sheen_at(x: f32, depth: f32, bevel: f32) -> f32 {
    lin(-depth, -bevel + 1e-4, -x).powi(2)
}

/// How far the backdrop moves at x pixels in from the edge (FigCraft thick-lens curve profile).
#[inline(always)]
pub fn bend_at(x: f32, depth: f32, bevel: f32, refraction: f32) -> f32 {
    if x >= depth.max(1e-4) + bevel {
        return 0.0;
    }
    let sd = -x;
    let t = lin(-depth - bevel, -bevel, sd);
    let most = MAX_BEND * depth;
    let core = (t.powf(PROFILE_EXP) * depth * refraction).clamp(-most, most);
    let rim = refraction * RIM_BEND * most;
    let u = lin(-bevel - 1e-4, -0.3 * bevel, sd);
    core + (rim - core) * u
}

/// Bilinear sample of 4-channel buffer at (x, y), returns [R, G, B, A] in 0..=255.
#[inline(always)]
pub fn sample_rgba(
    buf: &[u8],
    w: i32,
    h: i32,
    stride: i32,
    x: f32,
    y: f32,
    is_bgra: bool,
) -> [f32; 4] {
    if w <= 0 || h <= 0 || buf.is_empty() {
        return [0.0, 0.0, 0.0, 0.0];
    }
    let x = x - 0.5;
    let y = y - 0.5;
    let x0 = x.floor() as i32;
    let y0 = y.floor() as i32;
    let tx = x - x0 as f32;
    let ty = y - y0 as f32;

    let px = |xi: i32, yi: i32| -> [f32; 4] {
        if xi < 0 || yi < 0 || xi >= w || yi >= h {
            return [0.0, 0.0, 0.0, 0.0];
        }
        let i = (yi as usize) * (stride as usize) + (xi as usize) * 4;
        if i + 3 < buf.len() {
            if is_bgra {
                // Buffer is B, G, R, A -> return R, G, B, A
                [buf[i + 2] as f32, buf[i + 1] as f32, buf[i] as f32, buf[i + 3] as f32]
            } else {
                [buf[i] as f32, buf[i + 1] as f32, buf[i + 2] as f32, buf[i + 3] as f32]
            }
        } else {
            [0.0, 0.0, 0.0, 0.0]
        }
    };

    let a = px(x0, y0);
    let b = px(x0 + 1, y0);
    let c = px(x0, y0 + 1);
    let d = px(x0 + 1, y0 + 1);

    let mut out = [0.0f32; 4];
    for k in 0..4 {
        let top = a[k] + (b[k] - a[k]) * tx;
        let bot = c[k] + (d[k] - c[k]) * tx;
        out[k] = top + (bot - top) * ty;
    }
    out
}

/// Bilinear interpolation sampler over a 4-byte-per-pixel buffer (BGRA or RGBA), returning [R, G, B].
#[inline(always)]
pub fn sample_bilinear(
    buf: &[u8],
    w: i32,
    h: i32,
    stride: i32,
    x: f32,
    y: f32,
    is_bgra: bool,
) -> [f32; 3] {
    let s = sample_rgba(buf, w, h, stride, x, y, is_bgra);
    [s[0], s[1], s[2]]
}

/// Dispersed backdrop sampling with FigCraft chromatic aberration window.
pub fn dispersed(
    buf: &[u8],
    w: i32,
    h: i32,
    stride: i32,
    x: f32,
    y: f32,
    dx: f32,
    dy: f32,
    k: f32,
    is_bgra: bool,
) -> [f32; 4] {
    if k < 0.001 || (dx * dx + dy * dy).sqrt() * k < ONE_SAMPLE_SPLIT_PX {
        let s = sample_rgba(buf, w, h, stride, x + dx, y + dy, is_bgra);
        return [s[0].min(s[3]), s[1].min(s[3]), s[2].min(s[3]), s[3]];
    }
    dispersion_window(buf, w, h, stride, x, y, dx, dy, k, is_bgra)
}

fn dispersion_window(
    buf: &[u8],
    w: i32,
    h: i32,
    stride: i32,
    x: f32,
    y: f32,
    dx: f32,
    dy: f32,
    k: f32,
    is_bgra: bool,
) -> [f32; 4] {
    let last = (DISPERSION_STEPS - 1) as f32;
    let mut acc = [0.0f32; 4];
    let mut sum = 0.0f32;
    for i in 1..DISPERSION_STEPS - 1 {
        let t = i as f32 / last - 0.5;
        let weight = 1.0 - (2.0 * t).abs();
        let r = sample_rgba(buf, w, h, stride, x + dx * (1.0 + (t + 0.5) * k), y + dy * (1.0 + (t + 0.5) * k), is_bgra);
        let g = sample_rgba(buf, w, h, stride, x + dx * (1.0 + t * k), y + dy * (1.0 + t * k), is_bgra);
        let b = sample_rgba(buf, w, h, stride, x + dx * (1.0 + (t - 0.5) * k), y + dy * (1.0 + (t - 0.5) * k), is_bgra);
        acc[0] += r[0] * weight;
        acc[1] += g[1] * weight;
        acc[2] += b[2] * weight;
        acc[3] += r[3].max(g[3]).max(b[3]) * weight;
        sum += weight;
    }
    let a = acc[3] / sum.max(1e-5);
    [(acc[0] / sum).min(a), (acc[1] / sum).min(a), (acc[2] / sum).min(a), a]
}

/// Signed distance to a rounded rectangle (negative inside).
#[inline(always)]
pub fn sdf(px: f32, py: f32, cx: f32, cy: f32, hw: f32, hh: f32, r: f32) -> f32 {
    let r = r.max(0.0).min(hw).min(hh);
    let qx = (px - cx).abs() - (hw - r);
    let qy = (py - cy).abs() - (hh - r);
    let outside = ((qx.max(0.0) * qx.max(0.0)) + (qy.max(0.0) * qy.max(0.0))).sqrt();
    outside + qx.max(qy).min(0.0) - r
}

/// Convex squircle height profile, x in [0,1].
#[inline(always)]
pub fn height(x: f32) -> f32 {
    let clamped = x.max(0.0).min(1.0);
    let inv = 1.0 - clamped;
    let inv4 = inv * inv * inv * inv;
    (1.0 - inv4).max(0.0).powf(0.25)
}

#[inline(always)]
pub fn height_slope(x: f32) -> f32 {
    let e = 0.001f32;
    (height(x + e) - height(x - e)) / (2.0 * e)
}

/// Snell refraction.
#[inline(always)]
pub fn refract(i: [f32; 3], n: [f32; 3], eta: f32) -> [f32; 3] {
    let d = i[0] * n[0] + i[1] * n[1] + i[2] * n[2];
    let k = 1.0 - eta * eta * (1.0 - d * d);
    if k < 0.0 {
        return [0.0, 0.0, -1.0];
    }
    let s = eta * d + k.sqrt();
    [
        eta * i[0] - s * n[0],
        eta * i[1] - s * n[1],
        eta * i[2] - s * n[2],
    ]
}

/// Fast separable horizontal and vertical box blur for frosted glass diffusion.
pub fn apply_box_blur(
    src: &[u8],
    dst: &mut [u8],
    w: i32,
    h: i32,
    stride: i32,
    radius: i32,
) {
    if radius <= 0 || w <= 0 || h <= 0 {
        dst.copy_from_slice(src);
        return;
    }

    let r = radius.min(w / 2).min(h / 2).max(1);
    let mut temp = vec![0u8; src.len()];

    // Horizontal pass
    for y in 0..h {
        let row_offset = (y * stride) as usize;
        for x in 0..w {
            let mut sum_b = 0u32;
            let mut sum_g = 0u32;
            let mut sum_r = 0u32;
            let mut sum_a = 0u32;
            let mut count = 0u32;

            let k_start = (x - r).max(0);
            let k_end = (x + r).min(w - 1);

            for kx in k_start..=k_end {
                let px_idx = row_offset + (kx as usize) * 4;
                sum_b += src[px_idx] as u32;
                sum_g += src[px_idx + 1] as u32;
                sum_r += src[px_idx + 2] as u32;
                sum_a += src[px_idx + 3] as u32;
                count += 1;
            }

            let out_idx = row_offset + (x as usize) * 4;
            temp[out_idx] = (sum_b / count) as u8;
            temp[out_idx + 1] = (sum_g / count) as u8;
            temp[out_idx + 2] = (sum_r / count) as u8;
            temp[out_idx + 3] = (sum_a / count) as u8;
        }
    }

    // Vertical pass
    for x in 0..w {
        for y in 0..h {
            let mut sum_b = 0u32;
            let mut sum_g = 0u32;
            let mut sum_r = 0u32;
            let mut sum_a = 0u32;
            let mut count = 0u32;

            let k_start = (y - r).max(0);
            let k_end = (y + r).min(h - 1);

            for ky in k_start..=k_end {
                let px_idx = (ky * stride) as usize + (x as usize) * 4;
                sum_b += temp[px_idx] as u32;
                sum_g += temp[px_idx + 1] as u32;
                sum_r += temp[px_idx + 2] as u32;
                sum_a += temp[px_idx + 3] as u32;
                count += 1;
            }

            let out_idx = (y * stride) as usize + (x as usize) * 4;
            dst[out_idx] = (sum_b / count) as u8;
            dst[out_idx + 1] = (sum_g / count) as u8;
            dst[out_idx + 2] = (sum_r / count) as u8;
            dst[out_idx + 3] = (sum_a / count) as u8;
        }
    }
}

/// Full optical liquid glass renderer (v2):
/// Supports FigCraft optical thick-lens refraction, chromatic dispersion window,
/// rim lighting, facing highlights, sheen gradient, and antialiased transparency.
pub fn render_liquid_glass_v2(
    bg_buf: &[u8],
    bg_w: i32,
    bg_h: i32,
    bg_stride: i32,
    out_buf: &mut [u8],
    out_w: i32,
    out_h: i32,
    out_stride: i32,
    margin_x: f32,
    margin_y: f32,
    radius: f32,
    bezel: f32,
    bezel_height: f32,
    glass_thickness: f32,
    frost_sigma: f32,
    dispersion: f32,
    light_dir: [f32; 2],
    tint_rgb: [f32; 3],
    tint_alpha: f32,
    specular_intensity: f32,
    is_bgra: bool,
) {
    if bg_w <= 0 || bg_h <= 0 || out_w <= 0 || out_h <= 0 || bg_buf.is_empty() || out_buf.is_empty() {
        return;
    }

    // Unpack FigCraft optical parameters
    let depth = if bezel > 0.0 { bezel } else { 20.0 };
    let refraction = if bezel_height > 0.0 && bezel_height <= 1.0 {
        bezel_height
    } else if bezel_height > 1.0 {
        (bezel_height / 100.0).min(1.0)
    } else {
        0.8
    };
    let splay = glass_thickness.max(0.0).min(1.0);
    let frost = frost_sigma.max(0.0);
    let disp = if dispersion > 0.0 { dispersion.min(1.0) } else { 0.5 };
    let intensity = if specular_intensity > 0.0 { specular_intensity.min(1.0) } else { 0.8 };

    // Prepare frosted buffer if frost > 0
    let frost_radius = (frost * 1.0).round() as i32;
    let frosted_storage;
    let frosted_ref: &[u8] = if frost_radius > 0 {
        let mut fbuf = vec![0u8; bg_buf.len()];
        apply_box_blur(bg_buf, &mut fbuf, bg_w, bg_h, bg_stride, frost_radius);
        frosted_storage = fbuf;
        &frosted_storage
    } else {
        bg_buf
    };

    // Normalize light vector
    let light_len = (light_dir[0] * light_dir[0] + light_dir[1] * light_dir[1]).sqrt().max(1e-6);
    let light = [light_dir[0] / light_len, light_dir[1] / light_len];

    let hw = out_w as f32 * 0.5f32;
    let hh = out_h as f32 * 0.5f32;
    let cx = hw;
    let cy = hh;

    let k_disp = disp * DISPERSION_SPREAD;

    for y in 0..out_h {
        let out_row_idx = (y * out_stride) as usize;
        let fy = y as f32 + 0.5f32;
        let bg_fy = fy + margin_y;

        for x in 0..out_w {
            let out_px_idx = out_row_idx + (x as usize) * 4;
            let fx = x as f32 + 0.5f32;
            let bg_fx = fx + margin_x;

            let d = sdf(fx, fy, cx, cy, hw, hh, radius);
            let coverage = (0.5f32 - d).max(0.0).min(1.0);
            if coverage <= 0.0 {
                out_buf[out_px_idx] = 0;
                out_buf[out_px_idx + 1] = 0;
                out_buf[out_px_idx + 2] = 0;
                out_buf[out_px_idx + 3] = 0;
                continue;
            }

            let dist_in = -d;

            // Outward 2D normal from SDF gradient
            let e = 0.5f32;
            let gx = sdf(fx + e, fy, cx, cy, hw, hh, radius) - sdf(fx - e, fy, cx, cy, hw, hh, radius);
            let gy = sdf(fx, fy + e, cx, cy, hw, hh, radius) - sdf(fx, fy - e, cx, cy, hw, hh, radius);
            let gl = (gx * gx + gy * gy).sqrt().max(1e-6);
            let n2 = [gx / gl, gy / gl];
            let inward = [-n2[0], -n2[1]];

            // Direction away from layer centre (for splay)
            let px = fx - cx;
            let py = fy - cy;
            let len = (px * px + py * py).sqrt();
            let away = if len > 1e-6 { [px / len, py / len] } else { [0.0, 0.0] };

            // Outward bend direction: away from core, modified by splay
            let toward = inward;
            let out_dir = [
                -toward[0] * (1.0 - splay) + away[0] * splay,
                -toward[1] * (1.0 - splay) + away[1] * splay,
            ];

            // FigCraft bend displacement
            let bend = bend_at(dist_in, depth, BEVEL, refraction);
            let move_x = -out_dir[0] * bend;
            let move_y = -out_dir[1] * bend;
            let k = if bend == 0.0 { 0.0 } else { k_disp };

            // Dispersed background sampling
            let mut base = dispersed(frosted_ref, bg_w, bg_h, bg_stride, bg_fx, bg_fy, move_x, move_y, k, is_bgra);

            // FigCraft Rim light and Sheen
            let facing = -(inward[0] * light[0] + inward[1] * light[1]);
            let rim = rim_light_at(dist_in, BEVEL) * catches(facing);
            let sheen = -(out_dir[0] * light[0] + out_dir[1] * light[1]) * sheen_at(dist_in, depth, BEVEL) * BROAD;
            let add = 255.0 * intensity * (rim + sheen);

            // Composite tint
            if tint_alpha > 0.0 {
                base[0] = base[0] * (1.0 - tint_alpha) + tint_rgb[0] * tint_alpha;
                base[1] = base[1] * (1.0 - tint_alpha) + tint_rgb[1] * tint_alpha;
                base[2] = base[2] * (1.0 - tint_alpha) + tint_rgb[2] * tint_alpha;
            }

            // Antialiased outer edge compositing against direct background pixel
            let base_rgb = sample_rgba(bg_buf, bg_w, bg_h, bg_stride, bg_fx, bg_fy, is_bgra);
            let final_r = ((base_rgb[0] * (1.0 - coverage) + (base[0] + add).clamp(0.0, 255.0) * coverage)).round().min(255.0).max(0.0) as u8;
            let final_g = ((base_rgb[1] * (1.0 - coverage) + (base[1] + add).clamp(0.0, 255.0) * coverage)).round().min(255.0).max(0.0) as u8;
            let final_b = ((base_rgb[2] * (1.0 - coverage) + (base[2] + add).clamp(0.0, 255.0) * coverage)).round().min(255.0).max(0.0) as u8;
            let final_a = (coverage * 255.0f32).round().min(255.0).max(0.0) as u8;

            if is_bgra {
                out_buf[out_px_idx] = final_b;
                out_buf[out_px_idx + 1] = final_g;
                out_buf[out_px_idx + 2] = final_r;
                out_buf[out_px_idx + 3] = final_a;
            } else {
                out_buf[out_px_idx] = final_r;
                out_buf[out_px_idx + 1] = final_g;
                out_buf[out_px_idx + 2] = final_b;
                out_buf[out_px_idx + 3] = final_a;
            }
        }
    }
}

/// Legacy entry point for backwards compatibility.
pub fn render_liquid_glass(
    bg_buf: &[u8],
    w: i32,
    h: i32,
    stride: i32,
    out_buf: &mut [u8],
    _cx: f32,
    _cy: f32,
    _hw: f32,
    _hh: f32,
    radius: f32,
    bezel: f32,
    bezel_height: f32,
    glass_thickness: f32,
    frost_sigma: f32,
    light_dir: [f32; 2],
    tint_rgb: [f32; 3],
    tint_alpha: f32,
    specular_intensity: f32,
    is_bgra: bool,
) {
    render_liquid_glass_v2(
        bg_buf,
        w,
        h,
        stride,
        out_buf,
        w,
        h,
        stride,
        0.0,
        0.0,
        radius,
        bezel,
        bezel_height,
        glass_thickness,
        frost_sigma,
        0.5,
        light_dir,
        tint_rgb,
        tint_alpha,
        specular_intensity,
        is_bgra,
    );
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_figcraft_light_and_gradient_profile() {
        for (x, rim, sheen) in [
            (0.10f32, 1.0f32, 1.0f32),
            (0.25, 0.9888, 1.0),
            (0.50, 0.6843, 1.0),
            (0.75, 0.2330, 1.0),
            (1.00, 0.0, 1.0),
            (2.0, 0.0, 0.8975),
            (4.0, 0.0, 0.7091),
            (6.0, 0.0, 0.5429),
            (8.0, 0.0, 0.3989),
            (16.0, 0.0, 0.0443),
            (20.0, 0.0, 0.0),
            (30.0, 0.0, 0.0),
        ] {
            assert!((rim_light_at(x, 1.0) - rim).abs() < 1e-3, "rim at {}: {} vs {}", x, rim_light_at(x, 1.0), rim);
            assert!((sheen_at(x, 20.0, 1.0) - sheen).abs() < 1e-3, "sheen at {}: {} vs {}", x, sheen_at(x, 20.0, 1.0), sheen);
        }

        assert!((catches(1.0) - 1.0).abs() < 1e-6);
        assert!((catches(std::f32::consts::FRAC_1_SQRT_2) - 0.574).abs() < 1e-3);
        assert_eq!(catches(0.0), 0.0);
        assert!((catches(-1.0) - 0.6).abs() < 1e-6);
        assert!((catches(-std::f32::consts::FRAC_1_SQRT_2) - 0.6 * 0.574).abs() < 1e-3);
    }

    #[test]
    fn test_figcraft_bend_profile() {
        assert_eq!(bend_at(25.0, 20.0, 1.0, 0.8), 0.0);
        let b0 = bend_at(0.0, 20.0, 1.0, 0.8);
        assert!(b0 > 0.0 && b0 < 20.0, "b0 = {}", b0);
        let b1 = bend_at(1.0, 20.0, 1.0, 0.8);
        assert!(b1 > b0, "b1 ({}) should be > b0 ({})", b1, b0);
    }
}
