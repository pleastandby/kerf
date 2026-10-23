pub mod bezier;
pub mod geometry;
pub mod simd;

use std::ffi::CString;
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

