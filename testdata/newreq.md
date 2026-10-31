# Figma + Rive Desktop App: Engine Spec

Target stack: **Rust core** (scene graph, layout, animation, state machine, rendering) + **C# shell** (WPF or Avalonia UI, tools, panels, file dialogs).

Contents
1. Architecture
2. Agent research checklist (what to look for)
3. Core data model
4. Auto layout algorithm (Figma-style)
5. Animation system algorithm (Rive-style)
6. State machine algorithm (Rive-style)
7. Prototype links to state machine bridge
8. Rust to C# interop
9. Milestones and tests

---

## 1. Architecture

```
C# shell (UI, tools, inspector, timeline panel, state machine graph editor)
        |  C ABI (handles + command buffer + event queue)
Rust core
  |- scene     : node tree, properties, ids, undo/redo (command log)
  |- layout    : auto layout + constraints
  |- anim      : clips, tracks, keyframes, blending
  |- sm        : state machine runtime
  |- render    : vector tessellation -> GPU (wgpu / vello / Skia via skia-safe)
  |- io        : own file format, import/export
```

Rules:
- One source of truth: the Rust scene. C# holds only handles and view state.
- Edits are **commands** (undoable, serializable, replayable). Animation recording and collaboration both build on this.
- The runtime (layout + anim + sm + render) must work **without** the editor so the same crate can ship in games and apps.

---

## 2. Agent research checklist

### 2.1 Ground rules for the agent

- Prefer **public and licensed sources first**: official docs, the open-source Rive runtimes (MIT-licensed), Figma's public REST/Plugin API docs, published file-format notes, and black-box behavior observed by using the apps normally.
- Do **not** copy proprietary code or assets. Reverse engineering a proprietary app can breach its terms of service or license, so check the license or terms before analyzing any shipped binary. Where a target is not clearly permitted, stop and ask the user.
- Use the uploaded REA skill only on targets you are allowed to analyze (open-source runtimes, your own builds, artifacts you have rights to).
- For every finding, record: **Observation** (what evidence shows), **Inference** (what you conclude), **Unknown** (what is not established), and the **evidence ID or source URL**. Never claim static analysis showed runtime behavior.

### 2.2 Finding ledger template

| Feature | Question | Source type | Observation | Inference | Unknown | Evidence ID / URL |
|---|---|---|---|---|---|---|

### 2.3 What to look for

**A. Design editor (Figma-like)**

| # | Feature | What to find out |
|---|---|---|
| A1 | Scene graph | Node types (frame, group, vector, text, component, instance, boolean op), id scheme, parent/child ordering |
| A2 | Auto layout | Direction, wrap, padding, gap, space-between, hug/fill/fixed, min/max, absolute-position children, alignment, baseline, z-order reversal |
| A3 | Constraints | Left/right/center/scale/stretch behavior on resize for non-auto-layout frames |
| A4 | Vector model | Paths vs vector networks, fills, strokes (align, join, cap, dash), corner smoothing |
| A5 | Boolean ops | Union/subtract/intersect/exclude implementation and path cleanup |
| A6 | Text | Shaping, font fallback, variable fonts, text auto-resize modes, truncation |
| A7 | Components | Master/instance model, overrides, variants and properties, nested instances, swap |
| A8 | Styles and variables | Color/number/string/boolean variables, modes, aliasing, resolution order |
| A9 | Effects | Shadows, blurs (layer vs background), blend modes, masks |
| A10 | Interaction | Selection, snapping, smart guides, multi-select transforms, nudge, rulers, grid |
| A11 | Undo/redo and collaboration | Operation model, conflict resolution (CRDT or OT style), multiplayer cursors |
| A12 | Import/export | Formats (SVG, PNG, PDF), export scales, slices, file format versioning |
| A13 | Prototyping | Triggers, actions, overlays, smart animate matching, easing presets |
| A14 | Plugin and API surface | Extension API shape, sandboxing, permissions |

**B. Animation and state machine (Rive-like)**

| # | Feature | What to find out |
|---|---|---|
| B1 | File format | Object types, property keys, versioning, how unknown properties are skipped (forward compatibility) |
| B2 | Artboards | Size, clip, origin, nested artboards, instancing |
| B3 | Timeline | Frame rate, work area, loop modes (one-shot, loop, ping-pong), keyframe interpolation types |
| B4 | Property animation | How a track addresses an object + property, additive vs absolute |
| B5 | Easing | Cubic bezier solver, presets, hold keys |
| B6 | Skeletal | Bones, IK, skinning, mesh deformation, weights, constraints (translation/rotation/scale/distance) |
| B7 | State machine | Layers, entry/exit/any states, animation states, blend states (1D, direct), transitions, conditions, exit-time, duration, interrupt rules |
| B8 | Inputs | Boolean, number, trigger semantics (when triggers reset) |
| B9 | Listeners | Pointer enter/exit/down/up/move, hit testing, event actions that set inputs |
| B10 | Events | Reported events, audio events, callbacks to the host app |
| B11 | Data binding | View models, bound properties, converters, lists |
| B12 | Scripting | Language, sandbox, per-frame hooks |
| B13 | Runtime | Advance loop, dirty flags, draw ordering, renderer abstraction, platform runtimes |
| B14 | Performance | Tessellation caching, atlas, batching, what is re-done per frame |

**C. Integration points (your app's unique value)**

| # | Question |
|---|---|
| C1 | How does auto layout re-solve while an animation changes size or padding? (see 4.6) |
| C2 | How does a prototype link map onto a state machine transition? (see 7) |
| C3 | How are components and variants animated (variant switch as a state)? |
| C4 | How do design variables feed state machine inputs and vice versa? |
| C5 | What is the export package: scene + clips + state machines in one file? |

### 2.4 Suggested agent procedure

1. Turn A, B, C into a checklist of questions.
2. For each, check public docs and open-source code first; record evidence.
3. For behavior not documented, write a small black-box test (build a file in the real tool, record the result) and treat the result as an Observation.
4. Mark each item **answered / partially answered / unresolved** before moving on.
5. Produce a final report listing unresolved items so they can be decided as design choices, not assumed facts.

---

## 3. Core data model (Rust)

```rust
pub type NodeId = u32;
pub type PropKey = u16;           // stable numeric ids, like a binary format

pub enum Value { F32(f32), Vec2([f32;2]), Color([f32;4]), Bool(bool), Str(StrId) }

pub struct Node {
    pub id: NodeId,
    pub parent: Option<NodeId>,
    pub children: Vec<NodeId>,
    pub kind: NodeKind,           // Frame, Group, Path, Text, Instance, ...
    pub props: PropMap,           // x, y, w, h, rotation, opacity, fills, ...
    pub layout: LayoutProps,
    pub dirty: DirtyFlags,        // LAYOUT | TRANSFORM | PAINT
}

pub struct Document {
    pub nodes: Arena<Node>,
    pub clips: Vec<Clip>,
    pub machines: Vec<StateMachine>,
}
```

Dirty flags propagate upward for layout (a child size change dirties the parent) and downward for transform.

---

## 4. Auto layout algorithm (Figma-style)

### 4.1 Properties

```rust
pub enum Axis { None, Horizontal, Vertical }
pub enum Sizing { Fixed, Hug, Fill }
pub enum PrimaryAlign { Start, Center, End, SpaceBetween }
pub enum CounterAlign { Start, Center, End, Baseline, Stretch }

pub struct LayoutProps {
    pub axis: Axis,
    pub wrap: bool,
    pub padding: [f32; 4],        // left, top, right, bottom
    pub gap: f32,                 // primary-axis gap
    pub counter_gap: f32,         // gap between wrapped lines
    pub primary_align: PrimaryAlign,
    pub counter_align: CounterAlign,
    pub w_sizing: Sizing,
    pub h_sizing: Sizing,
    pub min: [f32; 2], pub max: [f32; 2],
    pub absolute: bool,           // ignore parent's auto layout
    pub reverse_z: bool,
}
```

### 4.2 Two passes

- **Pass 1, measure (bottom-up):** compute each node's intrinsic size. `Fixed` returns its own size, `Hug` sums or maxes its children, `Fill` reports 0 intrinsic (it takes leftover space).
- **Pass 2, arrange (top-down):** the parent knows its final size, distributes space, and positions children.

### 4.3 Measure (Hug)

```text
measure(node):
  for child in node.children where !child.absolute: measure(child)
  if node.axis == None: return node.size (Fixed) or union bounds (Hug)

  main    = axis primary, cross = axis counter
  content_main  = sum(child.main_size for non-fill children)
                  + gap * (count - 1)
  content_cross = max(child.cross_size for non-fill-cross children)

  if node.sizing[main] == Hug:  node.main  = content_main  + padding_main
  if node.sizing[cross] == Hug: node.cross = content_cross + padding_cross
  clamp(node.size, node.min, node.max)
```

### 4.4 Arrange (Fill distribution, no wrap)

```text
arrange(node):                       // node.size is final here
  inner_main  = node.main  - padding_main
  inner_cross = node.cross - padding_cross
  items = children where !absolute

  fixed_total = sum(item.main for item where sizing[main] != Fill)
  gaps        = gap * (len(items) - 1)      // 0 if SpaceBetween (see below)
  free        = inner_main - fixed_total - gaps

  // distribute free space equally among Fill items, honoring min/max
  fills = items where sizing[main] == Fill
  loop:
     share = free / len(unfrozen fills)
     violated = false
     for f in unfrozen fills:
        v = clamp(share, f.min_main, f.max_main)
        if v != share: freeze f at v; free -= v; violated = true
     if !violated: break
  assign share to remaining unfrozen fills

  // primary-axis alignment
  used = sum(item.main) + gaps
  leftover = inner_main - used
  match primary_align:
     Start        => cursor = padding_start
     Center       => cursor = padding_start + leftover / 2
     End          => cursor = padding_start + leftover
     SpaceBetween => cursor = padding_start; gap = leftover / (n - 1) + gap_min

  for item in items (reverse if reverse_z only affects paint order, not position):
     item.cross = if sizing[cross] == Fill || counter_align == Stretch
                  then inner_cross else item.cross
     cross_pos = match counter_align:
         Start  => padding_cross_start
         Center => padding_cross_start + (inner_cross - item.cross) / 2
         End    => padding_cross_start + inner_cross - item.cross
         Baseline => align first text baselines to max baseline
     item.pos = (cursor, cross_pos) mapped to (x,y) by axis
     cursor += item.main + gap
     arrange(item)                   // recurse into child

  for item in children where absolute:
     position by constraints (section 4.5)
```

### 4.5 Wrap

When `wrap == true` and the primary axis is a fixed or fill size:
1. Break items into lines: add items while `line_main + gap + item.main <= inner_main`, otherwise start a new line.
2. Each line is arranged with the same primary-axis rules above.
3. Line cross size = max item cross size in that line; lines are stacked with `counter_gap`.
4. If cross sizing is `Hug`, the container's cross size = sum of line cross sizes + counter gaps + padding.

### 4.6 Constraints for non-auto-layout children (absolute)

```text
on parent resize (dx, dy):
  horizontal: Left   => x unchanged
              Right  => x += dx
              Center => x += dx / 2
              LeftRight (stretch) => w += dx
              Scale  => x, w scaled by parent_w_new / parent_w_old
  (same for vertical)
```

### 4.7 Interaction with animation

Animated properties can change size, padding, or gap. Order each frame:

1. Advance animations and state machines (writes property values, marks `LAYOUT` dirty).
2. Run layout **only on dirty subtrees** (find topmost dirty ancestor whose size is not Fixed, re-measure it, re-arrange down).
3. Apply transform-only animations (rotation, opacity) **after** layout so they never trigger a re-layout.
4. Render.

Guard against infinite loops: a node's layout can run at most once per frame.

Optional: use the `taffy` crate (flexbox/grid in Rust) as the solver and map `LayoutProps` onto it. Write your own only if you need exact Figma behavior (hug/fill semantics, baseline) that taffy does not reproduce.

---

## 5. Animation system algorithm (Rive-style)

### 5.1 Data

```rust
pub enum Interp { Hold, Linear, Cubic { x1: f32, y1: f32, x2: f32, y2: f32 } }

pub struct Keyframe { pub time: f32, pub value: f32, pub interp: Interp } // interp: this key -> next key

pub struct Track {
    pub node: NodeId,
    pub prop: PropKey,
    pub keys: Vec<Keyframe>,      // sorted by time
}

pub enum LoopMode { OneShot, Loop, PingPong }

pub struct Clip {
    pub duration: f32,            // seconds
    pub fps: f32,
    pub loop_mode: LoopMode,
    pub tracks: Vec<Track>,
}

pub struct ClipInstance { pub clip: usize, pub time: f32, pub speed: f32, pub direction: i8 }
```

### 5.2 Advance time

```text
advance(inst, dt):
  inst.time += dt * inst.speed * inst.direction
  match loop_mode:
    OneShot:  inst.time = clamp(inst.time, 0, duration); done = (time hits end)
    Loop:     inst.time = wrap(inst.time, duration)
    PingPong: if time > duration: time = 2*duration - time; direction = -1
              if time < 0:        time = -time;               direction = +1
```

Fire any keyframe events whose time falls between the previous and new time (handle the wrap case by splitting the range into two segments).

### 5.3 Sample a track

```text
sample(track, t):
  if t <= keys[0].time: return keys[0].value
  if t >= keys.last.time: return keys.last.value
  i = binary_search(keys, t)            // keys[i].time <= t < keys[i+1].time
  a, b = keys[i], keys[i+1]
  u = (t - a.time) / (b.time - a.time)
  match a.interp:
    Hold   => a.value
    Linear => lerp(a.value, b.value, u)
    Cubic  => lerp(a.value, b.value, bezier_ease(u, x1,y1,x2,y2))
```

Cubic bezier easing (`x(s)` is monotonic for valid control points):

```text
bezier_ease(u, x1,y1,x2,y2):
  // solve x(s) = u for s in [0,1]
  s = u
  repeat 8: Newton step  s -= (x(s) - u) / dx(s)   (stop if |dx| < 1e-6)
  if not converged: bisection on [0,1] for 20 iterations
  return y(s)
  where x(s) = 3(1-s)^2 s x1 + 3(1-s) s^2 x2 + s^3, same form for y
```

Colors: interpolate in linear RGB or OKLab (not sRGB) for correct blends. Rotation: interpolate as a float, not as a quaternion, unless you add 3D.

### 5.4 Apply with blending

Each layer or state produces values with a **mix** weight in `[0,1]`.

```text
apply(inst, mix):
  for track in clip(inst).tracks:
     v = sample(track, inst.time)
     cur = node.get(track.prop)
     node.set(track.prop, lerp(cur, v, mix))      // layer order = later layers win
     mark_dirty(node, track.prop)
```

Additive tracks (offsets on top of a base pose) use `node.set(prop, cur + mix * (v - rest_value))`.

Frame order:
1. Reset animated properties to their **rest values** (or keep a base snapshot).
2. Apply layers in order, bottom to top.
3. Run constraints and bones (solve IK after animation, before layout/render).
4. Layout, then render.

### 5.5 Blend states

**1D blend** (input `x`): sort children by threshold. Find the two neighbors `a`, `b` with `a.t <= x <= b.t`, `w = (x - a.t)/(b.t - a.t)`; apply `a` at `mix*(1-w)` and `b` at `mix*w`.

**Direct blend:** each child has its own weight input; apply every child at `mix * weight_i`.

Keep child clip times in sync by normalizing: each child advances by `dt * speed`, or all share one normalized time if the clips are loop-aligned.

---

## 6. State machine algorithm (Rive-style)

### 6.1 Data

```rust
pub enum InputKind { Bool, Number, Trigger }
pub struct Input { pub name: String, pub kind: InputKind, pub value: f32 /* trigger: 0/1 */ }

pub enum Op { Eq, Ne, Lt, Le, Gt, Ge }
pub struct Condition { pub input: usize, pub op: Op, pub value: f32 }   // Trigger: fired == true

pub enum StateKind {
    Entry, Exit, Any,
    Animation { clip: usize },
    Blend1D   { input: usize, items: Vec<(f32 /*threshold*/, usize /*clip*/)> },
    BlendDirect { items: Vec<(usize /*weight input*/, usize /*clip*/)> },
}

pub struct Transition {
    pub from: usize, pub to: usize,
    pub conditions: Vec<Condition>,       // AND
    pub duration: f32,                    // seconds, 0 = instant
    pub exit_time: Option<f32>,           // 0..1 normalized of from-state, or seconds
    pub interrupt: bool,                  // may another transition cut in while this one mixes?
}

pub struct Layer { pub states: Vec<State>, pub transitions: Vec<Transition> }
pub struct StateMachine { pub inputs: Vec<Input>, pub layers: Vec<Layer>, pub listeners: Vec<Listener> }
```

### 6.2 Runtime state (per layer)

```rust
pub struct LayerRuntime {
    pub current: usize,                 // state index
    pub cur_time: ClipInstance,
    pub transition: Option<ActiveTransition>,   // {to, to_time, elapsed, duration, def}
}
```

### 6.3 Per-frame update

```text
update(machine, dt):
  1. host_input_events -> apply to inputs (set bool/number, fire trigger)
  2. pointer events    -> listeners -> may set inputs (section 6.5)

  for each layer:
    advance(layer.current clip, dt)
    if layer.transition: advance(to clip, dt); elapsed += dt

    // resolve zero-duration chains
    for step in 0..MAX_STEPS (e.g. 100):
        if layer.transition exists and not (transition.def.interrupt):
              break                              // can't be interrupted
        t = first_valid_transition(layer)
        if t is None: break
        start_transition(layer, t)
        if t.duration > 0: break
        complete_transition(layer)               // instant: loop again
    if step == MAX_STEPS: log "transition cycle" and stop

    // finish blend
    if layer.transition and elapsed >= duration:
        complete_transition(layer)

    // apply poses
    if layer.transition:
        mix = ease(elapsed / duration)
        apply(layer.current, mix = 1 - mix)
        apply(transition.to, mix = mix)
    else:
        apply(layer.current, mix = 1)

  3. clear all trigger inputs (they last exactly one update)
  4. layout -> render
  return needs_redraw = any clip playing || any dirty
```

### 6.4 Transition selection

```text
first_valid_transition(layer):
  candidates = transitions from layer.current  (editor order)
             + transitions from Any state      (checked after state-specific ones)
  for t in candidates:
     if t.to == layer.current and t.from == Any: skip     // avoid self-restart loop
     if !all(condition_true(c) for c in t.conditions): continue
     if t.exit_time is Some(e):
         if normalized_progress(layer.current) < e: continue
     return t
  return None

condition_true(c):
  v = inputs[c.input].value
  match c.op: Eq: v == c.value, Ne: v != c.value, Lt: v < c.value, ...
  // Trigger: true if it fired this frame
```

Rules to make this deterministic:
- Candidates are ordered; the **first** valid wins.
- Conditions are AND-ed; use multiple transitions for OR.
- A transition with no conditions and an `exit_time` plays the state once and moves on.
- Compare floats with an epsilon for `Eq`.

### 6.5 Start and complete

```text
start_transition(layer, t):
  layer.transition = { def: t, to: t.to, to_time: new ClipInstance(0), elapsed: 0, duration: t.duration }

complete_transition(layer):
  layer.current   = layer.transition.to
  layer.cur_time  = layer.transition.to_time
  layer.transition = None
  if layer.current is Exit: layer.finished = true
```

If a transition is interrupted mid-blend, snapshot the **current blended pose** as the new "from" so there is no visual pop (freeze values into a temporary pose and blend from it).

### 6.6 Listeners (pointer to inputs)

```text
on_pointer(event):
  hit = hit_test(artboard, event.pos)         // reverse paint order, respect clips and layout bounds
  for l in listeners where l.target == hit and l.kind == event.kind:
      for action in l.actions:
          SetBool(i, v) | SetNumber(i, v) | Fire(i)
  track enter/exit by comparing hit vs previous_hit
```

### 6.7 Host API (what the C# app calls)

```text
sm.set_bool("isHover", true)
sm.set_number("progress", 0.7)
sm.fire("click")
sm.advance(dt)               // returns redraw flag
sm.poll_events()             // reported events for the host
```

---

## 7. Prototype links to state machine bridge

Figma-style prototypes (frame A to frame B on click) can be compiled to a state machine:

| Prototype concept | State machine concept |
|---|---|
| Frame | State (or Variant of a component) |
| Trigger (click, hover, after delay) | Listener firing a Trigger input (delay = timed transition) |
| Action: navigate | Transition `from -> to` with the trigger condition |
| Smart animate + duration + easing | Transition `duration` + easing; auto-generated tracks |
| Overlay | Nested artboard shown by a Bool input |

**Smart animate matching:**

```text
smart_animate(frame_a, frame_b):
  match nodes by (1) same name and same path in tree, else (2) same component id
  for each matched pair, for each property that differs:
      create track [0 -> duration]: value_a -> value_b with transition easing
  unmatched in A only: fade out; in B only: fade in
  run layout on B first to get target sizes/positions, then animate toward them
```

---

## 8. Rust to C# interop

- Expose a **C ABI** from Rust (`#[no_mangle] extern "C"`), generate C# bindings with `csbindgen` or `cbindgen` + `P/Invoke`.
- Pass **handles** (opaque `u64` ids), never raw pointers to Rust structs.
- Use a **command buffer**: C# batches edits (set property, create node, move) into one byte buffer per frame, Rust applies them in a single call.
- Use an **event queue** for Rust to C#: Rust pushes events, C# polls once per frame.
- Rendering: either Rust renders into a shared GPU texture that the C# UI composites, or Rust renders to a window handle (HWND). Avoid copying pixel buffers across the boundary per frame.
- Strings cross as UTF-8 pointer + length; Rust owns and frees its memory (provide `free_*` functions).

```rust
#[no_mangle]
pub extern "C" fn sm_set_bool(doc: u64, machine: u32, name: *const u8, len: usize, v: bool) -> i32 { /* ... */ 0 }

#[no_mangle]
pub extern "C" fn doc_advance(doc: u64, dt: f32) -> u32 /* redraw flags */ { /* ... */ 0 }
```

```csharp
[DllImport("engine", CallingConvention = CallingConvention.Cdecl)]
static extern int sm_set_bool(ulong doc, uint machine, byte[] name, nuint len, [MarshalAs(UnmanagedType.I1)] bool v);
```

---

## 9. Milestones and tests

| Milestone | Deliverable | Test |
|---|---|---|
| M1 | Scene graph + renderer + selection | Golden-image tests of shapes, strokes, text |
| M2 | Auto layout | Property tests: children never overflow when fixed; fill shares sum to free space; compare against Figma exported layouts |
| M3 | Animation clips + timeline UI | Sample unit tests (hold/linear/cubic); loop/ping-pong time wrap; event firing across wraps |
| M4 | State machine runtime | Table-driven tests: inputs sequence -> expected state sequence; trigger resets; cycle guard; interrupt snapshot |
| M5 | Listeners + prototype bridge | Hit-test tests; prototype file compiles to state machine and plays identically |
| M6 | File format + C# editor | Round-trip tests; forward-compatibility (unknown property is skipped, not fatal) |
| M7 | Interop hardening | Fuzz the command buffer; run under Valgrind/Miri for the Rust side |

Cross-cutting: deterministic fixed-timestep mode for tests, so every animation test is reproducible frame by frame.