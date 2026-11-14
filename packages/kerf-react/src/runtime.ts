import * as fflate from 'fflate';

// ============================================================================
// Kerf .kfx Format Interfaces
// ============================================================================

export interface KfxManifest {
  FormatVersion: string;
  Generator: string;
  CreatedAt: string;
  RuntimeCompatibility: string;
  Title: string;
  HasMetadata: boolean;
  EntryPoints?: {
    defaultClipId?: number;
    defaultStateMachine?: string;
  };
}

export interface KfxFill {
  Type: number;
  ColorHex: string;
  Opacity: number;
  Enabled: boolean;
}

export interface KfxLayer {
  Id: string;
  NumericId?: number;
  Name: string;
  Type: number;
  X: number;
  Y: number;
  Width: number;
  Height: number;
  Rotation: number;
  ScaleX: number;
  ScaleY: number;
  CornerRadius?: number;
  CornerRadiusTopLeft?: number;
  CornerRadiusTopRight?: number;
  CornerRadiusBottomRight?: number;
  CornerRadiusBottomLeft?: number;
  Fills?: KfxFill[];
  Opacity?: number;
  IsVisible?: boolean;
  Children?: KfxLayer[];
  ClipContent?: boolean;
  Constraints?: KfxConstraint[];
  ClippingTargets?: KfxClippingTarget[];
  Geometry?: KfxVectorGeometry;
}

export interface KfxVectorGeometry {
  SvgPathData?: string;
  IsClosed?: boolean;
  Nodes?: KfxBezierNode[];
}

export interface KfxBezierNode {
  Anchor: { X: number; Y: number };
  InHandle: { X: number; Y: number };
  OutHandle: { X: number; Y: number };
  Type?: number;
}

export interface KfxConstraint {
  Type: number; // 0=Transform, 1=Distance, 2=Translation, 3=Scale, 4=Rotation, 5=IK, 6=FollowPath, 7=Scroll
  TargetLayerId?: string;
  TargetNodeId?: number;
  Strength?: number;
  Enabled?: boolean;
  ConstrainX?: boolean;
  ConstrainY?: boolean;
  OffsetX?: number;
  OffsetY?: number;
  OffsetRotation?: number;
  MinLimitEnabled?: boolean;
  MinLimit?: number;
  MaxLimitEnabled?: boolean;
  MaxLimit?: number;
  DistanceMode?: number; // 0=Exact, 1=Closer/maximum, 2=Further/minimum
  Distance?: number;
  BoneLength1?: number;
  BoneLength2?: number;
  InvertBend?: boolean;
  PathDistance?: number;
  PathOffset?: number;
  OrientToPath?: boolean;
  ScrollDirection?: number; // 0=Vertical, 1=Horizontal, 2=Both
  ScrollOffset?: number;
  MinScrollOffset?: number;
  MaxScrollOffset?: number;
}

export interface KfxClippingTarget {
  TargetLayerId?: string;
  TargetNodeId?: number;
  IsInverse?: boolean;
  Enabled?: boolean;
}

export interface KfxScene {
  PageId: string;
  Name: string;
  Width: number;
  Height: number;
  BackgroundColor: string;
  Layers: KfxLayer[];
}

export interface KfxKeyframe {
  Id?: string;
  Time: number;
  Value: number;
  Interp: number; // 0=Hold, 1=Linear, 2=Cubic, 3=Spring
  X1?: number;
  Y1?: number;
  X2?: number;
  Y2?: number;
  Mass?: number;
  Stiffness?: number;
  Damping?: number;
  InitialVelocity?: number;
  GeometryValue?: KfxVectorGeometry;
  GeometryNodeValue?: KfxBezierNode;
}

export interface KfxTrack {
  Id?: string;
  IsActive?: boolean;
  NodeId?: number;
  TargetNodeId?: string;
  Prop?: number;
  PropertyPath?: string;
  GeometryNodeIndex?: number;
  Keys: KfxKeyframe[];
}

export type KfxLoopMode = 0 | 1 | 2; // Once, Loop, PingPong

export interface KfxClip {
  Id: number;
  Name: string;
  Duration: number;
  WorkAreaStart?: number;
  WorkAreaEnd?: number;
  Fps?: number;
  LoopMode?: KfxLoopMode;
  Tracks: KfxTrack[];
}

export interface KfxInput {
  InputId: string;
  Id: number;
  Name: string;
  Kind: number; // 0=Boolean, 1=Number, 2=Trigger, 3=Folder
  ParentFolderId?: string;
  Value: number;
  InitialValue?: number;
  Fired?: boolean;
}

export interface KfxCondition {
  InputId?: string;
  InputIndex?: number;
  PropertyName?: string;
  Op: number; // 0=Equal, 1=NotEqual, 2=Less, 3=LessOrEqual, 4=Greater, 5=GreaterOrEqual
  Value: number;
}

export interface KfxConditionGroup {
  Id?: string;
  Conditions: KfxCondition[];
}

export interface KfxTransition {
  TransitionId: string;
  FromState: number;
  ToState: number;
  FromStateId?: string;
  ToStateId?: string;
  Duration: number;
  DurationMs?: number;
  ExitTime?: number | null;
  Interrupt?: boolean;
  Interpolation?: number;
  X1?: number;
  Y1?: number;
  X2?: number;
  Y2?: number;
  ConditionGroups?: KfxConditionGroup[];
  Conditions?: KfxCondition[];
}

export interface KfxState {
  Id: number;
  NodeId: string;
  Name: string;
  Kind: number; // 0=Entry, 1=Exit, 2=AnyState, 3=Animation, 4=Blend1D, 5=BlendDirect
  StateType?: number;
  ClipIndex: number;
  ClipName?: string;
  BlendInputIndex?: number;
  Blend1DItems?: Array<{ Threshold: number; ClipIndex: number }>;
  BlendDirectItems?: Array<{ WeightInputIndex: number; ClipIndex: number }>;
  Speed?: number;
  Loop?: boolean;
  LoopMode?: KfxLoopMode;
  ClipId?: number;
}

export interface KfxListenerAction {
  Kind: number;
  InputIndex?: number;
  InputId?: string;
  PropertyName?: string;
  TextValue?: string;
  Parameter: number;
  TargetLayerId?: string;
  TargetNodeId?: number;
}

export interface KfxListener {
  ListenerId: string;
  Name?: string;
  Filter?: string;
  TargetNodeId?: number;
  TargetLayerId?: string;
  IsActive?: boolean;
  Kind: number; // Mirrors PointerEventKind in the native runtime
  Enabled?: boolean;
  Actions: KfxListenerAction[];
}

export interface KfxStateMachine {
  Name: string;
  Inputs: KfxInput[];
  ViewModelProperties?: Array<{
    Name: string;
    Kind: number;
    InitialNumber?: number;
    InitialBoolean?: boolean;
    InitialText?: string;
  }>;
  Layers: Array<{
    Name: string;
    States: KfxState[];
    Transitions: KfxTransition[];
  }>;
  Listeners?: KfxListener[];
}

export interface KfxPackage {
  manifest: KfxManifest;
  scene: KfxScene;
  animations: KfxClip[];
  stateMachine: KfxStateMachine | null;
}

// ============================================================================
// Utility & Interpolation Functions
// ============================================================================

/**
 * 1D Cubic Bézier solver with Newton-Raphson & bisection fallback
 */
export function cubicBezier(t: number, x1: number, y1: number, x2: number, y2: number): number {
  if (t <= 0) return 0;
  if (t >= 1) return 1;

  let low = 0.0;
  let high = 1.0;
  let s = 0.5;

  for (let i = 0; i < 16; i++) {
    s = 0.5 * (low + high);
    const x = 3 * (1 - s) * (1 - s) * s * x1 + 3 * (1 - s) * s * s * x2 + s * s * s;
    if (Math.abs(x - t) < 1e-4) break;
    if (x < t) low = s;
    else high = s;
  }

  return 3 * (1 - s) * (1 - s) * s * y1 + 3 * (1 - s) * s * s * y2 + s * s * s;
}

/**
 * Evaluates a track at time t
 */
export function evaluateTrack(track: KfxTrack, t: number): number {
  if (!track || !track.Keys || track.Keys.length === 0) return 0;
  const keys = track.Keys;
  if (t <= keys[0].Time) return keys[0].Value;
  if (t >= keys[keys.length - 1].Time) return keys[keys.length - 1].Value;

  for (let i = 0; i < keys.length - 1; i++) {
    const a = keys[i];
    const b = keys[i + 1];
    if (t >= a.Time && t <= b.Time) {
      const u = (t - a.Time) / Math.max(0.0001, b.Time - a.Time);
      if (a.Interp === 0) return a.Value; // Hold
      if (a.Interp === 1) return a.Value + (b.Value - a.Value) * u; // Linear
      if (a.Interp === 3) {
        const progress = evaluateSpring(
          t - a.Time,
          a.Mass ?? 1,
          a.Stiffness ?? 100,
          a.Damping ?? 10,
          a.InitialVelocity ?? 0
        );
        return a.Value + (b.Value - a.Value) * (Number.isFinite(progress) ? progress : 1);
      }
      // Cubic Bézier
      const eased = cubicBezier(u, a.X1 ?? 0.25, a.Y1 ?? 0.1, a.X2 ?? 0.25, a.Y2 ?? 1.0);
      return a.Value + (b.Value - a.Value) * eased;
    }
  }

  return keys[0].Value;
}

function evaluateSpring(time: number, mass: number, stiffness: number, damping: number, initialVelocity: number): number {
  if (time <= 0) return 0;
  const safeMass = Math.max(0.0001, mass);
  const safeStiffness = Math.max(0.0001, stiffness);
  const safeDamping = Math.max(0, damping);
  const omega0 = Math.sqrt(safeStiffness / safeMass);
  const gamma = safeDamping / (2 * safeMass);
  const discriminant = gamma * gamma - omega0 * omega0;

  if (Math.abs(discriminant) < 1e-7) {
    return 1 - Math.exp(-gamma * time) * (1 + (gamma - initialVelocity) * time);
  }
  if (discriminant < 0) {
    const omegaD = Math.sqrt(omega0 * omega0 - gamma * gamma);
    const b = (gamma - initialVelocity) / omegaD;
    return 1 - Math.exp(-gamma * time) * (Math.cos(omegaD * time) + b * Math.sin(omegaD * time));
  }

  const mu = Math.sqrt(discriminant);
  const r1 = -gamma + mu;
  const r2 = -gamma - mu;
  const c1 = (initialVelocity - gamma - mu) / (2 * mu);
  const c2 = (-initialVelocity + gamma - mu) / (2 * mu);
  return 1 + c1 * Math.exp(r1 * time) + c2 * Math.exp(r2 * time);
}

function cloneGeometry(geometry: KfxVectorGeometry): KfxVectorGeometry {
  return {
    IsClosed: geometry.IsClosed,
    SvgPathData: geometry.SvgPathData,
    Nodes: geometry.Nodes?.map(node => ({
      Anchor: { ...node.Anchor },
      InHandle: { ...node.InHandle },
      OutHandle: { ...node.OutHandle },
      Type: node.Type
    }))
  };
}

function cloneBezierNode(node: KfxBezierNode): KfxBezierNode {
  return {
    Anchor: { ...node.Anchor },
    InHandle: { ...node.InHandle },
    OutHandle: { ...node.OutHandle },
    Type: node.Type
  };
}

function interpolateBezierNode(from: KfxBezierNode, to: KfxBezierNode, amount: number): KfxBezierNode {
  const mix = Number.isFinite(amount) ? amount : 0;
  const lerp = (a: number, b: number) => a + (b - a) * mix;
  return {
    Anchor: { X: lerp(from.Anchor.X, to.Anchor.X), Y: lerp(from.Anchor.Y, to.Anchor.Y) },
    InHandle: { X: lerp(from.InHandle.X, to.InHandle.X), Y: lerp(from.InHandle.Y, to.InHandle.Y) },
    OutHandle: { X: lerp(from.OutHandle.X, to.OutHandle.X), Y: lerp(from.OutHandle.Y, to.OutHandle.Y) },
    Type: mix < 0.5 ? from.Type : to.Type
  };
}

function interpolateGeometry(from: KfxVectorGeometry, to: KfxVectorGeometry, amount: number): KfxVectorGeometry {
  const mix = Math.max(0, Math.min(1, amount));
  if (mix <= 0) return cloneGeometry(from);
  if (mix >= 1) return cloneGeometry(to);

  const fromNodes = from.Nodes ?? [];
  const toNodes = to.Nodes ?? [];
  const canMorph = fromNodes.length > 0 && fromNodes.length === toNodes.length && !from.SvgPathData && !to.SvgPathData;
  if (!canMorph) return cloneGeometry(from);

  const lerp = (a: number, b: number) => a + (b - a) * mix;
  return {
    IsClosed: mix < 0.5 ? from.IsClosed : to.IsClosed,
    Nodes: fromNodes.map((a, index) => {
      const b = toNodes[index];
      return {
        Anchor: { X: lerp(a.Anchor.X, b.Anchor.X), Y: lerp(a.Anchor.Y, b.Anchor.Y) },
        InHandle: { X: lerp(a.InHandle.X, b.InHandle.X), Y: lerp(a.InHandle.Y, b.InHandle.Y) },
        OutHandle: { X: lerp(a.OutHandle.X, b.OutHandle.X), Y: lerp(a.OutHandle.Y, b.OutHandle.Y) },
        Type: mix < 0.5 ? a.Type : b.Type
      };
    })
  };
}

export function evaluateGeometryTrack(track: KfxTrack, time: number): KfxVectorGeometry | null {
  const keys = (track?.Keys ?? []).filter(key => key?.GeometryValue != null);
  if (keys.length === 0) return null;
  if (time <= keys[0].Time) return cloneGeometry(keys[0].GeometryValue!);
  if (time >= keys[keys.length - 1].Time) return cloneGeometry(keys[keys.length - 1].GeometryValue!);

  for (let index = 0; index < keys.length - 1; index++) {
    const from = keys[index];
    const to = keys[index + 1];
    if (time < from.Time || time > to.Time) continue;
    const progressTrack: KfxTrack = {
      ...track,
      Keys: [{ ...from, Value: 0 }, { ...to, Value: 1 }]
    };
    const progress = evaluateTrack(progressTrack, time);
    return interpolateGeometry(from.GeometryValue!, to.GeometryValue!, progress);
  }
  return cloneGeometry(keys[keys.length - 1].GeometryValue!);
}

export function evaluateGeometryNodeTrack(track: KfxTrack, time: number): KfxBezierNode | null {
  const keys = (track?.Keys ?? []).filter(key => key?.GeometryNodeValue != null);
  if (keys.length === 0) return null;
  if (time <= keys[0].Time) return cloneBezierNode(keys[0].GeometryNodeValue!);
  if (time >= keys[keys.length - 1].Time) return cloneBezierNode(keys[keys.length - 1].GeometryNodeValue!);

  for (let index = 0; index < keys.length - 1; index++) {
    const from = keys[index];
    const to = keys[index + 1];
    if (time < from.Time || time > to.Time) continue;
    const progressTrack: KfxTrack = {
      ...track,
      Keys: [{ ...from, Value: 0 }, { ...to, Value: 1 }]
    };
    return interpolateBezierNode(from.GeometryNodeValue!, to.GeometryNodeValue!, evaluateTrack(progressTrack, time));
  }
  return cloneBezierNode(keys[keys.length - 1].GeometryNodeValue!);
}

// ============================================================================
// Loader: Reads a .kfx file (ZIP archive) into memory
// ============================================================================

export async function loadKfxFromUrl(url: string): Promise<KfxPackage> {
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(`Failed to load .kfx package from ${url}: HTTP ${response.status}`);
  }
  const buffer = await response.arrayBuffer();
  return loadKfxFromArrayBuffer(buffer);
}

export function loadKfxFromArrayBuffer(buffer: ArrayBuffer): KfxPackage {
  const bytes = new Uint8Array(buffer);
  const unzipped = fflate.unzipSync(bytes);

  const readJson = <T>(filename: string, required = true): T => {
    const fileBytes = unzipped[filename];
    if (!fileBytes) {
      if (required) throw new Error(`Missing expected file in .kfx container: ${filename}`);
      return null as unknown as T;
    }
    const text = new TextDecoder('utf-8').decode(fileBytes);
    return JSON.parse(text) as T;
  };

  const manifest = readJson<KfxManifest>('manifest.json');
  const scene = readJson<KfxScene>('scene.json');
  const animations = readJson<KfxClip[]>('animations.json') ?? [];
  const stateMachine = readJson<KfxStateMachine>('state-machines.json', false);

  return {
    manifest,
    scene,
    animations,
    stateMachine
  };
}

// ============================================================================
// Kerf Runtime Player Controller
// ============================================================================

export interface LayerPose {
  X?: number;
  Y?: number;
  Width?: number;
  Height?: number;
  ScaleX?: number;
  ScaleY?: number;
  VisualWidth?: number;
  VisualHeight?: number;
  Rotation?: number;
  Opacity?: number;
  PathGeometry?: KfxVectorGeometry;
}

export type ScenePose = Record<string, LayerPose>;

export class KerfRuntimePlayer {
  public pkg: KfxPackage;
  public isPlaying: boolean = true;
  public currentTime: number = 0.0;
  public playbackSpeed: number = 1.0;
  /** Optional playback override. Null preserves the mode authored in the package. */
  public loop: boolean | null = null;

  // State Machine State
  public inputs: Record<string, number> = {};
  public textInputs: Record<string, string> = {};
  public currentState: KfxState | null = null;
  public activeClip: KfxClip | null = null;

  // State Machine Transition State
  public isTransitioning: boolean = false;
  public transitionFromState: KfxState | null = null;
  public transitionToState: KfxState | null = null;
  public transitionTime: number = 0.0;
  public transitionDuration: number = 0.25;
  public transitionMix: number = 0.0;
  public activeTransition: KfxTransition | null = null;

  // Listeners & Pointer Hover Tracking
  private hoveredLayers: Set<string> = new Set();
  private pressedLayers: Set<string> = new Set();
  private alignedLayers: Map<string, { x: number; y: number }> = new Map();
  private firedTriggers: Set<string> = new Set();
  private triggerTimeouts: Map<string, ReturnType<typeof setTimeout>> = new Map();
  private pointerPosition: { x: number; y: number } | null = null;
  private isPointerDown = false;
  private playbackDirection = 1;
  private focusedNodeId = 0;

  // Change Callback
  public onStateChange?: (stateName: string) => void;
  public onInputChange?: (name: string, value: number) => void;
  public onTextInputChange?: (name: string, value: string) => void;
  public onEvent?: (eventName: string) => void;
  public reportedEvents: string[] = [];

  constructor(pkg: KfxPackage) {
    this.pkg = pkg;
    this.init();
  }

  private init() {
    // 1. Initialize Inputs from State Machine
    if (this.pkg.stateMachine?.Inputs) {
      for (const input of this.pkg.stateMachine.Inputs) {
        this.inputs[input.Name] = input.InitialValue ?? input.Value ?? 0;
      }
    }
    for (const property of this.pkg.stateMachine?.ViewModelProperties ?? []) {
      if (property.Kind === 3) this.textInputs[property.Name] = property.InitialText ?? '';
      else if (property.Kind === 0 && this.inputs[property.Name] === undefined) this.inputs[property.Name] = property.InitialBoolean ? 1 : 0;
      else if (property.Kind === 1 && this.inputs[property.Name] === undefined) this.inputs[property.Name] = property.InitialNumber ?? 0;
    }

    // 2. Initialize States
    if (this.pkg.stateMachine?.Layers?.[0]?.States) {
      const states = this.pkg.stateMachine.Layers[0].States;
      // Find Entry state
      const entryState = states.find(s => s.Kind === 0 || s.Name.toLowerCase() === 'entry') ?? states[0];
      this.currentState = entryState;

      this.updateActiveClip();
      this.currentTime = this.activeClip?.WorkAreaStart ?? 0;
      this.evaluateStateTransitions();
    } else if (this.pkg.animations.length > 0) {
      const defaultClipId = this.pkg.manifest?.EntryPoints?.defaultClipId;
      this.activeClip = this.pkg.animations.find(clip => clip.Id === defaultClipId) ?? this.pkg.animations[0];
    }
  }

  private updateActiveClip() {
    if (!this.currentState) return;
    this.activeClip = this.getClipForState(this.currentState);
    if (!this.activeClip && this.pkg.animations.length > 0) {
      this.activeClip = this.pkg.animations[0];
    }
  }

  private getClipForState(state: KfxState): KfxClip | null {
    if (state.ClipId !== undefined) {
      const clipById = this.pkg.animations.find(clip => clip.Id === state.ClipId);
      if (clipById) return clipById;
    }
    const blendClipIndex = state.Kind === 4 ? state.Blend1DItems?.[0]?.ClipIndex : undefined;
    const clipIndex = blendClipIndex ?? state.ClipIndex;
    if (clipIndex >= 0 && clipIndex < this.pkg.animations.length) {
      return this.pkg.animations[clipIndex] ?? null;
    }
    if (state.ClipName) {
      return this.pkg.animations.find(clip => clip.Name === state.ClipName) ?? null;
    }
    return null;
  }

  private getSceneLayers(): KfxLayer[] {
    const result: KfxLayer[] = [];
    const visit = (layers?: KfxLayer[]) => {
      if (!layers) return;
      for (const layer of layers) {
        result.push(layer);
        visit(layer.Children);
      }
    };
    visit(this.pkg.scene?.Layers);
    return result;
  }

  private findInput(nameOrId?: string, index?: number): KfxInput | undefined {
    const inputs = this.pkg.stateMachine?.Inputs ?? [];
    if (index !== undefined && index >= 0 && index < inputs.length) return inputs[index];
    if (!nameOrId) return undefined;
    const key = nameOrId.toLowerCase();
    return inputs.find(input =>
      input.Name?.toLowerCase() === key || input.InputId?.toLowerCase() === key
    );
  }

  private inputKey(input: KfxInput): string {
    return input.InputId || input.Name;
  }

  private getInputValue(input: KfxInput): number {
    return this.inputs[input.Name] ?? input.InitialValue ?? input.Value ?? 0;
  }

  private getNumericLayerId(layer: KfxLayer): number {
    if (layer.NumericId !== undefined && layer.NumericId !== 0) return layer.NumericId;
    let hash = 2166136261;
    for (let i = 0; i < layer.Id.length; i++) {
      hash ^= layer.Id.charCodeAt(i);
      hash = Math.imul(hash, 16777619);
    }
    return (hash >>> 0) || 1;
  }

  // Input Control API
  public setBool(name: string, value: boolean) {
    const input = this.findInput(name);
    if (input && (input.Kind === 2 || input.Kind === 3)) return;
    const inputName = input?.Name ?? name;
    const num = value ? 1 : 0;
    if (this.inputs[inputName] !== num) {
      this.inputs[inputName] = num;
      this.onInputChange?.(inputName, num);
      this.evaluateStateTransitions();
    }
  }

  public setNumber(name: string, value: number) {
    if (!Number.isFinite(value)) return;
    const input = this.findInput(name);
    if (input && (input.Kind === 2 || input.Kind === 3)) return;
    const inputName = input?.Name ?? name;
    if (this.inputs[inputName] !== value) {
      this.inputs[inputName] = value;
      this.onInputChange?.(inputName, value);
      this.evaluateStateTransitions();
    }
  }

  public fireTrigger(name: string) {
    const input = this.findInput(name);
    if (input && input.Kind !== 2) return;
    const inputName = input?.Name ?? name;
    const triggerKey = input ? this.inputKey(input) : inputName;
    this.inputs[inputName] = 1;
    this.firedTriggers.add(triggerKey);
    this.onInputChange?.(inputName, 1);
    this.evaluateStateTransitions();

    const previousTimeout = this.triggerTimeouts.get(triggerKey);
    if (previousTimeout) clearTimeout(previousTimeout);
    const timeout = setTimeout(() => {
      this.inputs[inputName] = 0;
      this.firedTriggers.delete(triggerKey);
      this.triggerTimeouts.delete(triggerKey);
      this.onInputChange?.(inputName, 0);
    }, 16);
    this.triggerTimeouts.set(triggerKey, timeout);
  }

  // Pointer Interaction
  public handlePointerMove(artboardX: number, artboardY: number) {
    this.pointerPosition = { x: artboardX, y: artboardY };
    const layers = this.getSceneLayers();
    const current = new Set(layers.filter(layer => this.containsPoint(layer, artboardX, artboardY)).map(layer => layer.Id));

    for (const layerKey of this.hoveredLayers) {
      if (!current.has(layerKey)) {
        const layer = layers.find(item => item.Id === layerKey);
        if (layer) this.dispatchLayerEvent(layer, 4 /* PointerExit */);
      }
    }
    for (const layer of layers) {
      const wasInside = this.hoveredLayers.has(layer.Id);
      const isInside = current.has(layer.Id);
      if (isInside && !wasInside) this.dispatchLayerEvent(layer, 3 /* PointerEnter */);
      if (isInside) {
        this.dispatchLayerEvent(layer, 2 /* PointerMove */);
        if (this.isPointerDown) this.dispatchLayerEvent(layer, 8 /* PointerDrag */);
      }
    }

    this.hoveredLayers = current;
    this.dispatchListenerTarget(0, 2 /* PointerMove */, null);
    if (this.isPointerDown) this.dispatchListenerTarget(0, 8 /* PointerDrag */, null);
  }

  public handlePointerLeave() {
    for (const layerKey of Array.from(this.hoveredLayers)) {
      const layer = this.getSceneLayers().find(l => l.Id === layerKey);
      if (layer) {
        this.dispatchLayerEvent(layer, 4 /* PointerExit */);
      }
    }
    this.hoveredLayers.clear();
    this.dispatchListenerTarget(0, 4 /* PointerExit */, null);
    if (!this.isPointerDown) {
      this.pressedLayers.clear();
      this.pointerPosition = null;
    }
  }

  public handlePointerDown() {
    this.isPointerDown = true;
    this.pressedLayers = new Set(this.hoveredLayers);
    const hoveredIds = Array.from(this.hoveredLayers);
    const focusedId = hoveredIds.length > 0 ? hoveredIds[hoveredIds.length - 1] : null;
    const focused = focusedId ? this.getSceneLayers().find(layer => layer.Id === focusedId) : undefined;
    if (focused) this.focusTarget(focused);
    for (const layerKey of this.hoveredLayers) {
      const layer = this.getSceneLayers().find(item => item.Id === layerKey);
      if (layer) {
        this.dispatchLayerEvent(layer, 0 /* PointerDown */);
        this.dispatchLayerEvent(layer, 7 /* DragStart */);
      }
    }
    this.dispatchListenerTarget(0, 0 /* PointerDown */, null);
    this.dispatchListenerTarget(0, 7 /* DragStart */, null);
  }

  public handlePointerUp() {
    const releaseTargets = new Set([...this.hoveredLayers, ...this.pressedLayers]);
    for (const layerKey of releaseTargets) {
      const layer = this.getSceneLayers().find(item => item.Id === layerKey);
      if (layer) {
        this.dispatchLayerEvent(layer, 1 /* PointerUp */);
        if (this.pressedLayers.has(layerKey)) this.dispatchLayerEvent(layer, 9 /* DragEnd */);
      }
    }
    this.dispatchListenerTarget(0, 1 /* PointerUp */, null);
    if (this.pressedLayers.size > 0) this.dispatchListenerTarget(0, 9 /* DragEnd */, null);
    this.isPointerDown = false;
  }

  public handlePointerCancel() {
    const releaseTargets = new Set([...this.hoveredLayers, ...this.pressedLayers]);
    for (const layerKey of releaseTargets) {
      const layer = this.getSceneLayers().find(item => item.Id === layerKey);
      if (layer && this.pressedLayers.has(layerKey)) this.dispatchLayerEvent(layer, 9 /* DragEnd */);
    }
    if (this.pressedLayers.size > 0) this.dispatchListenerTarget(0, 9 /* DragEnd */, null);
    this.isPointerDown = false;
    this.pressedLayers.clear();
    this.pointerPosition = null;
  }

  public handlePointerClick() {
    for (const layerKey of this.pressedLayers) {
      if (!this.hoveredLayers.has(layerKey)) continue;
      const layer = this.getSceneLayers().find(item => item.Id === layerKey);
      if (layer) {
        this.dispatchLayerEvent(layer, 5 /* Click */);
        this.dispatchLayerEvent(layer, 6 /* Tap */);
      }
    }
    this.dispatchListenerTarget(0, 5 /* Click */, null);
    this.dispatchListenerTarget(0, 6 /* Tap */, null);
    this.pressedLayers.clear();
  }

  public handleKeyDown(key: string) {
    const focused = this.getSceneLayers().find(layer => this.getNumericLayerId(layer) === this.focusedNodeId);
    if (focused) this.dispatchLayerEvent(focused, 12 /* Keyboard */, key);
    this.dispatchListenerTarget(0, 12 /* Keyboard */, null, key);
  }

  public handleTextInput(text: string) {
    const focused = this.getSceneLayers().find(layer => this.getNumericLayerId(layer) === this.focusedNodeId);
    if (focused) this.dispatchLayerEvent(focused, 14 /* Text Input */, text);
    this.dispatchListenerTarget(0, 14 /* Text Input */, null, text);
  }

  private containsPoint(layer: KfxLayer, x: number, y: number): boolean {
    if (layer.IsVisible === false) return false;
    const pose = this.computePose();
    const layerPose = pose[layer.Id] ?? pose[String(this.getNumericLayerId(layer))] ?? {};
    const left = layerPose.X ?? layer.X;
    const top = layerPose.Y ?? layer.Y;
    const width = layerPose.VisualWidth ?? layerPose.Width ?? layer.Width ?? 100;
    const height = layerPose.VisualHeight ?? layerPose.Height ?? layer.Height ?? 100;
    const centerX = left + width * 0.5;
    const centerY = top + height * 0.5;
    const angle = -((layerPose.Rotation ?? layer.Rotation ?? 0) * Math.PI / 180);
    const dx = x - centerX;
    const dy = y - centerY;
    const localX = (dx * Math.cos(angle) - dy * Math.sin(angle)) / (layerPose.ScaleX ?? layer.ScaleX ?? 1) + centerX;
    const localY = (dx * Math.sin(angle) + dy * Math.cos(angle)) / (layerPose.ScaleY ?? layer.ScaleY ?? 1) + centerY;
    return localX >= left && localX <= left + width && localY >= top && localY <= top + height;
  }

  private dispatchLayerEvent(layer: KfxLayer, eventKind: number, keyOrText?: string) {
    this.dispatchListenerTarget(this.getNumericLayerId(layer), eventKind, layer, keyOrText);
  }

  private focusTarget(layer: KfxLayer | undefined) {
    if (!layer) return;
    const nextId = this.getNumericLayerId(layer);
    if (this.focusedNodeId === nextId) return;
    const previous = this.getSceneLayers().find(item => this.getNumericLayerId(item) === this.focusedNodeId);
    if (previous) this.dispatchLayerEvent(previous, 11 /* Blur */);
    this.focusedNodeId = nextId;
    this.dispatchLayerEvent(layer, 10 /* Focus */);
  }

  private dispatchListenerTarget(targetNodeId: number, eventKind: number, layer: KfxLayer | null, keyOrText?: string) {
    const listeners = this.pkg.stateMachine?.Listeners ?? [];
    for (const l of listeners) {
      if (l.Enabled === false || l.IsActive === false || l.Kind !== eventKind) continue;
      const targetMatches = targetNodeId === 0
        ? (!l.TargetNodeId && !l.TargetLayerId)
        : ((!!l.TargetLayerId && l.TargetLayerId === layer?.Id) ||
          (!!targetNodeId && l.TargetNodeId === targetNodeId));
      if (!targetMatches) continue;
      for (const action of l.Actions ?? []) {
        if (keyOrText) {
          let filter = l.Filter ?? '';
          if (!filter && action.InputId && action.PropertyName) {
            const boundInput = this.findInput(action.InputId);
            if (boundInput && boundInput.Name !== action.PropertyName) filter = action.PropertyName;
          }
          if (filter && filter.toLowerCase() !== 'any' && filter.toLowerCase() !== keyOrText.toLowerCase()) continue;
        }
        this.executeListenerAction(l, action, layer, eventKind);
      }
    }

    if (eventKind === 4 && layer) {
      // Pointer Enter listeners that turn on a boolean behave like hover bindings:
      // clear that value when leaving unless the listener has an explicit exit action.
      for (const enterListener of listeners) {
        const targetsLayer = enterListener.TargetLayerId === layer.Id ||
          enterListener.TargetNodeId === this.getNumericLayerId(layer);
        if (!targetsLayer || enterListener.Kind !== 3 || enterListener.Enabled === false || enterListener.IsActive === false) continue;
        for (const enterAction of enterListener.Actions ?? []) {
          if (enterAction.Kind !== 0 || (enterAction.Parameter ?? 1) <= 0.5) continue;
          const hasExit = listeners.some(exitListener =>
            exitListener.Kind === 4 &&
            exitListener.Enabled !== false &&
            exitListener.IsActive !== false &&
            (exitListener.TargetLayerId === layer.Id || exitListener.TargetNodeId === this.getNumericLayerId(layer)) &&
            (exitListener.Actions ?? []).some(exitAction =>
              (enterAction.InputId && exitAction.InputId === enterAction.InputId) ||
              (enterAction.InputIndex !== undefined && exitAction.InputIndex === enterAction.InputIndex) ||
              (enterAction.PropertyName && exitAction.PropertyName === enterAction.PropertyName)
            )
          );
          if (!hasExit) {
            const input = this.findInput(enterAction.InputId, enterAction.InputIndex) ??
              this.findInput(enterAction.PropertyName);
            if (input) this.setBool(input.Name, false);
          }
        }
      }

    }

    if (eventKind === 4) {
      // Align-to-pointer actions stop controlling their target once the listener target exits.
      for (const moveListener of listeners) {
        const targetsExit = layer
          ? (moveListener.TargetLayerId === layer.Id ||
            moveListener.TargetNodeId === this.getNumericLayerId(layer))
          : (!moveListener.TargetLayerId && !moveListener.TargetNodeId);
        if (!targetsExit || (moveListener.Kind !== 2 && moveListener.Kind !== 8)) continue;
        for (const action of moveListener.Actions ?? []) {
          if (action.Kind !== 13) continue;
          const target = this.findLayerByTarget(action.TargetLayerId, action.TargetNodeId) ??
            (layer ? this.findLayerByTarget(moveListener.TargetLayerId, moveListener.TargetNodeId) ?? layer : undefined);
          if (target) this.alignedLayers.delete(target.Id);
        }
      }
    }
  }

  private findLayerByTarget(layerId?: string, numericId?: number): KfxLayer | undefined {
    return this.getSceneLayers().find(layer =>
      (!!layerId && layer.Id === layerId) ||
      (!!numericId && this.getNumericLayerId(layer) === numericId)
    );
  }

  private executeListenerAction(listener: KfxListener, action: KfxListenerAction, sourceLayer: KfxLayer | null, eventKind: number) {
    const input = this.findInput(action.InputId, action.InputIndex) ?? this.findInput(action.PropertyName);
    const inputName = input?.Name ?? action.PropertyName;
    const point = this.pointerPosition;
    const value = action.Parameter ?? 1;

    switch (action.Kind) {
      case 0: // SetBool
        if (inputName) this.setBool(inputName, value > 0.5);
        break;
      case 1: // SetNumber
        if (inputName) this.setNumber(inputName, value);
        break;
      case 2: // Fire trigger
        if (input?.Kind === 2) this.fireTrigger(input.Name);
        else if (inputName) this.fireTrigger(inputName);
        break;
      case 3: // View Model Boolean
        if (action.PropertyName) this.setBool(action.PropertyName, value > 0.5);
        break;
      case 4: // View Model Number
        if (action.PropertyName) this.setNumber(action.PropertyName, value);
        break;
      case 5: // View Model Trigger
        if (action.PropertyName) this.fireTrigger(action.PropertyName);
        break;
      case 6: // View Model Text
        if (action.PropertyName) {
          this.textInputs[action.PropertyName] = action.TextValue ?? '';
          this.onTextInputChange?.(action.PropertyName, this.textInputs[action.PropertyName]);
        }
        break;
      case 7: // ToggleBool
        if (input?.Kind === 0) this.setBool(input.Name, this.getInputValue(input) <= 0.5);
        break;
      case 8: // Reset
        if (input) {
          this.inputs[input.Name] = input.InitialValue ?? 0;
          this.firedTriggers.delete(this.inputKey(input));
          this.onInputChange?.(input.Name, this.inputs[input.Name]);
          this.evaluateStateTransitions();
        }
        break;
      case 9: // Pointer X
      case 10: // Pointer Y
      case 11: // Normalized Pointer X
      case 12: // Normalized Pointer Y
        if (inputName && point) {
          const x = point.x - (sourceLayer?.X ?? 0);
          const y = point.y - (sourceLayer?.Y ?? 0);
          const width = sourceLayer?.Width ?? this.pkg.scene.Width ?? 0;
          const height = sourceLayer?.Height ?? this.pkg.scene.Height ?? 0;
          const pointerValue = action.Kind === 9 ? x
            : action.Kind === 10 ? y
              : action.Kind === 11 ? (width > 0 ? Math.max(0, Math.min(100, x / width * 100)) : 50)
                : (height > 0 ? Math.max(0, Math.min(100, y / height * 100)) : 50);
          this.setNumber(inputName, pointerValue);
        }
        break;
      case 13: { // Align object to pointer
        if (!point) break;
        const target = this.findLayerByTarget(action.TargetLayerId, action.TargetNodeId) ??
          this.findLayerByTarget(listener.TargetLayerId, listener.TargetNodeId);
        if (target) this.alignedLayers.set(target.Id, { x: point.x, y: point.y });
        break;
      }
      case 14: { // Report event
        const eventName = action.TextValue?.trim();
        if (eventName) {
          this.reportedEvents.push(eventName);
          if (this.reportedEvents.length > 512) this.reportedEvents.shift();
          this.onEvent?.(eventName);
        }
        break;
      }
      case 15: // Blank action
        break;
      case 16: { // Focus target
        const target = this.findLayerByTarget(action.TargetLayerId, action.TargetNodeId) ?? sourceLayer ?? undefined;
        this.focusTarget(target);
        break;
      }
      default:
        break;
    }
  }

  // State Transition Evaluation
  private evaluateStateTransitions(exitTimeBoundary = false) {
    if (!this.pkg.stateMachine?.Layers?.[0]) return;
    const layer = this.pkg.stateMachine.Layers[0];
    const states = layer.States;
    const transitions = layer.Transitions ?? [];

    if (!this.currentState) return;

    const anyState = states.find(state => state.Kind === 2);
    const fromCurrent = transitions.filter(tr =>
      tr.FromState === this.currentState?.Id || tr.FromStateId === this.currentState?.NodeId
    );
    const fromAny = transitions.filter(tr =>
      tr.FromState === anyState?.Id || tr.FromStateId === anyState?.NodeId ||
      (!anyState && tr.FromState === 1)
    );

    for (const tr of [...fromCurrent, ...fromAny]) {
      if (this.isTransitioning && tr.Interrupt !== true) continue;
      if (tr.ExitTime !== undefined && tr.ExitTime !== null) {
        const clipDuration = this.activeClip?.Duration ?? 0;
        const workStart = Math.max(0, Math.min(clipDuration, this.activeClip?.WorkAreaStart ?? 0));
        const workEnd = Math.max(workStart + 0.001, Math.min(clipDuration, this.activeClip?.WorkAreaEnd ?? clipDuration));
        const progress = exitTimeBoundary
          ? 1
          : (clipDuration > 0 ? Math.max(0, Math.min(1, (this.currentTime - workStart) / (workEnd - workStart))) : 1);
        if (progress < tr.ExitTime) continue;
      }
      let conditionsMet = true;
      const conditionGroups = tr.ConditionGroups ?? [];
      const rootConditions = tr.Conditions ?? [];

      if (conditionGroups.length > 0) {
        conditionsMet = conditionGroups.some(cg =>
          (cg.Conditions ?? []).every(cond => this.testCondition(cond))
        );
      } else if (rootConditions.length > 0) {
        conditionsMet = rootConditions.every(cond => this.testCondition(cond));
      }

      if (conditionsMet) {
        const nextState = states.find(s =>
          s.Id === tr.ToState || s.NodeId === tr.ToStateId
        );
        if (nextState && nextState.Id !== this.currentState.Id) {
          this.startTransition(tr, nextState);
          break;
        }
      }
    }
  }

  private testCondition(cond: KfxCondition): boolean {
    const input = this.findInput(cond.InputId) ??
      this.findInput(cond.PropertyName) ??
      this.findInput(undefined, cond.InputIndex);
    if (input?.Kind === 3) return false;
    if (input?.Kind === 2) {
      return this.firedTriggers.has(this.inputKey(input));
    }
    const inputVal = input
      ? this.getInputValue(input)
      : this.inputs[cond.PropertyName ?? ''] ?? 0;
    const targetVal = cond.Value;
    const epsilon = 1e-5;
    switch (cond.Op) {
      case 0: return Math.abs(inputVal - targetVal) <= epsilon; // Equal
      case 1: return Math.abs(inputVal - targetVal) > epsilon; // NotEqual
      case 2: return inputVal < targetVal;
      case 3: return inputVal <= targetVal;
      case 4: return inputVal > targetVal;
      case 5: return inputVal >= targetVal;
      default: return inputVal === targetVal;
    }
  }

  private startTransition(tr: KfxTransition, nextState: KfxState) {
    const duration = tr.Duration > 0 ? tr.Duration : Math.max(0, (tr.DurationMs ?? 0) / 1000);
    if (duration <= 0) {
      this.currentState = nextState;
      this.updateActiveClip();
      this.currentTime = this.activeClip?.WorkAreaStart ?? 0;
      this.playbackDirection = 1;
      this.isTransitioning = false;
      this.onStateChange?.(nextState.Name);
    } else {
      this.isTransitioning = true;
      this.transitionFromState = this.currentState;
      this.transitionToState = nextState;
      this.activeTransition = tr;
      this.transitionTime = 0.0;
      this.transitionDuration = duration;
      this.transitionMix = 0.0;
      this.onStateChange?.(`${this.transitionFromState?.Name} → ${nextState.Name}`);
    }
  }

  private getPlaybackLoopMode(): number {
    const state = this.currentState;
    if (state) {
      // Once is paired with Loop=false. Older KFX files can contain Loop=true with
      // a default LoopMode=Once, so preserve their authored continuous playback.
      if (state.Loop === false) return 0;
      if (state.LoopMode === 2) return 2;
      if (state.Loop === true || state.LoopMode === 1) return 1;
      if (state.LoopMode === 0) return 0;
      if (this.loop !== null) return this.loop ? 1 : 0;
      return this.activeClip?.LoopMode ?? 1;
    }

    if (this.loop !== null) return this.loop ? 1 : 0;
    return this.activeClip?.LoopMode ?? 1;
  }

  // Animation Update Loop
  public advance(dtSeconds: number) {
    if (!this.isPlaying) return;

    const dt = Number.isFinite(dtSeconds) && dtSeconds > 0
      ? dtSeconds * (Number.isFinite(this.playbackSpeed) ? this.playbackSpeed : 1)
      : 0;

    const duration = this.activeClip?.Duration ?? 2.0;
    const workStart = Math.max(0, Math.min(duration, this.activeClip?.WorkAreaStart ?? 0));
    const workEnd = Math.max(workStart + 0.001, Math.min(duration, this.activeClip?.WorkAreaEnd ?? duration));
    const workDuration = workEnd - workStart;
    let crossedPlaybackBoundary = false;
    if (workDuration > 0) {
      if (this.currentTime < workStart || this.currentTime > workEnd) this.currentTime = workStart;
      const mode = this.getPlaybackLoopMode();

      if (mode === 2) {
        const direction = this.playbackDirection;
        const phase = this.currentTime - workStart + dt * direction;
        // ExitTime is measured from the start of the forward pass. The lower
        // turnaround is not the 100% exit point for a PingPong state.
        crossedPlaybackBoundary = direction >= 0 && phase >= workDuration;
        const cycle = ((phase % (workDuration * 2)) + workDuration * 2) % (workDuration * 2);
        this.currentTime = workStart + (cycle <= workDuration ? cycle : workDuration * 2 - cycle);
        this.playbackDirection = cycle <= workDuration ? 1 : -1;
      } else if (mode === 0) {
        this.currentTime = Math.min(workEnd, this.currentTime + dt);
        if (this.currentTime >= workEnd) this.isPlaying = false;
      } else {
        const unwrappedTime = this.currentTime - workStart + dt;
        crossedPlaybackBoundary = unwrappedTime >= workDuration || unwrappedTime < 0;
        this.currentTime = workStart + (((unwrappedTime % workDuration) + workDuration) % workDuration);
      }
    }

    if (this.isTransitioning && this.transitionToState) {
      this.transitionTime += dt;
      const progress = Math.min(1.0, this.transitionTime / Math.max(0.001, this.transitionDuration));
      
      const tr = this.activeTransition;
      this.transitionMix = cubicBezier(
        progress,
        tr?.X1 ?? 0.42,
        tr?.Y1 ?? 0.0,
        tr?.X2 ?? 0.58,
        tr?.Y2 ?? 1.0
      );

      if (progress >= 1.0) {
        const completedState = this.transitionToState;
        const completedClip = this.getClipForState(completedState);
        const completedTime = this.transitionTime;
        this.currentState = this.transitionToState;
        this.updateActiveClip();
        this.playbackDirection = 1;
        if (completedClip?.Duration) {
          const start = Math.max(0, Math.min(completedClip.Duration, completedClip.WorkAreaStart ?? 0));
          const end = Math.max(start + 0.001, Math.min(completedClip.Duration, completedClip.WorkAreaEnd ?? completedClip.Duration));
          const range = Math.max(0.001, end - start);
          this.currentTime = start + (completedTime % range);
        } else this.currentTime = 0;
        this.isTransitioning = false;
        this.transitionFromState = null;
        this.transitionToState = null;
        this.activeTransition = null;
        this.transitionMix = 0.0;
        this.onStateChange?.(this.currentState.Name);
      }
    }

    // Recheck exit-time and condition transitions as playback advances. In a looping
    // state, the playhead wraps but the state remains active until this finds a target.
    this.evaluateStateTransitions(crossedPlaybackBoundary);
    if (this.isTransitioning) this.isPlaying = true;
  }

  // Pose Computation & Blending
  public computePose(): ScenePose {
    const basePose: ScenePose = {};
    if (this.currentState && (this.currentState.Kind === 4 || this.currentState.Kind === 5 || this.currentState.StateType === 1)) {
      this.evaluateStateIntoPose(this.currentState, this.currentTime, basePose);
    } else if (this.activeClip) {
      this.evaluateClipIntoPose(this.activeClip, this.currentTime, basePose);

      if (this.isTransitioning && this.transitionToState) {
        const destClip = this.getClipForState(this.transitionToState);

        if (destClip) {
          const destPose: ScenePose = {};
          this.evaluateStateIntoPose(this.transitionToState, this.currentTime, destPose);

          const mix = this.transitionMix;
          for (const layerId of Object.keys(destPose)) {
            if (!basePose[layerId]) basePose[layerId] = {};
            const pA = basePose[layerId];
            const pB = destPose[layerId];

            if (pB.Width !== undefined && pA.Width !== undefined) pA.Width = pA.Width * (1 - mix) + pB.Width * mix;
            else if (pB.Width !== undefined) pA.Width = pB.Width;

            if (pB.Height !== undefined && pA.Height !== undefined) pA.Height = pA.Height * (1 - mix) + pB.Height * mix;
            else if (pB.Height !== undefined) pA.Height = pB.Height;

            if (pB.VisualWidth !== undefined && pA.VisualWidth !== undefined) pA.VisualWidth = pA.VisualWidth * (1 - mix) + pB.VisualWidth * mix;
            else if (pB.VisualWidth !== undefined) pA.VisualWidth = pB.VisualWidth;
            if (pB.VisualHeight !== undefined && pA.VisualHeight !== undefined) pA.VisualHeight = pA.VisualHeight * (1 - mix) + pB.VisualHeight * mix;
            else if (pB.VisualHeight !== undefined) pA.VisualHeight = pB.VisualHeight;

            if (pB.ScaleX !== undefined && pA.ScaleX !== undefined) pA.ScaleX = pA.ScaleX * (1 - mix) + pB.ScaleX * mix;
            else if (pB.ScaleX !== undefined) pA.ScaleX = pB.ScaleX;
            if (pB.ScaleY !== undefined && pA.ScaleY !== undefined) pA.ScaleY = pA.ScaleY * (1 - mix) + pB.ScaleY * mix;
            else if (pB.ScaleY !== undefined) pA.ScaleY = pB.ScaleY;

            if (pB.X !== undefined && pA.X !== undefined) pA.X = pA.X * (1 - mix) + pB.X * mix;
            else if (pB.X !== undefined) pA.X = pB.X;
            if (pB.Y !== undefined && pA.Y !== undefined) pA.Y = pA.Y * (1 - mix) + pB.Y * mix;
            else if (pB.Y !== undefined) pA.Y = pB.Y;
            if (pB.Rotation !== undefined && pA.Rotation !== undefined) pA.Rotation = pA.Rotation * (1 - mix) + pB.Rotation * mix;
            else if (pB.Rotation !== undefined) pA.Rotation = pB.Rotation;
            if (pB.Opacity !== undefined && pA.Opacity !== undefined) pA.Opacity = pA.Opacity * (1 - mix) + pB.Opacity * mix;
            else if (pB.Opacity !== undefined) pA.Opacity = pB.Opacity;
            if (pB.PathGeometry && pA.PathGeometry) pA.PathGeometry = interpolateGeometry(pA.PathGeometry, pB.PathGeometry, mix);
            else if (pB.PathGeometry) pA.PathGeometry = cloneGeometry(pB.PathGeometry);
          }
        }
      }
    }

    this.applyPointerAlignment(basePose);
    this.solveConstraints(basePose);
    return basePose;
  }

  private evaluateStateIntoPose(state: KfxState, time: number, pose: ScenePose) {
    const sample = (clipIndex: number): ScenePose => {
      const result: ScenePose = {};
      const clip = this.pkg.animations[clipIndex];
      if (!clip) return result;
      const start = Math.max(0, Math.min(clip.Duration, clip.WorkAreaStart ?? 0));
      const end = Math.max(start + 0.001, Math.min(clip.Duration, clip.WorkAreaEnd ?? clip.Duration));
      const range = Math.max(0.001, end - start);
      const localTime = start + ((((time - start) % range) + range) % range);
      this.evaluateClipIntoPose(clip, localTime, result);
      return result;
    };

    if ((state.Kind === 4 || state.StateType === 1) && state.Blend1DItems?.length) {
      const items = [...state.Blend1DItems].filter(item => item && item.ClipIndex >= 0 && item.ClipIndex < this.pkg.animations.length)
        .sort((a, b) => a.Threshold - b.Threshold);
      if (items.length === 0) return;
      const input = this.findInput(undefined, state.BlendInputIndex ?? -1);
      const value = input ? this.getInputValue(input) : 0;
      if (items.length === 1 || value <= items[0].Threshold) {
        Object.assign(pose, sample(items[0].ClipIndex));
        return;
      }
      const last = items[items.length - 1];
      if (value >= last.Threshold) {
        Object.assign(pose, sample(last.ClipIndex));
        return;
      }
      for (let index = 0; index < items.length - 1; index++) {
        const first = items[index];
        const second = items[index + 1];
        if (value < first.Threshold || value > second.Threshold) continue;
        const span = second.Threshold - first.Threshold;
        const weight = span > 0.00001 ? Math.max(0, Math.min(1, (value - first.Threshold) / span)) : 0;
        Object.assign(pose, this.blendPoses(sample(first.ClipIndex), sample(second.ClipIndex), weight));
        return;
      }
      Object.assign(pose, sample(last.ClipIndex));
      return;
    }

    if (state.Kind === 5 && state.BlendDirectItems?.length) {
      const items = state.BlendDirectItems.filter(item => item && item.ClipIndex >= 0 && item.ClipIndex < this.pkg.animations.length);
      const weighted: Array<{ pose: ScenePose; weight: number }> = [];
      for (const item of items) {
        const input = this.findInput(undefined, item.WeightInputIndex);
        const weight = Math.max(0, input ? this.getInputValue(input) : 0);
        if (weight > 0) weighted.push({ pose: sample(item.ClipIndex), weight });
      }
      const weightTotal = weighted.reduce((total, item) => total + item.weight, 0);
      if (weightTotal > 0) {
        const sums: Record<string, Record<string, number>> = {};
        const weights: Record<string, Record<string, number>> = {};
        const pathGeometries: Record<string, KfxVectorGeometry> = {};
        const pathWeights: Record<string, number> = {};
        for (const item of weighted) {
          for (const [layerId, layerPose] of Object.entries(item.pose)) {
            sums[layerId] ??= {};
            weights[layerId] ??= {};
            for (const [property, value] of Object.entries(layerPose)) {
              if (typeof value !== 'number') continue;
              sums[layerId][property] = (sums[layerId][property] ?? 0) + value * item.weight;
              weights[layerId][property] = (weights[layerId][property] ?? 0) + item.weight;
            }
            if (layerPose.PathGeometry) {
              const previousWeight = pathWeights[layerId] ?? 0;
              const totalWeight = previousWeight + item.weight;
              pathGeometries[layerId] = pathGeometries[layerId]
                ? interpolateGeometry(pathGeometries[layerId], layerPose.PathGeometry, item.weight / totalWeight)
                : cloneGeometry(layerPose.PathGeometry);
              pathWeights[layerId] = totalWeight;
            }
          }
        }
        for (const layerId of Object.keys(sums)) {
          const layerPose: LayerPose = {};
          for (const property of Object.keys(sums[layerId])) {
            const weight = weights[layerId][property];
            (layerPose as Record<string, number>)[property] = weight > 0 ? sums[layerId][property] / weight : 0;
          }
          if (pathGeometries[layerId]) layerPose.PathGeometry = pathGeometries[layerId];
          pose[layerId] = layerPose;
        }
      }
      return;
    }

    const clip = this.getClipForState(state);
    if (clip) this.evaluateClipIntoPose(clip, time, pose);
  }

  private blendPoses(first: ScenePose, second: ScenePose, weight: number): ScenePose {
    const result: ScenePose = {};
    const mix = Math.max(0, Math.min(1, weight));
    const layerIds = new Set([...Object.keys(first), ...Object.keys(second)]);
    for (const layerId of layerIds) {
      const firstLayer = first[layerId] ?? {};
      const secondLayer = second[layerId] ?? {};
      const layer: LayerPose = { ...firstLayer };
      const properties = new Set([...Object.keys(firstLayer), ...Object.keys(secondLayer)] as Array<keyof LayerPose>);
      for (const property of properties) {
        const firstValue = firstLayer[property];
        const secondValue = secondLayer[property];
        if (property === 'PathGeometry') {
          if (firstLayer.PathGeometry && secondLayer.PathGeometry)
            layer.PathGeometry = interpolateGeometry(firstLayer.PathGeometry, secondLayer.PathGeometry, mix);
          else if (secondLayer.PathGeometry)
            layer.PathGeometry = cloneGeometry(secondLayer.PathGeometry);
          continue;
        }
        if (typeof firstValue === 'number' && typeof secondValue === 'number')
          layer[property] = firstValue * (1 - mix) + secondValue * mix;
        else if (typeof secondValue === 'number')
          layer[property] = secondValue;
      }
      result[layerId] = layer;
    }
    return result;
  }

  private getLayerPose(pose: ScenePose, layer: KfxLayer): LayerPose {
    const byId = pose[layer.Id];
    if (byId) return byId;
    const numericId = String(this.getNumericLayerId(layer));
    if (pose[numericId]) return pose[numericId];
    pose[layer.Id] = {};
    return pose[layer.Id];
  }

  private applyPointerAlignment(pose: ScenePose) {
    for (const [layerId, point] of this.alignedLayers) {
      const layer = this.findLayerByTarget(layerId);
      if (!layer) continue;
      const layerPose = this.getLayerPose(pose, layer);
      const width = layerPose.VisualWidth ?? layerPose.Width ?? layer.Width ?? 100;
      const height = layerPose.VisualHeight ?? layerPose.Height ?? layer.Height ?? 100;
      layerPose.X = point.x - width * 0.5;
      layerPose.Y = point.y - height * 0.5;
    }
  }

  private solveConstraints(pose: ScenePose) {
    const layers = this.getSceneLayers();
    const getX = (layer: KfxLayer, layerPose: LayerPose) => layerPose.X ?? layer.X;
    const getY = (layer: KfxLayer, layerPose: LayerPose) => layerPose.Y ?? layer.Y;
    const getScaleX = (layer: KfxLayer, layerPose: LayerPose) => (layer.ScaleX ?? 1) * (layerPose.ScaleX ?? 1);
    const getScaleY = (layer: KfxLayer, layerPose: LayerPose) => (layer.ScaleY ?? 1) * (layerPose.ScaleY ?? 1);
    const getRotation = (layer: KfxLayer, layerPose: LayerPose) => layerPose.Rotation ?? layer.Rotation ?? 0;
    const clamp = (value: number, min: number, max: number) => Math.max(min, Math.min(max, value));
    const shortestRotation = (from: number, to: number) => {
      let diff = (to - from) % 360;
      if (diff > 180) diff -= 360;
      if (diff < -180) diff += 360;
      return diff;
    };
    const constrainTranslation = (layer: KfxLayer, layerPose: LayerPose, target: KfxLayer, targetPose: LayerPose, constraint: KfxConstraint, strength: number) => {
      const constrainX = constraint.ConstrainX !== false;
      const constrainY = constraint.ConstrainY !== false;
      const x = getX(layer, layerPose);
      const y = getY(layer, layerPose);
      let targetX = getX(target, targetPose) + (constraint.OffsetX ?? 0);
      let targetY = getY(target, targetPose) + (constraint.OffsetY ?? 0);
      if (constraint.MinLimitEnabled) {
        targetX = Math.max(constraint.MinLimit ?? targetX, targetX);
        targetY = Math.max(constraint.MinLimit ?? targetY, targetY);
      }
      if (constraint.MaxLimitEnabled) {
        targetX = Math.min(constraint.MaxLimit ?? targetX, targetX);
        targetY = Math.min(constraint.MaxLimit ?? targetY, targetY);
      }
      if (constrainX) layerPose.X = x + (targetX - x) * strength;
      if (constrainY) layerPose.Y = y + (targetY - y) * strength;
    };
    const constrainScale = (layer: KfxLayer, layerPose: LayerPose, target: KfxLayer, targetPose: LayerPose, constraint: KfxConstraint, strength: number) => {
      const sourceScaleX = layer.ScaleX ?? 1;
      const sourceScaleY = layer.ScaleY ?? 1;
      let targetScaleX = getScaleX(target, targetPose);
      let targetScaleY = getScaleY(target, targetPose);
      if (constraint.MinLimitEnabled) {
        targetScaleX = Math.max(constraint.MinLimit ?? targetScaleX, targetScaleX);
        targetScaleY = Math.max(constraint.MinLimit ?? targetScaleY, targetScaleY);
      }
      if (constraint.MaxLimitEnabled) {
        targetScaleX = Math.min(constraint.MaxLimit ?? targetScaleX, targetScaleX);
        targetScaleY = Math.min(constraint.MaxLimit ?? targetScaleY, targetScaleY);
      }
      if (constraint.ConstrainX !== false) {
        const current = getScaleX(layer, layerPose);
        layerPose.ScaleX = (current + (targetScaleX - current) * strength) / (Math.abs(sourceScaleX) < 0.0001 ? 1 : sourceScaleX);
      }
      if (constraint.ConstrainY !== false) {
        const current = getScaleY(layer, layerPose);
        layerPose.ScaleY = (current + (targetScaleY - current) * strength) / (Math.abs(sourceScaleY) < 0.0001 ? 1 : sourceScaleY);
      }
    };
    const constrainRotation = (layer: KfxLayer, layerPose: LayerPose, target: KfxLayer, targetPose: LayerPose, constraint: KfxConstraint, strength: number) => {
      const current = getRotation(layer, layerPose);
      let targetRotation = getRotation(target, targetPose) + (constraint.OffsetRotation ?? 0);
      if (constraint.MinLimitEnabled) targetRotation = Math.max(constraint.MinLimit ?? targetRotation, targetRotation);
      if (constraint.MaxLimitEnabled) targetRotation = Math.min(constraint.MaxLimit ?? targetRotation, targetRotation);
      layerPose.Rotation = current + shortestRotation(current, targetRotation) * strength;
    };
    const constrainDistance = (layer: KfxLayer, layerPose: LayerPose, target: KfxLayer, targetPose: LayerPose, constraint: KfxConstraint, strength: number) => {
      const width = layerPose.VisualWidth ?? layerPose.Width ?? layer.Width ?? 100;
      const height = layerPose.VisualHeight ?? layerPose.Height ?? layer.Height ?? 100;
      const targetWidth = targetPose.VisualWidth ?? targetPose.Width ?? target.Width ?? 100;
      const targetHeight = targetPose.VisualHeight ?? targetPose.Height ?? target.Height ?? 100;
      const x = getX(layer, layerPose);
      const y = getY(layer, layerPose);
      const targetX = getX(target, targetPose);
      const targetY = getY(target, targetPose);
      const dx = x + width * 0.5 - (targetX + targetWidth * 0.5);
      const dy = y + height * 0.5 - (targetY + targetHeight * 0.5);
      let distance = Math.sqrt(dx * dx + dy * dy);
      const desired = Math.max(0, constraint.Distance ?? 0);
      if (distance < 0.0001) distance = 1;
      const mode = constraint.DistanceMode ?? 1;
      const needsClamp = mode === 0 || (mode === 1 && distance > desired) || (mode === 2 && distance < desired);
      if (!needsClamp) return;
      const directionX = Math.abs(dx) + Math.abs(dy) < 0.0001 ? 1 : dx / distance;
      const directionY = Math.abs(dx) + Math.abs(dy) < 0.0001 ? 0 : dy / distance;
      const desiredX = targetX + targetWidth * 0.5 + directionX * desired - width * 0.5;
      const desiredY = targetY + targetHeight * 0.5 + directionY * desired - height * 0.5;
      layerPose.X = x + (desiredX - x) * strength;
      layerPose.Y = y + (desiredY - y) * strength;
    };
    const constrainIk = (layer: KfxLayer, layerPose: LayerPose, target: KfxLayer, targetPose: LayerPose, constraint: KfxConstraint, strength: number) => {
      const x = getX(layer, layerPose);
      const y = getY(layer, layerPose);
      const tx = getX(target, targetPose) + (target.Width ?? 100) * 0.5;
      const ty = getY(target, targetPose) + (target.Height ?? 100) * 0.5;
      const dx = tx - x;
      const dy = ty - y;
      let distance = Math.sqrt(dx * dx + dy * dy);
      if (distance < 0.0001) return;
      const bone1 = constraint.BoneLength1 && constraint.BoneLength1 > 0 ? constraint.BoneLength1 : Math.max(20, (layer.Height ?? 100) * 0.5);
      const bone2 = constraint.BoneLength2 && constraint.BoneLength2 > 0 ? constraint.BoneLength2 : Math.max(20, (layer.Height ?? 100) * 0.5);
      distance = clamp(distance, Math.abs(bone1 - bone2) + 0.001, bone1 + bone2 - 0.001);
      const cosAlpha = clamp((bone1 * bone1 + distance * distance - bone2 * bone2) / (2 * bone1 * distance), -1, 1);
      const alpha = Math.acos(cosAlpha) * 180 / Math.PI;
      const baseAngle = Math.atan2(dy, dx) * 180 / Math.PI;
      const targetAngle = baseAngle + (constraint.InvertBend ? -alpha : alpha);
      const current = getRotation(layer, layerPose);
      layerPose.Rotation = current + shortestRotation(current, targetAngle) * strength;
    };

    for (const layer of layers) {
      for (const constraint of layer.Constraints ?? []) {
        if (constraint.Enabled === false || !constraint.TargetLayerId && !constraint.TargetNodeId) continue;
        const target = this.findLayerByTarget(constraint.TargetLayerId, constraint.TargetNodeId);
        if (!target || target === layer) continue;
        const strength = clamp(constraint.Strength ?? 1, 0, 1);
        if (strength <= 0) continue;
        const layerPose = this.getLayerPose(pose, layer);
        const targetPose = this.getLayerPose(pose, target);

        switch (constraint.Type) {
          case 0:
            constrainTranslation(layer, layerPose, target, targetPose, constraint, strength);
            constrainScale(layer, layerPose, target, targetPose, constraint, strength);
            constrainRotation(layer, layerPose, target, targetPose, constraint, strength);
            break;
          case 1:
            constrainDistance(layer, layerPose, target, targetPose, constraint, strength);
            break;
          case 2:
            constrainTranslation(layer, layerPose, target, targetPose, constraint, strength);
            break;
          case 3:
            constrainScale(layer, layerPose, target, targetPose, constraint, strength);
            break;
          case 4:
            constrainRotation(layer, layerPose, target, targetPose, constraint, strength);
            break;
          case 5:
            constrainIk(layer, layerPose, target, targetPose, constraint, strength);
            break;
          case 6:
            this.constrainFollowPath(layer, layerPose, target, targetPose, constraint, strength, shortestRotation);
            break;
          case 7: {
            const offset = clamp(
              constraint.ScrollOffset ?? 0,
              constraint.MinScrollOffset ?? -1000,
              constraint.MaxScrollOffset ?? 1000
            );
            const direction = constraint.ScrollDirection ?? 0;
            if (direction === 0 || direction === 2) {
              const y = getY(layer, layerPose);
              layerPose.Y = y + (getY(target, targetPose) + offset - y) * strength;
            }
            if (direction === 1 || direction === 2) {
              const x = getX(layer, layerPose);
              layerPose.X = x + (getX(target, targetPose) + offset - x) * strength;
            }
            break;
          }
        }
      }
    }
  }

  private constrainFollowPath(
    layer: KfxLayer,
    layerPose: LayerPose,
    target: KfxLayer,
    targetPose: LayerPose,
    constraint: KfxConstraint,
    strength: number,
    shortestRotation: (from: number, to: number) => number
  ) {
    const position = this.getFollowPathPosition(target, constraint);
    if (!position) return;
    const targetX = targetPose.X ?? target.X;
    const targetY = targetPose.Y ?? target.Y;
    const targetWidth = targetPose.VisualWidth ?? targetPose.Width ?? target.Width ?? 100;
    const targetHeight = targetPose.VisualHeight ?? targetPose.Height ?? target.Height ?? 100;
    const targetCenterX = targetX + targetWidth * 0.5;
    const targetCenterY = targetY + targetHeight * 0.5;
    const targetScaleX = (target.ScaleX ?? 1) * (targetPose.ScaleX ?? 1);
    const targetScaleY = (target.ScaleY ?? 1) * (targetPose.ScaleY ?? 1);
    const targetRotation = ((targetPose.Rotation ?? target.Rotation ?? 0) * Math.PI) / 180;
    // Editable vector nodes and SVG path data use canvas coordinates, matching the renderer.
    const localX = (position.x - targetCenterX) * targetScaleX;
    const localY = (position.y - targetCenterY) * targetScaleY;
    const pointX = targetCenterX + localX * Math.cos(targetRotation) - localY * Math.sin(targetRotation);
    const pointY = targetCenterY + localX * Math.sin(targetRotation) + localY * Math.cos(targetRotation);
    const width = layerPose.VisualWidth ?? layerPose.Width ?? layer.Width ?? 100;
    const height = layerPose.VisualHeight ?? layerPose.Height ?? layer.Height ?? 100;
    const x = layerPose.X ?? layer.X;
    const y = layerPose.Y ?? layer.Y;
    const idealX = pointX - width * 0.5;
    const idealY = pointY - height * 0.5;
    layerPose.X = x + (idealX - x) * strength;
    layerPose.Y = y + (idealY - y) * strength;

    if (constraint.OrientToPath !== false) {
      const tangentX = position.tangentX * targetScaleX;
      const tangentY = position.tangentY * targetScaleY;
      const angle = Math.atan2(
        tangentX * Math.sin(targetRotation) + tangentY * Math.cos(targetRotation),
        tangentX * Math.cos(targetRotation) - tangentY * Math.sin(targetRotation)
      ) * 180 / Math.PI;
      const current = layerPose.Rotation ?? layer.Rotation ?? 0;
      layerPose.Rotation = current + shortestRotation(current, angle) * strength;
    }
  }

  private getFollowPathPosition(target: KfxLayer, constraint: KfxConstraint): { x: number; y: number; tangentX: number; tangentY: number } | null {
    const geometry = target.Geometry;
    if (!geometry) return null;
    let points: Array<{ x: number; y: number }> = [];

    if (geometry.Nodes && geometry.Nodes.length > 0) {
      const nodes = geometry.Nodes;
      points.push({ x: nodes[0].Anchor.X, y: nodes[0].Anchor.Y });
      for (let i = 1; i < nodes.length; i++) {
        const previous = nodes[i - 1];
        const current = nodes[i];
        const hasCurve = previous.OutHandle.X !== previous.Anchor.X || previous.OutHandle.Y !== previous.Anchor.Y ||
          current.InHandle.X !== current.Anchor.X || current.InHandle.Y !== current.Anchor.Y;
        if (hasCurve) {
          const p0 = previous.Anchor;
          const p1 = previous.OutHandle;
          const p2 = current.InHandle;
          const p3 = current.Anchor;
          for (let step = 1; step <= 24; step++) {
            const t = step / 24;
            const u = 1 - t;
            points.push({
              x: u * u * u * p0.X + 3 * u * u * t * p1.X + 3 * u * t * t * p2.X + t * t * t * p3.X,
              y: u * u * u * p0.Y + 3 * u * u * t * p1.Y + 3 * u * t * t * p2.Y + t * t * t * p3.Y
            });
          }
        } else {
          points.push({ x: current.Anchor.X, y: current.Anchor.Y });
        }
      }
      if (geometry.IsClosed && nodes.length > 2) points.push({ x: nodes[0].Anchor.X, y: nodes[0].Anchor.Y });
    } else if (geometry.SvgPathData && typeof document !== 'undefined') {
      try {
        const svgPath = document.createElementNS('http://www.w3.org/2000/svg', 'path') as SVGPathElement;
        svgPath.setAttribute('d', geometry.SvgPathData);
        const totalLength = svgPath.getTotalLength();
        if (totalLength > 0) {
          const distance = (constraint.PathDistance ?? 0) + (constraint.PathOffset ?? 0);
          let pathLength = distance * totalLength / 100;
          if (geometry.IsClosed) pathLength = ((pathLength % totalLength) + totalLength) % totalLength;
          const clamped = Math.max(0, Math.min(totalLength, pathLength));
          const point = svgPath.getPointAtLength(clamped);
          const a = svgPath.getPointAtLength(Math.max(0, clamped - 0.5));
          const b = svgPath.getPointAtLength(Math.min(totalLength, clamped + 0.5));
          let x = point.x;
          let y = point.y;
          let tangentX = b.x - a.x;
          let tangentY = b.y - a.y;
          if (!geometry.IsClosed && pathLength < 0) {
            const end = svgPath.getPointAtLength(0);
            const next = svgPath.getPointAtLength(Math.min(totalLength, 1));
            tangentX = next.x - end.x;
            tangentY = next.y - end.y;
            const magnitude = Math.sqrt(tangentX * tangentX + tangentY * tangentY) || 1;
            x = end.x + tangentX / magnitude * pathLength;
            y = end.y + tangentY / magnitude * pathLength;
          } else if (!geometry.IsClosed && pathLength > totalLength) {
            const end = svgPath.getPointAtLength(totalLength);
            const previous = svgPath.getPointAtLength(Math.max(0, totalLength - 1));
            tangentX = end.x - previous.x;
            tangentY = end.y - previous.y;
            const magnitude = Math.sqrt(tangentX * tangentX + tangentY * tangentY) || 1;
            x = end.x + tangentX / magnitude * (pathLength - totalLength);
            y = end.y + tangentY / magnitude * (pathLength - totalLength);
          }
          return { x, y, tangentX, tangentY };
        }
      } catch {
        return null;
      }
    }

    if (points.length < 2) return null;
    const lengths: number[] = [0];
    for (let i = 1; i < points.length; i++) {
      const dx = points[i].x - points[i - 1].x;
      const dy = points[i].y - points[i - 1].y;
      lengths.push(lengths[i - 1] + Math.sqrt(dx * dx + dy * dy));
    }
    const total = lengths[lengths.length - 1];
    if (total <= 0.0001) return null;
    const closed = !!geometry.IsClosed;
    let pathDistance = ((constraint.PathDistance ?? 0) + (constraint.PathOffset ?? 0)) * total / 100;
    if (closed) pathDistance = ((pathDistance % total) + total) % total;
    if (pathDistance < 0 || pathDistance > total) {
      const atStart = pathDistance < 0;
      const startIndex = atStart ? 0 : points.length - 2;
      const endIndex = atStart ? 1 : points.length - 1;
      let tangentX = points[endIndex].x - points[startIndex].x;
      let tangentY = points[endIndex].y - points[startIndex].y;
      const magnitude = Math.sqrt(tangentX * tangentX + tangentY * tangentY) || 1;
      tangentX /= magnitude;
      tangentY /= magnitude;
      const extension = atStart ? pathDistance : pathDistance - total;
      const endpoint = atStart ? points[0] : points[points.length - 1];
      return { x: endpoint.x + tangentX * extension, y: endpoint.y + tangentY * extension, tangentX, tangentY };
    }
    for (let i = 1; i < points.length; i++) {
      if (pathDistance > lengths[i]) continue;
      const length = lengths[i] - lengths[i - 1];
      const amount = length > 0.0001 ? (pathDistance - lengths[i - 1]) / length : 0;
      const tangentX = points[i].x - points[i - 1].x;
      const tangentY = points[i].y - points[i - 1].y;
      return {
        x: points[i - 1].x + tangentX * amount,
        y: points[i - 1].y + tangentY * amount,
        tangentX,
        tangentY
      };
    }
    return null;
  }

  private evaluateClipIntoPose(clip: KfxClip, t: number, pose: ScenePose) {
    if (!clip?.Tracks) return;

    for (const tr of clip.Tracks) {
      if (tr.IsActive === false) continue;
      const targetId = tr.TargetNodeId ?? String(tr.NodeId);
      if (!pose[targetId]) pose[targetId] = {};

      const propPath = (tr.PropertyPath || '').toLowerCase();
      if (propPath.startsWith('path.nodes[') || tr.Prop === 25) continue;
      if (propPath === 'path' || propPath === 'pathgeometry' || propPath === 'geometry' || tr.Prop === 24) {
        const geometry = evaluateGeometryTrack(tr, t);
        if (geometry) pose[targetId].PathGeometry = geometry;
        continue;
      }

      const val = evaluateTrack(tr, t);

      if (propPath === 'scalex' || tr.Prop === 12) pose[targetId].ScaleX = val;
      else if (propPath === 'scaley' || tr.Prop === 13) pose[targetId].ScaleY = val;
      else if (propPath === 'width' || tr.Prop === 3) pose[targetId].Width = val;
      else if (propPath === 'height' || tr.Prop === 4) pose[targetId].Height = val;
      else if (propPath === 'visualwidth' || tr.Prop === 16) pose[targetId].VisualWidth = val;
      else if (propPath === 'visualheight' || tr.Prop === 17) pose[targetId].VisualHeight = val;
      else if (propPath === 'rotation' || tr.Prop === 5) pose[targetId].Rotation = val;
      else if (propPath === 'opacity' || tr.Prop === 7) pose[targetId].Opacity = val;
      else if (propPath === 'x' || propPath === 'translationx' || tr.Prop === 1) pose[targetId].X = val;
      else if (propPath === 'y' || propPath === 'translationy' || tr.Prop === 2) pose[targetId].Y = val;
    }

    for (const tr of clip.Tracks) {
      if (!tr || tr.IsActive === false) continue;
      const propPath = (tr.PropertyPath || '').toLowerCase();
      if (!propPath.startsWith('path.nodes[') && tr.Prop !== 25) continue;
      const nodeIndex = tr.GeometryNodeIndex;
      const geometry = pose[tr.TargetNodeId ?? String(tr.NodeId)]?.PathGeometry;
      if (nodeIndex == null || nodeIndex < 0 || !geometry?.Nodes || nodeIndex >= geometry.Nodes.length) continue;
      const node = evaluateGeometryNodeTrack(tr, t);
      if (node) geometry.Nodes[nodeIndex] = node;
    }
  }

  // Canvas Render Execution
  public renderToCanvas(canvas: HTMLCanvasElement) {
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const scene = this.pkg.scene;
    const artboardWidth = scene.Width || 1920;
    const artboardHeight = scene.Height || 1080;

    const dpr = typeof window !== 'undefined' ? (window.devicePixelRatio || 1) : 1;
    const displayWidth = canvas.clientWidth || 800;
    const displayHeight = canvas.clientHeight || 500;

    if (canvas.width !== displayWidth * dpr || canvas.height !== displayHeight * dpr) {
      canvas.width = displayWidth * dpr;
      canvas.height = displayHeight * dpr;
    }

    ctx.save();
    ctx.scale(dpr, dpr);

    ctx.fillStyle = scene.BackgroundColor || '#1E1E22';
    ctx.fillRect(0, 0, displayWidth, displayHeight);

    const scale = Math.min(displayWidth / artboardWidth, displayHeight / artboardHeight);
    const offsetX = (displayWidth - artboardWidth * scale) * 0.5;
    const offsetY = (displayHeight - artboardHeight * scale) * 0.5;

    ctx.translate(offsetX, offsetY);
    ctx.scale(scale, scale);

    const pose = this.computePose();

    for (const layer of scene.Layers) {
      this.renderLayer(layer, pose, ctx, artboardWidth, artboardHeight);
    }

    ctx.restore();
  }

  private renderLayer(layer: KfxLayer, pose: ScenePose, ctx: CanvasRenderingContext2D, artboardWidth: number, artboardHeight: number) {
    if (layer.IsVisible === false) return;

    ctx.save();
    const layerPose = pose[layer.Id] ?? pose[String(this.getNumericLayerId(layer))] ?? {};
    const width = layerPose.VisualWidth ?? layerPose.Width ?? layer.Width ?? 100;
    const height = layerPose.VisualHeight ?? layerPose.Height ?? layer.Height ?? 100;
    const x = layerPose.X ?? layer.X;
    const y = layerPose.Y ?? layer.Y;
    const scaleX = layerPose.ScaleX ?? layer.ScaleX ?? 1;
    const scaleY = layerPose.ScaleY ?? layer.ScaleY ?? 1;
    const rotation = ((layerPose.Rotation ?? layer.Rotation ?? 0) * Math.PI) / 180;
    const opacity = Math.max(0, Math.min(1, layerPose.Opacity ?? layer.Opacity ?? 1));
    ctx.globalAlpha *= opacity;

    const cx = x + width * 0.5;
    const cy = y + height * 0.5;
    ctx.translate(cx, cy);
    ctx.rotate(rotation);
    ctx.scale(scaleX, scaleY);
    ctx.translate(-cx, -cy);

    if (layer.Type === 0 && layer.ClipContent) {
      const framePath = this.makeLayerPath(layer, x, y, width, height, pose);
      if (framePath) ctx.clip(framePath);
    }

    for (const clip of layer.ClippingTargets ?? []) {
      if (clip.Enabled === false) continue;
      const target = this.findLayerByTarget(clip.TargetLayerId, clip.TargetNodeId);
      if (!target) continue;
      const clipPath = this.makeClipSourcePath(target, pose);
      if (!clipPath) continue;
      if (clip.IsInverse && typeof Path2D !== 'undefined') {
        const inversePath = new Path2D();
        inversePath.rect(0, 0, artboardWidth, artboardHeight);
        inversePath.addPath(clipPath);
        ctx.clip(inversePath, 'evenodd');
      } else {
        ctx.clip(clipPath);
      }
    }

    if (layer.Type !== 1 && layer.Type !== 12) {
      const fill = layer.Fills?.find(item => item.Enabled !== false && item.ColorHex);
      ctx.fillStyle = fill?.ColorHex ?? '#203BFF';
      const inheritedAlpha = ctx.globalAlpha;
      ctx.globalAlpha *= Math.max(0, Math.min(1, fill?.Opacity ?? 1));
      this.fillLayerPath(ctx, layer, x, y, width, height, pose);
      ctx.globalAlpha = inheritedAlpha;
    }

    for (const child of layer.Children ?? []) {
      this.renderLayer(child, pose, ctx, artboardWidth, artboardHeight);
    }
    ctx.restore();
  }

  private makeLayerPath(layer: KfxLayer, x: number, y: number, width: number, height: number, pose?: ScenePose): Path2D | null {
    if (typeof Path2D === 'undefined') return null;
    const geometry = pose ? (this.getLayerPose(pose, layer).PathGeometry ?? layer.Geometry) : layer.Geometry;
    if (layer.Type === 8 && geometry?.Nodes?.length) {
      const nodes = geometry.Nodes;
      const path = new Path2D();
      path.moveTo(nodes[0].Anchor.X, nodes[0].Anchor.Y);
      for (let i = 1; i < nodes.length; i++) {
        const previous = nodes[i - 1];
        const current = nodes[i];
        const hasCurve = previous.OutHandle.X !== previous.Anchor.X || previous.OutHandle.Y !== previous.Anchor.Y ||
          current.InHandle.X !== current.Anchor.X || current.InHandle.Y !== current.Anchor.Y;
        if (hasCurve) {
          path.bezierCurveTo(
            previous.OutHandle.X, previous.OutHandle.Y,
            current.InHandle.X, current.InHandle.Y,
            current.Anchor.X, current.Anchor.Y
          );
        } else {
          path.lineTo(current.Anchor.X, current.Anchor.Y);
        }
      }
      if (geometry.IsClosed && nodes.length > 2) {
        const last = nodes[nodes.length - 1];
        const first = nodes[0];
        const hasCurve = last.OutHandle.X !== last.Anchor.X || last.OutHandle.Y !== last.Anchor.Y ||
          first.InHandle.X !== first.Anchor.X || first.InHandle.Y !== first.Anchor.Y;
        if (hasCurve) {
          path.bezierCurveTo(
            last.OutHandle.X, last.OutHandle.Y,
            first.InHandle.X, first.InHandle.Y,
            first.Anchor.X, first.Anchor.Y
          );
        }
        path.closePath();
      }
      return path;
    }
    if (layer.Type === 8 && geometry?.SvgPathData) {
      try {
        return new Path2D(geometry.SvgPathData);
      } catch {
        // Fall through to the layer bounds when the SVG path cannot be parsed.
      }
    }

    const path = new Path2D();
    if (layer.Type === 3) {
      path.ellipse(x + width * 0.5, y + height * 0.5, width * 0.5, height * 0.5, 0, 0, Math.PI * 2);
    } else {
      path.rect(x, y, width, height);
    }
    return path;
  }

  private getLayerTransformMatrix(layer: KfxLayer, pose: ScenePose): DOMMatrix {
    const layerPose = pose[layer.Id] ?? pose[String(this.getNumericLayerId(layer))] ?? {};
    const width = layerPose.VisualWidth ?? layerPose.Width ?? layer.Width ?? 100;
    const height = layerPose.VisualHeight ?? layerPose.Height ?? layer.Height ?? 100;
    const x = layerPose.X ?? layer.X;
    const y = layerPose.Y ?? layer.Y;
    const rotation = layerPose.Rotation ?? layer.Rotation ?? 0;
    const scaleX = layerPose.ScaleX ?? layer.ScaleX ?? 1;
    const scaleY = layerPose.ScaleY ?? layer.ScaleY ?? 1;
    const matrix = new DOMMatrix();
    matrix.translateSelf(x + width * 0.5, y + height * 0.5);
    matrix.rotateSelf(rotation);
    matrix.scaleSelf(scaleX, scaleY);
    matrix.translateSelf(-(x + width * 0.5), -(y + height * 0.5));
    return matrix;
  }

  private makeClipSourcePath(layer: KfxLayer, pose: ScenePose): Path2D | null {
    if (typeof Path2D === 'undefined') return null;
    if (layer.Type === 1) {
      const combined = new Path2D();
      for (const child of layer.Children ?? []) {
        if (child.IsVisible === false) continue;
        const childPath = this.makeClipSourcePath(child, pose);
        if (childPath) combined.addPath(childPath);
      }
      const transformed = new Path2D();
      transformed.addPath(combined, this.getLayerTransformMatrix(layer, pose));
      return transformed;
    }

    const layerPose = pose[layer.Id] ?? pose[String(this.getNumericLayerId(layer))] ?? {};
    const width = layerPose.VisualWidth ?? layerPose.Width ?? layer.Width ?? 100;
    const height = layerPose.VisualHeight ?? layerPose.Height ?? layer.Height ?? 100;
    const x = layerPose.X ?? layer.X;
    const y = layerPose.Y ?? layer.Y;
    const shape = this.makeLayerPath(layer, x, y, width, height, pose);
    if (!shape) return null;
    const transformed = new Path2D();
    transformed.addPath(shape, this.getLayerTransformMatrix(layer, pose));
    return transformed;
  }

  private fillLayerPath(ctx: CanvasRenderingContext2D, layer: KfxLayer, x: number, y: number, width: number, height: number, pose: ScenePose) {
    if ((layer.CornerRadius ?? 0) > 0 && typeof ctx.roundRect === 'function' && layer.Type !== 3) {
      ctx.beginPath();
      ctx.roundRect(x, y, width, height, layer.CornerRadius ?? 0);
      ctx.fill();
      return;
    }
    const path = this.makeLayerPath(layer, x, y, width, height, pose);
    if (path) ctx.fill(path);
    else ctx.fillRect(x, y, width, height);
  }

  public getArtboardCoords(canvas: HTMLCanvasElement, clientX: number, clientY: number): { x: number; y: number } {
    const rect = canvas.getBoundingClientRect();
    const displayWidth = rect.width;
    const displayHeight = rect.height;

    const artboardWidth = this.pkg.scene.Width || 1920;
    const artboardHeight = this.pkg.scene.Height || 1080;

    const scale = Math.min(displayWidth / artboardWidth, displayHeight / artboardHeight);
    const offsetX = (displayWidth - artboardWidth * scale) * 0.5;
    const offsetY = (displayHeight - artboardHeight * scale) * 0.5;

    const mouseX = clientX - rect.left;
    const mouseY = clientY - rect.top;

    const artboardX = (mouseX - offsetX) / scale;
    const artboardY = (mouseY - offsetY) / scale;

    return { x: artboardX, y: artboardY };
  }
}

