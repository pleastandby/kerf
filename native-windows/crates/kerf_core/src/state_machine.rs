// High-performance State Machine Runtime (states, layers, transitions, triggers, and listeners)
// Implements testdata/newreq.md Section 6 and Section 7

use std::collections::HashMap;
use crate::animation::{
    advance_instance, bezier_ease, blend_poses, sample_clip_to_pose, Clip, ClipInstance,
};

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum InputKind {
    Bool,
    Number,
    Trigger,
}

#[derive(Debug, Clone, PartialEq)]
pub struct Input {
    pub name: String,
    pub kind: InputKind,
    pub value: f32,
    pub fired: bool,
}

impl Input {
    pub fn bool_input(name: &str, initial: bool) -> Self {
        Self {
            name: name.to_string(),
            kind: InputKind::Bool,
            value: if initial { 1.0 } else { 0.0 },
            fired: false,
        }
    }

    pub fn number_input(name: &str, initial: f32) -> Self {
        Self {
            name: name.to_string(),
            kind: InputKind::Number,
            value: initial,
            fired: false,
        }
    }

    pub fn trigger_input(name: &str) -> Self {
        Self {
            name: name.to_string(),
            kind: InputKind::Trigger,
            value: 0.0,
            fired: false,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Op {
    Eq,
    Ne,
    Lt,
    Le,
    Gt,
    Ge,
}

#[derive(Debug, Clone, PartialEq)]
pub struct Condition {
    pub input_idx: usize,
    pub op: Op,
    pub value: f32,
}

impl Condition {
    pub fn new(input_idx: usize, op: Op, value: f32) -> Self {
        Self { input_idx, op, value }
    }

    pub fn is_true(&self, inputs: &[Input]) -> bool {
        if self.input_idx >= inputs.len() {
            return false;
        }
        let input = &inputs[self.input_idx];
        if input.kind == InputKind::Trigger {
            return input.fired;
        }

        let diff = input.value - self.value;
        const EPSILON: f32 = 1e-5;
        match self.op {
            Op::Eq => diff.abs() <= EPSILON,
            Op::Ne => diff.abs() > EPSILON,
            Op::Lt => input.value < self.value - EPSILON,
            Op::Le => input.value <= self.value + EPSILON,
            Op::Gt => input.value > self.value + EPSILON,
            Op::Ge => input.value >= self.value - EPSILON,
        }
    }
}

#[derive(Debug, Clone, PartialEq)]
pub enum StateKind {
    Entry,
    Exit,
    Any,
    Animation { clip: usize },
    Blend1D { input: usize, items: Vec<(f32, usize)> }, // (threshold, clip)
    BlendDirect { items: Vec<(usize, usize)> },         // (weight_input, clip)
}

#[derive(Debug, Clone, PartialEq)]
pub struct State {
    pub id: usize,
    pub name: String,
    pub kind: StateKind,
}

impl State {
    pub fn new(id: usize, name: &str, kind: StateKind) -> Self {
        Self {
            id,
            name: name.to_string(),
            kind,
        }
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct Transition {
    pub from: usize,
    pub to: usize,
    pub conditions: Vec<Condition>,
    pub duration: f32,          // seconds, 0 = instant
    pub exit_time: Option<f32>, // normalized [0, 1] of from-state duration
    pub interrupt: bool,        // may another transition cut in while this one mixes?
}

impl Transition {
    pub fn new(from: usize, to: usize, duration: f32) -> Self {
        Self {
            from,
            to,
            conditions: Vec::new(),
            duration: duration.max(0.0),
            exit_time: None,
            interrupt: true,
        }
    }

    pub fn with_condition(mut self, cond: Condition) -> Self {
        self.conditions.push(cond);
        self
    }

    pub fn with_exit_time(mut self, exit_time: f32) -> Self {
        self.exit_time = Some(exit_time);
        self
    }

    pub fn with_interrupt(mut self, interrupt: bool) -> Self {
        self.interrupt = interrupt;
        self
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct Layer {
    pub name: String,
    pub states: Vec<State>,
    pub transitions: Vec<Transition>,
}

impl Layer {
    pub fn new(name: &str) -> Self {
        Self {
            name: name.to_string(),
            states: Vec::new(),
            transitions: Vec::new(),
        }
    }

    pub fn add_state(&mut self, state: State) -> usize {
        let idx = self.states.len();
        self.states.push(state);
        idx
    }

    pub fn add_transition(&mut self, trans: Transition) {
        self.transitions.push(trans);
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum PointerEventKind {
    Down,
    Up,
    Move,
    Enter,
    Exit,
}

#[derive(Debug, Clone, PartialEq)]
pub enum ListenerAction {
    SetBool(usize, bool),
    SetNumber(usize, f32),
    Fire(usize),
}

#[derive(Debug, Clone, PartialEq)]
pub struct Listener {
    pub target_node: u32,
    pub kind: PointerEventKind,
    pub actions: Vec<ListenerAction>,
}

#[derive(Debug, Clone)]
pub struct ActiveTransition {
    pub def: Transition,
    pub to_state: usize,
    pub to_time: ClipInstance,
    pub elapsed: f32,
    pub duration: f32,
    // Mid-blend snapshot of pose when interrupted, eliminating visual pops
    pub snapshot_pose: Option<HashMap<(u32, u16), f32>>,
}

#[derive(Debug, Clone)]
pub struct LayerRuntime {
    pub current: usize,
    pub cur_time: ClipInstance,
    pub transition: Option<ActiveTransition>,
    pub finished: bool,
}

impl LayerRuntime {
    pub fn new(entry_state: usize) -> Self {
        Self {
            current: entry_state,
            cur_time: ClipInstance::new(0),
            transition: None,
            finished: false,
        }
    }
}

#[derive(Debug, Clone)]
pub struct StateMachine {
    pub name: String,
    pub inputs: Vec<Input>,
    pub layers: Vec<Layer>,
    pub listeners: Vec<Listener>,
    pub any_state_id: Option<usize>,
}

impl StateMachine {
    pub fn new(name: &str) -> Self {
        Self {
            name: name.to_string(),
            inputs: Vec::new(),
            layers: Vec::new(),
            listeners: Vec::new(),
            any_state_id: None,
        }
    }

    pub fn add_input(&mut self, input: Input) -> usize {
        let idx = self.inputs.len();
        self.inputs.push(input);
        idx
    }

    pub fn add_layer(&mut self, layer: Layer) -> usize {
        let idx = self.layers.len();
        self.layers.push(layer);
        idx
    }

    pub fn add_listener(&mut self, listener: Listener) {
        self.listeners.push(listener);
    }
}

#[derive(Debug, Clone)]
pub struct StateMachineRuntime {
    pub def: StateMachine,
    pub layer_runtimes: Vec<LayerRuntime>,
    pub reported_events: Vec<String>,
}

impl StateMachineRuntime {
    pub fn new(def: StateMachine) -> Self {
        let mut layer_runtimes = Vec::with_capacity(def.layers.len());
        for layer in &def.layers {
            // Find Entry state, or default to state 0
            let entry_idx = layer
                .states
                .iter()
                .position(|s| s.kind == StateKind::Entry)
                .unwrap_or(0);
            layer_runtimes.push(LayerRuntime::new(entry_idx));
        }

        Self {
            def,
            layer_runtimes,
            reported_events: Vec::new(),
        }
    }

    pub fn set_bool(&mut self, input_idx: usize, val: bool) {
        if input_idx < self.def.inputs.len() {
            self.def.inputs[input_idx].value = if val { 1.0 } else { 0.0 };
        }
    }

    pub fn set_number(&mut self, input_idx: usize, val: f32) {
        if input_idx < self.def.inputs.len() {
            self.def.inputs[input_idx].value = val;
        }
    }

    pub fn fire_trigger(&mut self, input_idx: usize) {
        if input_idx < self.def.inputs.len() {
            self.def.inputs[input_idx].fired = true;
        }
    }

    pub fn get_input_value(&self, input_idx: usize) -> f32 {
        if input_idx < self.def.inputs.len() {
            self.def.inputs[input_idx].value
        } else {
            0.0
        }
    }

    pub fn on_pointer_event(&mut self, target_node: u32, kind: PointerEventKind) {
        let mut actions_to_run = Vec::new();
        for listener in &self.def.listeners {
            if listener.target_node == target_node && listener.kind == kind {
                for action in &listener.actions {
                    actions_to_run.push(action.clone());
                }
            }
        }
        for action in actions_to_run {
            match action {
                ListenerAction::SetBool(i, v) => self.set_bool(i, v),
                ListenerAction::SetNumber(i, v) => self.set_number(i, v),
                ListenerAction::Fire(i) => self.fire_trigger(i),
            }
        }
    }

    fn normalized_progress(layer: &Layer, current_state_idx: usize, cur_time: f32, clips: &[Clip]) -> f32 {
        if current_state_idx >= layer.states.len() {
            return 1.0;
        }
        match &layer.states[current_state_idx].kind {
            StateKind::Animation { clip } => {
                if *clip < clips.len() {
                    let d = clips[*clip].duration;
                    if d > 0.0 { (cur_time / d).max(0.0) } else { 1.0 }
                } else {
                    1.0
                }
            }
            StateKind::Entry | StateKind::Exit | StateKind::Any => 1.0,
            StateKind::Blend1D { .. } | StateKind::BlendDirect { .. } => 1.0,
        }
    }

    fn find_valid_transition(
        layer: &Layer,
        current_state: usize,
        cur_time: f32,
        inputs: &[Input],
        clips: &[Clip],
        active_trans_to: Option<usize>,
    ) -> Option<Transition> {
        let any_state_idx = layer.states.iter().position(|s| s.kind == StateKind::Any);

        // Order: candidates from current state first, then Any state
        let mut candidates = Vec::new();
        for t in &layer.transitions {
            if t.from == current_state {
                candidates.push(t);
            }
        }
        if let Some(any_idx) = any_state_idx {
            for t in &layer.transitions {
                if t.from == any_idx {
                    candidates.push(t);
                }
            }
        }

        let progress = Self::normalized_progress(layer, current_state, cur_time, clips);

        for t in candidates {
            // Avoid self-restart loop if from Any to current
            if let Some(any_idx) = any_state_idx {
                if t.from == any_idx && t.to == current_state {
                    continue;
                }
            }

            // If currently transitioning, do not interrupt with the same target state
            if let Some(to_state) = active_trans_to {
                if t.to == to_state {
                    continue;
                }
            }

            // Check exit time
            if let Some(exit_time) = t.exit_time {
                if progress < exit_time {
                    continue;
                }
            }

            // Check conditions (all must match)
            let conditions_matched = t.conditions.iter().all(|c| c.is_true(inputs));
            if conditions_matched {
                return Some(t.clone());
            }
        }

        None
    }

    /// Evaluates the full state machine by dt, resolving transitions, sampling clips, and blending layers.
    /// Returns (needs_redraw, computed_pose).
    pub fn update(&mut self, dt: f32, clips: &[Clip]) -> (bool, HashMap<(u32, u16), f32>) {
        let mut needs_redraw = false;
        let mut final_pose = HashMap::new();

        for i in 0..self.def.layers.len() {
            let layer_def = &self.def.layers[i];
            let layer_rt = &mut self.layer_runtimes[i];

            if layer_rt.finished {
                continue;
            }

            // 1. Advance current state clip
            let current_clip_idx = match &layer_def.states[layer_rt.current].kind {
                StateKind::Animation { clip } => Some(*clip),
                _ => None,
            };
            if let Some(c_idx) = current_clip_idx {
                if c_idx < clips.len() {
                    let clip = &clips[c_idx];
                    advance_instance(&mut layer_rt.cur_time, dt, clip.duration, clip.loop_mode);
                    needs_redraw = true;
                }
            }

            // 2. Advance transition clip if active
            if let Some(ref mut active_trans) = layer_rt.transition {
                active_trans.elapsed += dt;
                needs_redraw = true;

                let to_clip_idx = match &layer_def.states[active_trans.to_state].kind {
                    StateKind::Animation { clip } => Some(*clip),
                    _ => None,
                };
                if let Some(t_idx) = to_clip_idx {
                    if t_idx < clips.len() {
                        let clip = &clips[t_idx];
                        advance_instance(&mut active_trans.to_time, dt, clip.duration, clip.loop_mode);
                    }
                }
            }

            // 3. Complete finished transition
            if let Some(ref trans) = layer_rt.transition {
                if trans.elapsed >= trans.duration {
                    let to_state = trans.to_state;
                    let to_time = trans.to_time.clone();
                    layer_rt.current = to_state;
                    layer_rt.cur_time = to_time;
                    layer_rt.transition = None;
                    if layer_def.states[layer_rt.current].kind == StateKind::Exit {
                        layer_rt.finished = true;
                    }
                }
            }

            // 4. Resolve transitions with zero-duration chain resolution & cycle guard (max 100 steps)
            const MAX_STEPS: usize = 100;
            for _step in 0..MAX_STEPS {
                if let Some(ref active_trans) = layer_rt.transition {
                    if !active_trans.def.interrupt {
                        // Current transition cannot be interrupted mid-blend
                        break;
                    }
                }

                let active_to = layer_rt.transition.as_ref().map(|tr| tr.to_state);
                let next_trans = Self::find_valid_transition(
                    layer_def,
                    layer_rt.current,
                    layer_rt.cur_time.time,
                    &self.def.inputs,
                    clips,
                    active_to,
                );

                if let Some(t) = next_trans {
                    // Do not interrupt if already blending to the exact same target state
                    if let Some(ref active_trans) = layer_rt.transition {
                        if active_trans.to_state == t.to {
                            break;
                        }
                    }

                    // Check if interrupting an existing blend: snapshot current pose to avoid pops
                    let snapshot = if let Some(ref cur_trans) = layer_rt.transition {
                        let mut from_pose = HashMap::new();
                        let mut to_pose = HashMap::new();
                        Self::sample_state(&layer_def.states[layer_rt.current], layer_rt.cur_time.time, clips, &self.def.inputs, &mut from_pose);
                        Self::sample_state(&layer_def.states[cur_trans.to_state], cur_trans.to_time.time, clips, &self.def.inputs, &mut to_pose);
                        let mix = (cur_trans.elapsed / cur_trans.duration.max(0.001)).max(0.0).min(1.0);
                        let mut blended = HashMap::new();
                        blend_poses(&from_pose, &to_pose, mix, &mut blended);
                        Some(blended)
                    } else {
                        None
                    };

                    let to_state = t.to;
                    let duration = t.duration;
                    let active_trans = ActiveTransition {
                        def: t,
                        to_state,
                        to_time: ClipInstance::new(to_state),
                        elapsed: 0.0,
                        duration,
                        snapshot_pose: snapshot,
                    };
                    layer_rt.transition = Some(active_trans);
                    needs_redraw = true;

                    if duration <= 0.0 {
                        // Instant transition: complete immediately and allow chain resolution
                        layer_rt.current = to_state;
                        layer_rt.cur_time = ClipInstance::new(to_state);
                        layer_rt.transition = None;
                        if layer_def.states[layer_rt.current].kind == StateKind::Exit {
                            layer_rt.finished = true;
                            break;
                        }
                    } else {
                        break;
                    }
                } else {
                    break;
                }
            }

            // 5. Sample poses and blend
            let mut layer_pose = HashMap::new();
            if let Some(ref trans) = layer_rt.transition {
                let mut from_pose = HashMap::new();
                if let Some(ref snap) = trans.snapshot_pose {
                    from_pose = snap.clone();
                } else {
                    Self::sample_state(
                        &layer_def.states[layer_rt.current],
                        layer_rt.cur_time.time,
                        clips,
                        &self.def.inputs,
                        &mut from_pose,
                    );
                }

                let mut to_pose = HashMap::new();
                Self::sample_state(
                    &layer_def.states[trans.to_state],
                    trans.to_time.time,
                    clips,
                    &self.def.inputs,
                    &mut to_pose,
                );

                let u = (trans.elapsed / trans.duration.max(0.001)).max(0.0).min(1.0);
                let mix = bezier_ease(u, 0.25, 0.1, 0.25, 1.0);
                blend_poses(&from_pose, &to_pose, mix, &mut layer_pose);
            } else {
                Self::sample_state(
                    &layer_def.states[layer_rt.current],
                    layer_rt.cur_time.time,
                    clips,
                    &self.def.inputs,
                    &mut layer_pose,
                );
            }

            // Merge into final output pose (later layers override earlier layers)
            for (key, val) in layer_pose {
                final_pose.insert(key, val);
            }
        }

        // 6. Clear triggers: triggers last exactly one update frame!
        for input in &mut self.def.inputs {
            if input.kind == InputKind::Trigger {
                input.fired = false;
            }
        }

        (needs_redraw, final_pose)
    }

    fn sample_state(
        state: &State,
        time: f32,
        clips: &[Clip],
        inputs: &[Input],
        out_pose: &mut HashMap<(u32, u16), f32>,
    ) {
        match &state.kind {
            StateKind::Animation { clip } => {
                if *clip < clips.len() {
                    sample_clip_to_pose(&clips[*clip], time, out_pose);
                }
            }
            StateKind::Blend1D { input, items } => {
                if items.is_empty() {
                    return;
                }
                let val = if *input < inputs.len() {
                    inputs[*input].value
                } else {
                    0.0
                };

                if val <= items[0].0 {
                    if items[0].1 < clips.len() {
                        sample_clip_to_pose(&clips[items[0].1], time, out_pose);
                    }
                    return;
                }
                if val >= items.last().unwrap().0 {
                    let last_clip = items.last().unwrap().1;
                    if last_clip < clips.len() {
                        sample_clip_to_pose(&clips[last_clip], time, out_pose);
                    }
                    return;
                }

                // Find two bounding thresholds
                for i in 0..(items.len() - 1) {
                    let a = &items[i];
                    let b = &items[i + 1];
                    if val >= a.0 && val <= b.0 {
                        let span = b.0 - a.0;
                        let w = if span > 1e-5 { (val - a.0) / span } else { 0.0 };
                        let mut pose_a = HashMap::new();
                        let mut pose_b = HashMap::new();
                        if a.1 < clips.len() {
                            sample_clip_to_pose(&clips[a.1], time, &mut pose_a);
                        }
                        if b.1 < clips.len() {
                            sample_clip_to_pose(&clips[b.1], time, &mut pose_b);
                        }
                        blend_poses(&pose_a, &pose_b, w, out_pose);
                        return;
                    }
                }
            }
            StateKind::BlendDirect { items } => {
                for (weight_input, clip_idx) in items {
                    let w = if *weight_input < inputs.len() {
                        inputs[*weight_input].value.max(0.0).min(1.0)
                    } else {
                        0.0
                    };
                    if w > 0.001 && *clip_idx < clips.len() {
                        let mut temp = HashMap::new();
                        sample_clip_to_pose(&clips[*clip_idx], time, &mut temp);
                        for (k, v) in temp {
                            let cur = out_pose.get(&k).copied().unwrap_or(0.0);
                            out_pose.insert(k, cur + v * w);
                        }
                    }
                }
            }
            _ => {}
        }
    }
}

// Prototype Link to State Machine Bridge (Section 7)
// Compiles frame-to-frame interactions into state machine states and animated tracks
pub fn smart_animate_frames(
    clip_id: u32,
    clip_name: &str,
    duration: f32,
    frame_a_props: &HashMap<(u32, u16), f32>,
    frame_b_props: &HashMap<(u32, u16), f32>,
    ease_x1: f32, ease_y1: f32, ease_x2: f32, ease_y2: f32,
) -> Clip {
    let mut clip = Clip::new(clip_id, clip_name, duration, 60.0, crate::animation::LoopMode::OneShot);

    for (&(node, prop), &val_a) in frame_a_props {
        let val_b = frame_b_props.get(&(node, prop)).copied().unwrap_or(val_a);
        if (val_b - val_a).abs() > 0.001 {
            let mut track = crate::animation::Track::new(node, prop);
            track.add_keyframe(crate::animation::Keyframe::cubic(0.0, val_a, ease_x1, ease_y1, ease_x2, ease_y2));
            track.add_keyframe(crate::animation::Keyframe::linear(duration, val_b));
            clip.add_track(track);
        }
    }

    // Properties only in frame B (e.g. fade in)
    for (&(node, prop), &val_b) in frame_b_props {
        if !frame_a_props.contains_key(&(node, prop)) {
            let mut track = crate::animation::Track::new(node, prop);
            track.add_keyframe(crate::animation::Keyframe::cubic(0.0, 0.0, ease_x1, ease_y1, ease_x2, ease_y2));
            track.add_keyframe(crate::animation::Keyframe::linear(duration, val_b));
            clip.add_track(track);
        }
    }

    clip
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::animation::{Clip, Keyframe, LoopMode, Track};

    #[test]
    fn test_state_machine_transition_and_trigger_reset() {
        let mut sm = StateMachine::new("ButtonSM");
        let trig_click = sm.add_input(Input::trigger_input("onClick"));
        let bool_hover = sm.add_input(Input::bool_input("isHover", false));

        let mut layer = Layer::new("Base");
        let idle = layer.add_state(State::new(0, "Idle", StateKind::Animation { clip: 0 }));
        let hover = layer.add_state(State::new(1, "Hover", StateKind::Animation { clip: 1 }));
        let pressed = layer.add_state(State::new(2, "Pressed", StateKind::Animation { clip: 2 }));

        // Transition: Idle -> Hover on isHover == true
        layer.add_transition(
            Transition::new(idle, hover, 0.2)
                .with_condition(Condition::new(bool_hover, Op::Eq, 1.0)),
        );

        // Transition: Hover -> Pressed on onClick Trigger
        layer.add_transition(
            Transition::new(hover, pressed, 0.1)
                .with_condition(Condition::new(trig_click, Op::Eq, 1.0)),
        );

        sm.add_layer(layer);

        let mut clips = vec![
            Clip::new(0, "IdleClip", 1.0, 60.0, LoopMode::Loop),
            Clip::new(1, "HoverClip", 1.0, 60.0, LoopMode::Loop),
            Clip::new(2, "PressedClip", 1.0, 60.0, LoopMode::OneShot),
        ];
        // Add a track to PressedClip: node 1, prop 5 (opacity)
        let mut t = Track::new(1, 5);
        t.add_keyframe(Keyframe::linear(0.0, 0.5));
        t.add_keyframe(Keyframe::linear(1.0, 1.0));
        clips[2].add_track(t);

        let mut runtime = StateMachineRuntime::new(sm);
        assert_eq!(runtime.layer_runtimes[0].current, idle);

        // 1. Advance in Idle
        runtime.update(0.016, &clips);
        assert_eq!(runtime.layer_runtimes[0].current, idle);

        // 2. Set isHover = true
        runtime.set_bool(bool_hover, true);
        runtime.update(0.016, &clips);
        assert!(runtime.layer_runtimes[0].transition.is_some());

        // Advance past transition duration
        runtime.update(0.25, &clips);
        assert_eq!(runtime.layer_runtimes[0].current, hover);
        assert!(runtime.layer_runtimes[0].transition.is_none());

        // 3. Fire trigger onClick
        runtime.fire_trigger(trig_click);
        assert!(runtime.def.inputs[trig_click].fired);

        runtime.update(0.016, &clips);
        // Trigger MUST be cleared immediately after update!
        assert!(!runtime.def.inputs[trig_click].fired);
        assert!(runtime.layer_runtimes[0].transition.is_some());

        // Advance to complete transition to Pressed
        runtime.update(0.15, &clips);
        assert_eq!(runtime.layer_runtimes[0].current, pressed);
    }

    #[test]
    fn test_zero_duration_chain_and_cycle_guard() {
        let mut sm = StateMachine::new("ChainSM");
        let mut layer = Layer::new("Base");
        let s0 = layer.add_state(State::new(0, "S0", StateKind::Entry));
        let s1 = layer.add_state(State::new(1, "S1", StateKind::Animation { clip: 0 }));
        let s2 = layer.add_state(State::new(2, "S2", StateKind::Animation { clip: 0 }));

        // Instant transitions: S0 -> S1 -> S2 (duration = 0)
        layer.add_transition(Transition::new(s0, s1, 0.0));
        layer.add_transition(Transition::new(s1, s2, 0.0));
        sm.add_layer(layer);

        let clips = vec![Clip::new(0, "Dummy", 1.0, 60.0, LoopMode::Loop)];
        let mut runtime = StateMachineRuntime::new(sm);

        // Single frame update should traverse instant chain S0 -> S1 -> S2
        runtime.update(0.016, &clips);
        assert_eq!(runtime.layer_runtimes[0].current, s2);
    }
}
