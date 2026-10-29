/// Liquid Glass Optical Refraction Engine
/// Implements Snell refraction, chromatic dispersion (per-channel IOR),
/// squircle SDF height-profile slope distortion, and specular highlights.

pub const DEFAULT_IOR: [f32; 3] = [1.44, 1.50, 1.56]; // R, G, B chromatic dispersion

/// Bilinear interpolation sampler over a 4-byte-per-pixel buffer (BGRA or RGBA).
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
    if w <= 0 || h <= 0 || buf.is_empty() {
        return [255.0, 255.0, 255.0];
    }

    let x = x.max(0.0).min((w - 1) as f32);
    let y = y.max(0.0).min((h - 1) as f32);

    let x0 = x.floor() as i32;
    let y0 = y.floor() as i32;
    let x1 = (x0 + 1).min(w - 1);
    let y1 = (y0 + 1).min(h - 1);

    let fx = x - x0 as f32;
    let fy = y - y0 as f32;

    let get_rgb = |xx: i32, yy: i32| -> [f32; 3] {
        let idx = (yy as usize) * (stride as usize) + (xx as usize) * 4;
        if idx + 3 < buf.len() {
            if is_bgra {
                // Buffer is B, G, R, A -> return R, G, B
                [buf[idx + 2] as f32, buf[idx + 1] as f32, buf[idx] as f32]
            } else {
                // Buffer is R, G, B, A -> return R, G, B
                [buf[idx] as f32, buf[idx + 1] as f32, buf[idx + 2] as f32]
            }
        } else {
            [255.0, 255.0, 255.0]
        }
    };

    let a = get_rgb(x0, y0);
    let b = get_rgb(x1, y0);
    let c = get_rgb(x0, y1);
    let d = get_rgb(x1, y1);

    let mut out = [0.0f32; 3];
    for i in 0..3 {
        let top = a[i] * (1.0 - fx) + b[i] * fx;
        let bot = c[i] * (1.0 - fx) + d[i] * fx;
        out[i] = top * (1.0 - fy) + bot * fy;
    }
    out
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

/// Convex squircle height profile, x in [0,1] (0 = outer edge, 1 = flat top).
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

/// Snell refraction. `i` and `n` are unit vectors, eta = n1/n2.
#[inline(always)]
pub fn refract(i: [f32; 3], n: [f32; 3], eta: f32) -> [f32; 3] {
    let d = i[0] * n[0] + i[1] * n[1] + i[2] * n[2];
    let k = 1.0 - eta * eta * (1.0 - d * d);
    if k < 0.0 {
        return [0.0, 0.0, -1.0]; // total internal reflection fallback
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
/// Supports backdrop buffers with extended border margins, real Snell optical refraction,
/// chromatic dispersion (per-channel IOR splitting), frosted glass diffusion,
/// squircle slope distortion, specular highlights, and antialiased transparency.
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

    // Prepare frosted buffer if frost_sigma > 0
    let frost_radius = (frost_sigma * 1.5).round() as i32;
    let frosted_storage;
    let frosted_ref: &[u8] = if frost_radius > 0 {
        let mut fbuf = vec![0u8; bg_buf.len()];
        apply_box_blur(bg_buf, &mut fbuf, bg_w, bg_h, bg_stride, frost_radius);
        frosted_storage = fbuf;
        &frosted_storage
    } else {
        bg_buf
    };

    let bezel_val = bezel.max(2.0);
    let bezel_h = bezel_height.max(1.0);
    let thickness = glass_thickness.max(5.0);

    // Light direction normalization
    let light_len = (light_dir[0] * light_dir[0] + light_dir[1] * light_dir[1]).sqrt().max(1e-6);
    let light = [light_dir[0] / light_len, light_dir[1] / light_len];

    let incident = [0.0f32, 0.0f32, -1.0f32];

    // Chromatic dispersion indices of refraction (Red, Green, Blue)
    // Red bends less (lower IOR), Blue bends more (higher IOR)
    let d_ior = if dispersion <= 0.0 { 0.07f32 } else { dispersion.max(0.01).min(0.25) };
    let ior = [1.50f32 - d_ior, 1.50f32, 1.50f32 + d_ior];

    let hw = out_w as f32 * 0.5f32;
    let hh = out_h as f32 * 0.5f32;
    let cx = hw;
    let cy = hh;

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
                // Outside the squircle: transparent
                out_buf[out_px_idx] = 0;
                out_buf[out_px_idx + 1] = 0;
                out_buf[out_px_idx + 2] = 0;
                out_buf[out_px_idx + 3] = 0;
                continue;
            }

            // Outward 2D normal from SDF gradient
            let e = 0.5f32;
            let gx = sdf(fx + e, fy, cx, cy, hw, hh, radius) - sdf(fx - e, fy, cx, cy, hw, hh, radius);
            let gy = sdf(fx, fy + e, cx, cy, hw, hh, radius) - sdf(fx, fy - e, cx, cy, hw, hh, radius);
            let gl = (gx * gx + gy * gy).sqrt().max(1e-6);
            let n2 = [gx / gl, gy / gl];

            let depth = -d;
            let t = (depth / bezel_val).max(0.0).min(1.0);
            let slope = height_slope(t) * (bezel_h / bezel_val);
            let in_bezel = depth < bezel_val;

            // 3D surface normal
            let (nx, ny) = if in_bezel {
                (n2[0] * slope, n2[1] * slope)
            } else {
                (0.0, 0.0)
            };
            let nl = (nx * nx + ny * ny + 1.0).sqrt();
            let normal = [nx / nl, ny / nl, 1.0 / nl];

            // Refract per channel -> chromatic dispersion (separate IOR per R, G, B)
            let mut refracted_rgb = [0.0f32; 3];
            for c in 0..3 {
                let r = refract(incident, normal, 1.0 / ior[c]);
                let travel = thickness / (-r[2]).max(0.05);
                let sx = bg_fx + r[0] * travel;
                let sy = bg_fy + r[1] * travel;
                refracted_rgb[c] = sample_bilinear(frosted_ref, bg_w, bg_h, bg_stride, sx, sy, is_bgra)[c];
            }

            // Specular highlight on bezel facing the light
            let spec = if in_bezel {
                let facing = (n2[0] * light[0] + n2[1] * light[1]).abs();
                facing.powf(1.5) * (1.0 - t).powi(2) * 0.9f32 * specular_intensity
            } else {
                0.0
            };

            // Composite tint, refraction, and specular highlight
            let mut col = [0.0f32; 3];
            for c in 0..3 {
                let blended = refracted_rgb[c] * (1.0 - tint_alpha) + tint_rgb[c] * tint_alpha;
                col[c] = (blended + 255.0 * spec).min(255.0).max(0.0);
            }

            // Anti-aliased outer edge compositing against direct background pixel
            let base_rgb = sample_bilinear(bg_buf, bg_w, bg_h, bg_stride, bg_fx, bg_fy, is_bgra);
            let final_r = (base_rgb[0] * (1.0 - coverage) + col[0] * coverage) as u8;
            let final_g = (base_rgb[1] * (1.0 - coverage) + col[1] * coverage) as u8;
            let final_b = (base_rgb[2] * (1.0 - coverage) + col[2] * coverage) as u8;
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
        0.07,
        light_dir,
        tint_rgb,
        tint_alpha,
        specular_intensity,
        is_bgra,
    );
}
