pub mod animation;
pub mod bezier;
pub mod geometry;
pub mod liquid_glass;
pub mod simd;
pub mod state_machine;

use std::collections::HashMap;
use std::ffi::{CStr, CString};
use std::os::raw::c_char;

use bezier::{
    cubic_exact_bounds, flatten_cubic, BezierNodeC, Point2D, Rect2D,
};
use geometry::{
    compute_convex_hull, compute_path_bounds, compute_path_length, compute_path_signed_area,
    hit_test_path, path_contains_point, simplify_rdp,
};
use simd::detect_simd_capabilities;

static VERSION_C_STR: &[u8] = b"Rust Kerf Core 1.0.0 (MSVC Native x64)\0";

#[no_mangle]
pub extern "C" fn kerf_engine_init() -> i32 {
    1
}

#[no_mangle]
pub extern "C" fn kerf_engine_version() -> *const c_char {
    VERSION_C_STR.as_ptr() as *const c_char
}

#[no_mangle]
pub extern "C" fn kerf_engine_simd_desc() -> *const c_char {
    let caps = detect_simd_capabilities();
    let s = CString::new(caps.description()).unwrap_or_default();
    s.into_raw()
}

#[no_mangle]
pub extern "C" fn kerf_engine_free_string(ptr: *mut c_char) {
    if !ptr.is_null() {
        unsafe {
            let _ = CString::from_raw(ptr);
        }
    }
}

#[no_mangle]
pub extern "C" fn kerf_bezier_bounds(
    p0x: f64, p0y: f64,
    p1x: f64, p1y: f64,
    p2x: f64, p2y: f64,
    p3x: f64, p3y: f64,
    out_min_x: *mut f64,
    out_min_y: *mut f64,
    out_max_x: *mut f64,
    out_max_y: *mut f64,
) {
    if out_min_x.is_null() || out_min_y.is_null() || out_max_x.is_null() || out_max_y.is_null() {
        return;
    }
    let p0 = Point2D::new(p0x, p0y);
    let p1 = Point2D::new(p1x, p1y);
    let p2 = Point2D::new(p2x, p2y);
    let p3 = Point2D::new(p3x, p3y);

    let (min_x, min_y, max_x, max_y) = cubic_exact_bounds(p0, p1, p2, p3);

    unsafe {
        *out_min_x = min_x;
        *out_min_y = min_y;
        *out_max_x = max_x;
        *out_max_y = max_y;
    }
}

#[no_mangle]
pub extern "C" fn kerf_path_compute_bounds(
    nodes_ptr: *const BezierNodeC,
    count: i32,
    is_closed: i32,
    out_rect: *mut Rect2D,
) -> i32 {
    if nodes_ptr.is_null() || count <= 0 || out_rect.is_null() {
        return 0;
    }

    let nodes = unsafe { std::slice::from_raw_parts(nodes_ptr, count as usize) };
    let rect = compute_path_bounds(nodes, is_closed != 0);

    unsafe {
        *out_rect = rect;
    }
    1
}

#[no_mangle]
pub extern "C" fn kerf_bezier_adaptive_flatten(
    p0x: f64, p0y: f64,
    p1x: f64, p1y: f64,
    p2x: f64, p2y: f64,
    p3x: f64, p3y: f64,
    tolerance: f64,
    out_points: *mut Point2D,
    max_points: i32,
    out_count: *mut i32,
) -> i32 {
    if out_points.is_null() || out_count.is_null() || max_points <= 0 {
        return 0;
    }

    let p0 = Point2D::new(p0x, p0y);
    let p1 = Point2D::new(p1x, p1y);
    let p2 = Point2D::new(p2x, p2y);
    let p3 = Point2D::new(p3x, p3y);

    let mut points = Vec::with_capacity(32);
    flatten_cubic(p0, p1, p2, p3, tolerance, &mut points);

    let write_len = points.len().min(max_points as usize);
    unsafe {
        std::ptr::copy_nonoverlapping(points.as_ptr(), out_points, write_len);
        *out_count = write_len as i32;
    }
    1
}

#[no_mangle]
pub extern "C" fn kerf_path_hit_test(
    nodes_ptr: *const BezierNodeC,
    count: i32,
    is_closed: i32,
    qx: f64,
    qy: f64,
    threshold: f64,
    out_part: *mut i32,
    out_index: *mut i32,
    out_dist: *mut f64,
) -> i32 {
    if nodes_ptr.is_null() || count <= 0 {
        return 0;
    }

    let nodes = unsafe { std::slice::from_raw_parts(nodes_ptr, count as usize) };
    let query = Point2D::new(qx, qy);

    let (part, idx, dist) = hit_test_path(nodes, is_closed != 0, query, threshold);

    unsafe {
        if !out_part.is_null() { *out_part = part as i32; }
        if !out_index.is_null() { *out_index = idx; }
        if !out_dist.is_null() { *out_dist = dist; }
    }
    1
}

#[no_mangle]
pub extern "C" fn kerf_path_length(
    nodes_ptr: *const BezierNodeC,
    count: i32,
    is_closed: i32,
) -> f64 {
    if nodes_ptr.is_null() || count < 2 {
        return 0.0;
    }

    let nodes = unsafe { std::slice::from_raw_parts(nodes_ptr, count as usize) };
    compute_path_length(nodes, is_closed != 0)
}

#[no_mangle]
pub extern "C" fn kerf_simplify_points(
    pts_ptr: *const Point2D,
    count: i32,
    epsilon: f64,
    out_pts: *mut Point2D,
    max_pts: i32,
    out_count: *mut i32,
) -> i32 {
    if pts_ptr.is_null() || count <= 0 || out_pts.is_null() || out_count.is_null() || max_pts <= 0 {
        return 0;
    }

    let pts = unsafe { std::slice::from_raw_parts(pts_ptr, count as usize) };
    let simplified = simplify_rdp(pts, epsilon);

    let write_len = simplified.len().min(max_pts as usize);
    unsafe {
        std::ptr::copy_nonoverlapping(simplified.as_ptr(), out_pts, write_len);
        *out_count = write_len as i32;
    }
    1
}

#[no_mangle]
pub extern "C" fn kerf_path_signed_area(
    nodes_ptr: *const BezierNodeC,
    count: i32,
    tolerance: f64,
) -> f64 {
    if nodes_ptr.is_null() || count < 3 {
        return 0.0;
    }
    let nodes = unsafe { std::slice::from_raw_parts(nodes_ptr, count as usize) };
    let tol = if tolerance <= 0.0 { 0.5 } else { tolerance };
    compute_path_signed_area(nodes, tol)
}

#[no_mangle]
pub extern "C" fn kerf_path_contains_point(
    nodes_ptr: *const BezierNodeC,
    count: i32,
    qx: f64,
    qy: f64,
    tolerance: f64,
) -> i32 {
    if nodes_ptr.is_null() || count < 3 {
        return 0;
    }
    let nodes = unsafe { std::slice::from_raw_parts(nodes_ptr, count as usize) };
    let query = Point2D::new(qx, qy);
    let tol = if tolerance <= 0.0 { 0.5 } else { tolerance };
    if path_contains_point(nodes, query, tol) { 1 } else { 0 }
}

#[no_mangle]
pub extern "C" fn kerf_convex_hull(
    pts_ptr: *const Point2D,
    count: i32,
    out_pts: *mut Point2D,
    max_pts: i32,
    out_count: *mut i32,
) -> i32 {
    if pts_ptr.is_null() || count <= 0 || out_pts.is_null() || out_count.is_null() || max_pts <= 0 {
        return 0;
    }
    let pts = unsafe { std::slice::from_raw_parts(pts_ptr, count as usize) };
    let hull = compute_convex_hull(pts);
    let write_len = hull.len().min(max_pts as usize);
    unsafe {
        std::ptr::copy_nonoverlapping(hull.as_ptr(), out_pts, write_len);
        *out_count = write_len as i32;
    }
    1
}

#[no_mangle]
pub extern "C" fn kerf_render_liquid_glass_v2(
    bg_ptr: *const u8,
    bg_w: i32,
    bg_h: i32,
    bg_stride: i32,
    out_ptr: *mut u8,
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
    light_x: f32,
    light_y: f32,
    tint_r: f32,
    tint_g: f32,
    tint_b: f32,
    tint_alpha: f32,
    specular_intensity: f32,
    is_bgra: i32,
) -> i32 {
    if bg_ptr.is_null() || out_ptr.is_null() || bg_w <= 0 || bg_h <= 0 || bg_stride <= 0 || out_w <= 0 || out_h <= 0 || out_stride <= 0 {
        return 0;
    }
    let bg_total = (bg_h as usize) * (bg_stride as usize);
    let out_total = (out_h as usize) * (out_stride as usize);
    let bg_buf = unsafe { std::slice::from_raw_parts(bg_ptr, bg_total) };
    let out_buf = unsafe { std::slice::from_raw_parts_mut(out_ptr, out_total) };

    liquid_glass::render_liquid_glass_v2(
        bg_buf,
        bg_w,
        bg_h,
        bg_stride,
        out_buf,
        out_w,
        out_h,
        out_stride,
        margin_x,
        margin_y,
        radius,
        bezel,
        bezel_height,
        glass_thickness,
        frost_sigma,
        dispersion,
        [light_x, light_y],
        [tint_r, tint_g, tint_b],
        tint_alpha,
        specular_intensity,
        is_bgra != 0,
    );

    1
}

#[no_mangle]
pub extern "C" fn kerf_render_liquid_glass(
    bg_ptr: *const u8,
    width: i32,
    height: i32,
    stride: i32,
    out_ptr: *mut u8,
    cx: f32,
    cy: f32,
    hw: f32,
    hh: f32,
    radius: f32,
    bezel: f32,
    bezel_height: f32,
    glass_thickness: f32,
    frost_sigma: f32,
    light_x: f32,
    light_y: f32,
    tint_r: f32,
    tint_g: f32,
    tint_b: f32,
    tint_alpha: f32,
    specular_intensity: f32,
    is_bgra: i32,
) -> i32 {
    if bg_ptr.is_null() || out_ptr.is_null() || width <= 0 || height <= 0 || stride <= 0 {
        return 0;
    }
    let total_bytes = (height as usize) * (stride as usize);
    let bg_buf = unsafe { std::slice::from_raw_parts(bg_ptr, total_bytes) };
    let out_buf = unsafe { std::slice::from_raw_parts_mut(out_ptr, total_bytes) };

    liquid_glass::render_liquid_glass(
        bg_buf,
        width,
        height,
        stride,
        out_buf,
        cx,
        cy,
        hw,
        hh,
        radius,
        bezel,
        bezel_height,
        glass_thickness,
        frost_sigma,
        [light_x, light_y],
        [tint_r, tint_g, tint_b],
        tint_alpha,
        specular_intensity,
        is_bgra != 0,
    );

    1
}

// ---------------------------------------------------------------------------
// Animation & State Machine C ABI (Vector State Machine Engine - testdata/newreq.md)
// ---------------------------------------------------------------------------

pub struct NativeEngineContext {
    pub clips: Vec<animation::Clip>,
    pub sm_defs: Vec<state_machine::StateMachine>,
    pub sm_runtimes: Vec<Option<state_machine::StateMachineRuntime>>,
    pub last_pose: HashMap<(u32, u16), f32>,
}

#[no_mangle]
pub extern "C" fn kerf_anim_context_create() -> u64 {
    let ctx = Box::new(NativeEngineContext {
        clips: Vec::new(),
        sm_defs: Vec::new(),
        sm_runtimes: Vec::new(),
        last_pose: HashMap::new(),
    });
    Box::into_raw(ctx) as u64
}

#[no_mangle]
pub extern "C" fn kerf_anim_context_destroy(handle: u64) {
    if handle != 0 {
        unsafe {
            let _ = Box::from_raw(handle as *mut NativeEngineContext);
        }
    }
}

#[no_mangle]
pub extern "C" fn kerf_anim_clip_create(
    handle: u64,
    id: u32,
    name_ptr: *const c_char,
    duration: f32,
    fps: f32,
    loop_mode: i32,
) -> u32 {
    let ctx = match unsafe { (handle as *mut NativeEngineContext).as_mut() } {
        Some(c) => c,
        None => return 0,
    };
    let name = if !name_ptr.is_null() {
        unsafe { CStr::from_ptr(name_ptr).to_string_lossy().into_owned() }
    } else {
        "Clip".to_string()
    };
    let mode = match loop_mode {
        1 => animation::LoopMode::Loop,
        2 => animation::LoopMode::PingPong,
        _ => animation::LoopMode::OneShot,
    };
    let idx = ctx.clips.len() as u32;
    ctx.clips.push(animation::Clip::new(id, &name, duration, fps, mode));
    idx
}

#[no_mangle]
pub extern "C" fn kerf_anim_clip_add_track(
    handle: u64,
    clip_idx: u32,
    node: u32,
    prop: u16,
) -> u32 {
    let ctx = match unsafe { (handle as *mut NativeEngineContext).as_mut() } {
        Some(c) => c,
        None => return 0,
    };
    if (clip_idx as usize) < ctx.clips.len() {
        let clip = &mut ctx.clips[clip_idx as usize];
        let t_idx = clip.tracks.len() as u32;
        clip.add_track(animation::Track::new(node, prop));
        t_idx
    } else {
        0
    }
}

#[no_mangle]
pub extern "C" fn kerf_anim_track_add_keyframe(
    handle: u64,
    clip_idx: u32,
    track_idx: u32,
    time: f32,
    val: f32,
    interp_type: i32,
    x1: f32, y1: f32, x2: f32, y2: f32,
) -> i32 {
    let ctx = match unsafe { (handle as *mut NativeEngineContext).as_mut() } {
        Some(c) => c,
        None => return 0,
    };
    if (clip_idx as usize) < ctx.clips.len() {
        let clip = &mut ctx.clips[clip_idx as usize];
        if (track_idx as usize) < clip.tracks.len() {
            let track = &mut clip.tracks[track_idx as usize];
            let interp = match interp_type {
                0 => animation::Interp::Hold,
                1 => animation::Interp::Linear,
                _ => animation::Interp::Cubic { x1, y1, x2, y2 },
            };
            track.add_keyframe(animation::Keyframe::new(time, val, interp));
            return 1;
        }
    }
    0
}

#[no_mangle]
pub extern "C" fn kerf_anim_track_sample(
    handle: u64,
    clip_idx: u32,
    track_idx: u32,
    time: f32,
) -> f32 {
    let ctx = match unsafe { (handle as *mut NativeEngineContext).as_ref() } {
        Some(c) => c,
        None => return 0.0,
    };
    if (clip_idx as usize) < ctx.clips.len() {
        let clip = &ctx.clips[clip_idx as usize];
        if (track_idx as usize) < clip.tracks.len() {
            return animation::sample_track(&clip.tracks[track_idx as usize], time);
        }
    }
    0.0
}

#[no_mangle]
pub extern "C" fn kerf_sm_create(handle: u64, name_ptr: *const c_char) -> u32 {
    let ctx = match unsafe { (handle as *mut NativeEngineContext).as_mut() } {
        Some(c) => c,
        None => return 0,
    };
    let name = if !name_ptr.is_null() {
        unsafe { CStr::from_ptr(name_ptr).to_string_lossy().into_owned() }
    } else {
        "StateMachine".to_string()
    };
    let idx = ctx.sm_defs.len() as u32;
    ctx.sm_defs.push(state_machine::StateMachine::new(&name));
    ctx.sm_runtimes.push(None);
    idx
}

#[no_mangle]
pub extern "C" fn kerf_sm_add_input(
    handle: u64,
    sm_idx: u32,
    name_ptr: *const c_char,
    kind: i32,
    initial_val: f32,
) -> u32 {
    let ctx = match unsafe { (handle as *mut NativeEngineContext).as_mut() } {
        Some(c) => c,
        None => return 0,
    };
    if (sm_idx as usize) < ctx.sm_defs.len() {
        let name = if !name_ptr.is_null() {
            unsafe { CStr::from_ptr(name_ptr).to_string_lossy().into_owned() }
        } else {
            "Input".to_string()
        };
        let input = match kind {
            0 => state_machine::Input::bool_input(&name, initial_val > 0.5),
            1 => state_machine::Input::number_input(&name, initial_val),
            _ => state_machine::Input::trigger_input(&name),
        };
        ctx.sm_defs[sm_idx as usize].add_input(input) as u32
    } else {
        0
    }
}

#[no_mangle]
pub extern "C" fn kerf_sm_set_bool(handle: u64, sm_idx: u32, input_idx: u32, val: i32) {
    let ctx = match unsafe { (handle as *mut NativeEngineContext).as_mut() } {
        Some(c) => c,
        None => return,
    };
    if (sm_idx as usize) < ctx.sm_defs.len() {
        let b = val != 0;
        let sm = &mut ctx.sm_defs[sm_idx as usize];
        if (input_idx as usize) < sm.inputs.len() {
            sm.inputs[input_idx as usize].value = if b { 1.0 } else { 0.0 };
        }
        if let Some(ref mut rt) = ctx.sm_runtimes[sm_idx as usize] {
            rt.set_bool(input_idx as usize, b);
        }
    }
}

#[no_mangle]
pub extern "C" fn kerf_sm_set_number(handle: u64, sm_idx: u32, input_idx: u32, val: f32) {
    let ctx = match unsafe { (handle as *mut NativeEngineContext).as_mut() } {
        Some(c) => c,
        None => return,
    };
    if (sm_idx as usize) < ctx.sm_defs.len() {
        let sm = &mut ctx.sm_defs[sm_idx as usize];
        if (input_idx as usize) < sm.inputs.len() {
            sm.inputs[input_idx as usize].value = val;
        }
        if let Some(ref mut rt) = ctx.sm_runtimes[sm_idx as usize] {
            rt.set_number(input_idx as usize, val);
        }
    }
}

#[no_mangle]
pub extern "C" fn kerf_sm_fire_trigger(handle: u64, sm_idx: u32, input_idx: u32) {
    let ctx = match unsafe { (handle as *mut NativeEngineContext).as_mut() } {
        Some(c) => c,
        None => return,
    };
    if (sm_idx as usize) < ctx.sm_defs.len() {
        let sm = &mut ctx.sm_defs[sm_idx as usize];
        if (input_idx as usize) < sm.inputs.len() {
            sm.inputs[input_idx as usize].fired = true;
        }
        if let Some(ref mut rt) = ctx.sm_runtimes[sm_idx as usize] {
            rt.fire_trigger(input_idx as usize);
        }
    }
}

#[no_mangle]
pub extern "C" fn kerf_sm_add_layer(handle: u64, sm_idx: u32, name_ptr: *const c_char) -> u32 {
    let ctx = match unsafe { (handle as *mut NativeEngineContext).as_mut() } {
        Some(c) => c,
        None => return 0,
    };
    if (sm_idx as usize) < ctx.sm_defs.len() {
        let name = if !name_ptr.is_null() {
            unsafe { CStr::from_ptr(name_ptr).to_string_lossy().into_owned() }
        } else {
            "Layer".to_string()
        };
        ctx.sm_defs[sm_idx as usize].add_layer(state_machine::Layer::new(&name)) as u32
    } else {
        0
    }
}

#[no_mangle]
pub extern "C" fn kerf_sm_add_state(
    handle: u64,
    sm_idx: u32,
    layer_idx: u32,
    name_ptr: *const c_char,
    kind: i32,
    clip_idx: u32,
) -> u32 {
    let ctx = match unsafe { (handle as *mut NativeEngineContext).as_mut() } {
        Some(c) => c,
        None => return 0,
    };
    if (sm_idx as usize) < ctx.sm_defs.len() {
        let sm = &mut ctx.sm_defs[sm_idx as usize];
        if (layer_idx as usize) < sm.layers.len() {
            let name = if !name_ptr.is_null() {
                unsafe { CStr::from_ptr(name_ptr).to_string_lossy().into_owned() }
            } else {
                "State".to_string()
            };
            let state_kind = match kind {
                0 => state_machine::StateKind::Entry,
                1 => state_machine::StateKind::Exit,
                2 => state_machine::StateKind::Any,
                _ => state_machine::StateKind::Animation { clip: clip_idx as usize },
            };
            let id = sm.layers[layer_idx as usize].states.len();
            sm.layers[layer_idx as usize].add_state(state_machine::State::new(id, &name, state_kind)) as u32
        } else {
            0
        }
    } else {
        0
    }
}

#[no_mangle]
pub extern "C" fn kerf_sm_add_transition(
    handle: u64,
    sm_idx: u32,
    layer_idx: u32,
    from_state: u32,
    to_state: u32,
    duration: f32,
    exit_time: f32,
    has_exit_time: i32,
    interrupt: i32,
) -> u32 {
    let ctx = match unsafe { (handle as *mut NativeEngineContext).as_mut() } {
        Some(c) => c,
        None => return 0,
    };
    if (sm_idx as usize) < ctx.sm_defs.len() {
        let sm = &mut ctx.sm_defs[sm_idx as usize];
        if (layer_idx as usize) < sm.layers.len() {
            let mut trans = state_machine::Transition::new(from_state as usize, to_state as usize, duration);
            if has_exit_time != 0 {
                trans = trans.with_exit_time(exit_time);
            }
            trans = trans.with_interrupt(interrupt != 0);
            let idx = sm.layers[layer_idx as usize].transitions.len() as u32;
            sm.layers[layer_idx as usize].add_transition(trans);
            idx
        } else {
            0
        }
    } else {
        0
    }
}

#[no_mangle]
pub extern "C" fn kerf_sm_transition_add_condition(
    handle: u64,
    sm_idx: u32,
    layer_idx: u32,
    trans_idx: u32,
    input_idx: u32,
    op: i32,
    val: f32,
) -> i32 {
    let ctx = match unsafe { (handle as *mut NativeEngineContext).as_mut() } {
        Some(c) => c,
        None => return 0,
    };
    if (sm_idx as usize) < ctx.sm_defs.len() {
        let sm = &mut ctx.sm_defs[sm_idx as usize];
        if (layer_idx as usize) < sm.layers.len() {
            let layer = &mut sm.layers[layer_idx as usize];
            if (trans_idx as usize) < layer.transitions.len() {
                let condition_op = match op {
                    0 => state_machine::Op::Eq,
                    1 => state_machine::Op::Ne,
                    2 => state_machine::Op::Lt,
                    3 => state_machine::Op::Le,
                    4 => state_machine::Op::Gt,
                    _ => state_machine::Op::Ge,
                };
                layer.transitions[trans_idx as usize].conditions.push(
                    state_machine::Condition::new(input_idx as usize, condition_op, val)
                );
                return 1;
            }
        }
    }
    0
}

#[no_mangle]
pub extern "C" fn kerf_sm_advance(handle: u64, sm_idx: u32, dt: f32) -> u32 {
    let ctx = match unsafe { (handle as *mut NativeEngineContext).as_mut() } {
        Some(c) => c,
        None => return 0,
    };
    if (sm_idx as usize) < ctx.sm_defs.len() {
        if ctx.sm_runtimes.len() <= (sm_idx as usize) || ctx.sm_runtimes[sm_idx as usize].is_none() {
            let def = ctx.sm_defs[sm_idx as usize].clone();
            if ctx.sm_runtimes.len() <= (sm_idx as usize) {
                ctx.sm_runtimes.resize(sm_idx as usize + 1, None);
            }
            ctx.sm_runtimes[sm_idx as usize] = Some(state_machine::StateMachineRuntime::new(def));
        }
        if let Some(ref mut rt) = ctx.sm_runtimes[sm_idx as usize] {
            let (redraw, pose) = rt.update(dt, &ctx.clips);
            for (k, v) in pose {
                ctx.last_pose.insert(k, v);
            }
            return if redraw { 1 } else { 0 };
        }
    }
    0
}

#[no_mangle]
pub extern "C" fn kerf_sm_get_property_value(handle: u64, node: u32, prop: u16, fallback: f32) -> f32 {
    let ctx = match unsafe { (handle as *mut NativeEngineContext).as_ref() } {
        Some(c) => c,
        None => return fallback,
    };
    ctx.last_pose.get(&(node, prop)).copied().unwrap_or(fallback)
}

#[no_mangle]
pub extern "C" fn kerf_sm_get_current_state(handle: u64, sm_idx: u32, layer_idx: u32) -> u32 {
    let ctx = match unsafe { (handle as *mut NativeEngineContext).as_ref() } {
        Some(c) => c,
        None => return 0,
    };
    if (sm_idx as usize) < ctx.sm_runtimes.len() {
        if let Some(ref rt) = ctx.sm_runtimes[sm_idx as usize] {
            if (layer_idx as usize) < rt.layer_runtimes.len() {
                return rt.layer_runtimes[layer_idx as usize].current as u32;
            }
        }
    }
    0
}


