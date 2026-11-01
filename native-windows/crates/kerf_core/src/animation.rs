// High-performance animation engine (keyframes, clips, tracks, and easing)
// Implements testdata/newreq.md Section 5

use std::collections::HashMap;

#[derive(Debug, Clone, Copy, PartialEq)]
pub enum Interp {
    Hold,
    Linear,
    Cubic { x1: f32, y1: f32, x2: f32, y2: f32 },
}

#[derive(Debug, Clone, PartialEq)]
pub struct Keyframe {
    pub time: f32,
    pub value: f32,
    pub interp: Interp,
}

impl Keyframe {
    pub fn new(time: f32, value: f32, interp: Interp) -> Self {
        Self { time, value, interp }
    }

    pub fn hold(time: f32, value: f32) -> Self {
        Self::new(time, value, Interp::Hold)
    }

    pub fn linear(time: f32, value: f32) -> Self {
        Self::new(time, value, Interp::Linear)
    }

    pub fn cubic(time: f32, value: f32, x1: f32, y1: f32, x2: f32, y2: f32) -> Self {
        Self::new(time, value, Interp::Cubic { x1, y1, x2, y2 })
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct Track {
    pub node: u32,
    pub prop: u16,
    pub keys: Vec<Keyframe>,
}

impl Track {
    pub fn new(node: u32, prop: u16) -> Self {
        Self {
            node,
            prop,
            keys: Vec::new(),
        }
    }

    pub fn add_keyframe(&mut self, kf: Keyframe) {
        let idx = match self.keys.binary_search_by(|k| k.time.partial_cmp(&kf.time).unwrap_or(std::cmp::Ordering::Equal)) {
            Ok(i) => i,
            Err(i) => i,
        };
        self.keys.insert(idx, kf);
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum LoopMode {
    OneShot,
    Loop,
    PingPong,
}

#[derive(Debug, Clone, PartialEq)]
pub struct Clip {
    pub id: u32,
    pub name: String,
    pub duration: f32,
    pub fps: f32,
    pub loop_mode: LoopMode,
    pub tracks: Vec<Track>,
}

impl Clip {
    pub fn new(id: u32, name: &str, duration: f32, fps: f32, loop_mode: LoopMode) -> Self {
        Self {
            id,
            name: name.to_string(),
            duration: duration.max(0.001),
            fps: fps.max(1.0),
            loop_mode,
            tracks: Vec::new(),
        }
    }

    pub fn add_track(&mut self, track: Track) {
        self.tracks.push(track);
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct ClipInstance {
    pub clip_idx: usize,
    pub time: f32,
    pub speed: f32,
    pub direction: i8, // +1 or -1
}

impl ClipInstance {
    pub fn new(clip_idx: usize) -> Self {
        Self {
            clip_idx,
            time: 0.0,
            speed: 1.0,
            direction: 1,
        }
    }

    pub fn with_speed(mut self, speed: f32) -> Self {
        self.speed = speed;
        self
    }
}

// Cubic Bézier calculation: x(s) = 3(1-s)^2 s x1 + 3(1-s) s^2 x2 + s^3
#[inline]
pub fn cubic_bezier_eval(t: f32, p1: f32, p2: f32) -> f32 {
    let one_minus_t = 1.0 - t;
    3.0 * one_minus_t * one_minus_t * t * p1 + 3.0 * one_minus_t * t * t * p2 + t * t * t
}

#[inline]
pub fn cubic_bezier_derivative(t: f32, p1: f32, p2: f32) -> f32 {
    let one_minus_t = 1.0 - t;
    3.0 * one_minus_t * one_minus_t * p1
        + 6.0 * one_minus_t * t * (p2 - p1)
        + 3.0 * t * t * (1.0 - p2)
}

/// Solves x(s) = u for s in [0, 1] using Newton-Raphson with bisection fallback, then returns y(s).
pub fn bezier_ease(u: f32, x1: f32, y1: f32, x2: f32, y2: f32) -> f32 {
    if u <= 0.0 {
        return 0.0;
    }
    if u >= 1.0 {
        return 1.0;
    }

    // Solve x(s) = u for s in [0, 1] using 8 Newton steps
    let mut s = u;
    for _ in 0..8 {
        let x_err = cubic_bezier_eval(s, x1, x2) - u;
        if x_err.abs() < 1e-6 {
            return cubic_bezier_eval(s, y1, y2);
        }
        let d = cubic_bezier_derivative(s, x1, x2);
        if d.abs() < 1e-6 {
            break;
        }
        s -= x_err / d;
        if s < 0.0 || s > 1.0 {
            break;
        }
    }

    // Fallback: bisection for 20 iterations
    let mut low = 0.0f32;
    let mut high = 1.0f32;
    s = u;
    for _ in 0..20 {
        let x_val = cubic_bezier_eval(s, x1, x2);
        if (x_val - u).abs() < 1e-6 {
            break;
        }
        if x_val < u {
            low = s;
        } else {
            high = s;
        }
        s = 0.5 * (low + high);
    }

    cubic_bezier_eval(s, y1, y2)
}

/// Advances a clip instance by dt seconds according to its loop mode. Returns true if completed or wrapped.
pub fn advance_instance(inst: &mut ClipInstance, dt: f32, duration: f32, loop_mode: LoopMode) -> bool {
    if duration <= 0.0 {
        inst.time = 0.0;
        return true;
    }

    inst.time += dt * inst.speed * (inst.direction as f32);
    let mut completed = false;

    match loop_mode {
        LoopMode::OneShot => {
            if inst.direction >= 0 && inst.time >= duration {
                inst.time = duration;
                completed = true;
            } else if inst.direction < 0 && inst.time <= 0.0 {
                inst.time = 0.0;
                completed = true;
            }
        }
        LoopMode::Loop => {
            while inst.time >= duration {
                inst.time -= duration;
                completed = true;
            }
            while inst.time < 0.0 {
                inst.time += duration;
                completed = true;
            }
        }
        LoopMode::PingPong => {
            while inst.time > duration || inst.time < 0.0 {
                if inst.time > duration {
                    inst.time = 2.0 * duration - inst.time;
                    inst.direction = -1;
                    completed = true;
                }
                if inst.time < 0.0 {
                    inst.time = -inst.time;
                    inst.direction = 1;
                    completed = true;
                }
            }
        }
    }

    completed
}

/// Samples a single track at time t using binary search and keyframe interpolation.
pub fn sample_track(track: &Track, t: f32) -> f32 {
    if track.keys.is_empty() {
        return 0.0;
    }
    if t <= track.keys[0].time {
        return track.keys[0].value;
    }
    if t >= track.keys.last().unwrap().time {
        return track.keys.last().unwrap().value;
    }

    // Binary search for index i such that keys[i].time <= t < keys[i + 1].time
    let keys = &track.keys;
    let mut low = 0;
    let mut high = keys.len() - 1;
    let mut idx = 0;

    while low <= high {
        let mid = (low + high) / 2;
        if keys[mid].time <= t {
            idx = mid;
            low = mid + 1;
        } else {
            if mid == 0 {
                break;
            }
            high = mid - 1;
        }
    }

    if idx >= keys.len() - 1 {
        return keys.last().unwrap().value;
    }

    let a = &keys[idx];
    let b = &keys[idx + 1];
    let span = b.time - a.time;
    if span <= 1e-6 {
        return a.value;
    }

    let u = (t - a.time) / span;
    match a.interp {
        Interp::Hold => a.value,
        Interp::Linear => a.value + (b.value - a.value) * u,
        Interp::Cubic { x1, y1, x2, y2 } => {
            let eased = bezier_ease(u, x1, y1, x2, y2);
            a.value + (b.value - a.value) * eased
        }
    }
}

/// Samples all tracks in a clip at time t and writes into an output pose map: (node_id, prop_key) -> value.
pub fn sample_clip_to_pose(clip: &Clip, t: f32, out_pose: &mut HashMap<(u32, u16), f32>) {
    for track in &clip.tracks {
        let val = sample_track(track, t);
        out_pose.insert((track.node, track.prop), val);
    }
}

/// Blends source pose into target pose with weight mix in [0, 1].
pub fn blend_poses(
    base_pose: &HashMap<(u32, u16), f32>,
    target_pose: &HashMap<(u32, u16), f32>,
    mix: f32,
    out_pose: &mut HashMap<(u32, u16), f32>,
) {
    let clamped_mix = mix.max(0.0).min(1.0);
    // Keys from both poses
    for (&key, &target_val) in target_pose {
        let base_val = base_pose.get(&key).copied().unwrap_or(target_val);
        let blended = base_val + (target_val - base_val) * clamped_mix;
        out_pose.insert(key, blended);
    }
    for (&key, &base_val) in base_pose {
        if !target_pose.contains_key(&key) {
            out_pose.insert(key, base_val);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_bezier_ease_extremes() {
        assert_eq!(bezier_ease(0.0, 0.42, 0.0, 0.58, 1.0), 0.0);
        assert_eq!(bezier_ease(1.0, 0.42, 0.0, 0.58, 1.0), 1.0);
        // Linear ease (x1=0, y1=0, x2=1, y2=1)
        let mid = bezier_ease(0.5, 0.0, 0.0, 1.0, 1.0);
        assert!((mid - 0.5).abs() < 1e-4);
    }

    #[test]
    fn test_track_sampling_interpolation() {
        let mut track = Track::new(1, 10);
        track.add_keyframe(Keyframe::linear(0.0, 100.0));
        track.add_keyframe(Keyframe::cubic(1.0, 200.0, 0.25, 0.1, 0.25, 1.0));
        track.add_keyframe(Keyframe::hold(2.0, 300.0));
        track.add_keyframe(Keyframe::linear(3.0, 400.0));

        // Sample before start
        assert_eq!(sample_track(&track, -1.0), 100.0);
        // Sample linear midpoint
        assert_eq!(sample_track(&track, 0.5), 150.0);
        // Sample exact key
        assert_eq!(sample_track(&track, 1.0), 200.0);
        // Sample hold range [2.0, 3.0) -> holds 300.0
        assert_eq!(sample_track(&track, 2.5), 300.0);
        // Sample after end
        assert_eq!(sample_track(&track, 4.0), 400.0);
    }

    #[test]
    fn test_advance_instance_loop_modes() {
        let mut inst = ClipInstance::new(0);
        let duration = 2.0;

        // OneShot
        advance_instance(&mut inst, 2.5, duration, LoopMode::OneShot);
        assert_eq!(inst.time, 2.0);

        // Loop
        inst.time = 0.0;
        advance_instance(&mut inst, 2.5, duration, LoopMode::Loop);
        assert!((inst.time - 0.5).abs() < 1e-5);

        // PingPong
        inst.time = 0.0;
        inst.direction = 1;
        advance_instance(&mut inst, 2.5, duration, LoopMode::PingPong);
        assert_eq!(inst.direction, -1);
        assert!((inst.time - 1.5).abs() < 1e-5);
    }
}

