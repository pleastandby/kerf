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
  Interp: number; // 0=Hold, 1=Linear, 2=Cubic
  X1?: number;
  Y1?: number;
  X2?: number;
  Y2?: number;
  Mass?: number;
  Stiffness?: number;
  Damping?: number;
}

export interface KfxTrack {
  Id?: string;
  IsActive?: boolean;
  NodeId?: number;
  TargetNodeId?: string;
  Prop?: number;
  PropertyPath?: string;
  Keys: KfxKeyframe[];
}

export interface KfxClip {
  Id: number;
  Name: string;
  Duration: number;
  Fps?: number;
  LoopMode?: number; // 0=Once, 1=Loop, 2=PingPong
  Tracks: KfxTrack[];
}

export interface KfxInput {
  InputId: string;
  Id: number;
  Name: string;
  Kind: number; // 0=Bool, 1=Number, 2=Trigger
  Value: number;
  InitialValue?: number;
  Fired?: boolean;
}

export interface KfxCondition {
  InputId?: string;
  InputIndex?: number;
  PropertyName: string;
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
  Kind: number; // 0=Entry, 1=Exit, 2=AnyState, 3=Animation
  ClipIndex: number;
  ClipName?: string;
  Speed?: number;
  Loop?: boolean;
}

export interface KfxListenerAction {
  Kind: number;
  InputIndex?: number;
  InputId?: string;
  PropertyName: string;
  Parameter: number;
}

export interface KfxListener {
  ListenerId: string;
  TargetNodeId?: number;
  TargetLayerId: string;
  IsActive?: boolean;
  Kind: number; // 0=Click, 1=Down, 2=Up, 3=PointerEnter, 4=PointerExit, 5=Move
  Enabled?: boolean;
  Actions: KfxListenerAction[];
}

export interface KfxStateMachine {
  Name: string;
  Inputs: KfxInput[];
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
      // Cubic Bézier
      const eased = cubicBezier(u, a.X1 ?? 0.25, a.Y1 ?? 0.1, a.X2 ?? 0.25, a.Y2 ?? 1.0);
      return a.Value + (b.Value - a.Value) * eased;
    }
  }

  return keys[0].Value;
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
}

export type ScenePose = Record<string, LayerPose>;

export class KerfRuntimePlayer {
  public pkg: KfxPackage;
  public isPlaying: boolean = true;
  public currentTime: number = 0.0;
  public playbackSpeed: number = 1.0;

  // State Machine State
  public inputs: Record<string, number> = {};
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

  // Change Callback
  public onStateChange?: (stateName: string) => void;
  public onInputChange?: (name: string, value: number) => void;

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

    // 2. Initialize States
    if (this.pkg.stateMachine?.Layers?.[0]?.States) {
      const states = this.pkg.stateMachine.Layers[0].States;
      // Find Entry state
      const entryState = states.find(s => s.Kind === 0 || s.Name.toLowerCase() === 'entry') ?? states[0];
      this.currentState = entryState;

      // Check if entry has immediate transition
      const transitions = this.pkg.stateMachine.Layers[0].Transitions ?? [];
      const entryTrans = transitions.find(t => t.FromState === entryState.Id);
      if (entryTrans) {
        const targetState = states.find(s => s.Id === entryTrans.ToState);
        if (targetState) {
          this.currentState = targetState;
        }
      }

      this.updateActiveClip();
    } else if (this.pkg.animations.length > 0) {
      this.activeClip = this.pkg.animations[0];
    }
  }

  private updateActiveClip() {
    if (!this.currentState) return;
    if (this.currentState.ClipIndex >= 0 && this.currentState.ClipIndex < this.pkg.animations.length) {
      this.activeClip = this.pkg.animations[this.currentState.ClipIndex];
    } else if (this.currentState.ClipName) {
      this.activeClip = this.pkg.animations.find(c => c.Name === this.currentState?.ClipName) ?? null;
    } else if (this.pkg.animations.length > 0) {
      this.activeClip = this.pkg.animations[0];
    }
  }

  // Input Control API
  public setBool(name: string, value: boolean) {
    const num = value ? 1 : 0;
    if (this.inputs[name] !== num) {
      this.inputs[name] = num;
      this.onInputChange?.(name, num);
      this.evaluateStateTransitions();
    }
  }

  public setNumber(name: string, value: number) {
    if (this.inputs[name] !== value) {
      this.inputs[name] = value;
      this.onInputChange?.(name, value);
      this.evaluateStateTransitions();
    }
  }

  public fireTrigger(name: string) {
    this.inputs[name] = 1;
    this.onInputChange?.(name, 1);
    this.evaluateStateTransitions();
    setTimeout(() => {
      this.inputs[name] = 0;
      this.onInputChange?.(name, 0);
    }, 16);
  }

  // Pointer Interaction
  public handlePointerMove(artboardX: number, artboardY: number) {
    if (!this.pkg.scene?.Layers) return;

    for (const layer of this.pkg.scene.Layers) {
      const isInside =
        artboardX >= layer.X &&
        artboardX <= layer.X + (layer.Width || 100) &&
        artboardY >= layer.Y &&
        artboardY <= layer.Y + (layer.Height || 100);

      const layerKey = layer.Id;
      const wasInside = this.hoveredLayers.has(layerKey);

      if (isInside && !wasInside) {
        // Pointer Enter
        this.hoveredLayers.add(layerKey);
        this.dispatchLayerEvent(layer, 3 /* PointerEnter */);
      } else if (!isInside && wasInside) {
        // Pointer Exit
        this.hoveredLayers.delete(layerKey);
        this.dispatchLayerEvent(layer, 4 /* PointerExit */);
      }
    }
  }

  public handlePointerLeave() {
    for (const layerKey of Array.from(this.hoveredLayers)) {
      this.hoveredLayers.delete(layerKey);
      const layer = this.pkg.scene?.Layers?.find(l => l.Id === layerKey);
      if (layer) {
        this.dispatchLayerEvent(layer, 4 /* PointerExit */);
      }
    }
  }

  private dispatchLayerEvent(layer: KfxLayer, eventKind: number) {
    const listeners = this.pkg.stateMachine?.Listeners ?? [];
    for (const l of listeners) {
      if (l.TargetLayerId === layer.Id || (layer.NumericId && l.TargetNodeId === layer.NumericId)) {
        if (l.Kind === eventKind || (eventKind === 4 && l.Kind === 3)) {
          for (const act of l.Actions) {
            if (act.PropertyName) {
              const val = (eventKind === 4) ? 0 : (act.Parameter ?? 1);
              this.setNumber(act.PropertyName, val);
            }
          }
        }
      }
    }
  }

  // State Transition Evaluation
  private evaluateStateTransitions() {
    if (!this.pkg.stateMachine?.Layers?.[0]) return;
    const layer = this.pkg.stateMachine.Layers[0];
    const states = layer.States;
    const transitions = layer.Transitions ?? [];

    if (!this.currentState) return;

    for (const tr of transitions) {
      if (tr.FromState !== this.currentState.Id && tr.FromState !== 1 /* AnyState */) {
        continue;
      }

      let conditionsMet = true;
      const conditionGroups = tr.ConditionGroups ?? [];
      const rootConditions = tr.Conditions ?? [];

      if (conditionGroups.length > 0) {
        for (const cg of conditionGroups) {
          for (const cond of cg.Conditions) {
            if (!this.testCondition(cond)) {
              conditionsMet = false;
              break;
            }
          }
          if (!conditionsMet) break;
        }
      } else if (rootConditions.length > 0) {
        for (const cond of rootConditions) {
          if (!this.testCondition(cond)) {
            conditionsMet = false;
            break;
          }
        }
      }

      if (conditionsMet) {
        const nextState = states.find(s => s.Id === tr.ToState);
        if (nextState && nextState.Id !== this.currentState.Id) {
          this.startTransition(tr, nextState);
          break;
        }
      }
    }
  }

  private testCondition(cond: KfxCondition): boolean {
    const inputVal = this.inputs[cond.PropertyName] ?? 0;
    const targetVal = cond.Value;
    switch (cond.Op) {
      case 0: return inputVal === targetVal; // Equal
      case 1: return inputVal !== targetVal; // NotEqual
      case 2: return inputVal < targetVal;
      case 3: return inputVal <= targetVal;
      case 4: return inputVal > targetVal;
      case 5: return inputVal >= targetVal;
      default: return inputVal === targetVal;
    }
  }

  private startTransition(tr: KfxTransition, nextState: KfxState) {
    if (tr.Duration <= 0) {
      this.currentState = nextState;
      this.updateActiveClip();
      this.isTransitioning = false;
      this.onStateChange?.(nextState.Name);
    } else {
      this.isTransitioning = true;
      this.transitionFromState = this.currentState;
      this.transitionToState = nextState;
      this.activeTransition = tr;
      this.transitionTime = 0.0;
      this.transitionDuration = tr.Duration;
      this.transitionMix = 0.0;
      this.onStateChange?.(`${this.transitionFromState?.Name} → ${nextState.Name}`);
    }
  }

  // Animation Update Loop
  public advance(dtSeconds: number) {
    if (!this.isPlaying) return;

    const dt = dtSeconds * this.playbackSpeed;

    const duration = this.activeClip?.Duration ?? 2.0;
    if (duration > 0) {
      this.currentTime = (this.currentTime + dt) % duration;
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
        this.currentState = this.transitionToState;
        this.updateActiveClip();
        this.isTransitioning = false;
        this.transitionFromState = null;
        this.transitionToState = null;
        this.activeTransition = null;
        this.transitionMix = 0.0;
        this.onStateChange?.(this.currentState.Name);
      }
    }
  }

  // Pose Computation & Blending
  public computePose(): ScenePose {
    const basePose: ScenePose = {};
    if (!this.activeClip) return basePose;

    this.evaluateClipIntoPose(this.activeClip, this.currentTime, basePose);

    if (this.isTransitioning && this.transitionToState) {
      const destClipIndex = this.transitionToState.ClipIndex;
      const destClip = (destClipIndex >= 0 && destClipIndex < this.pkg.animations.length)
        ? this.pkg.animations[destClipIndex]
        : null;

      if (destClip) {
        const destPose: ScenePose = {};
        this.evaluateClipIntoPose(destClip, this.currentTime % (destClip.Duration || 2.0), destPose);

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
          if (pB.VisualHeight !== undefined && pA.VisualHeight !== undefined) pA.VisualHeight = pA.VisualHeight * (1 - mix) + pB.VisualHeight * mix;

          if (pB.ScaleX !== undefined && pA.ScaleX !== undefined) pA.ScaleX = pA.ScaleX * (1 - mix) + pB.ScaleX * mix;
          if (pB.ScaleY !== undefined && pA.ScaleY !== undefined) pA.ScaleY = pA.ScaleY * (1 - mix) + pB.ScaleY * mix;

          if (pB.Rotation !== undefined && pA.Rotation !== undefined) pA.Rotation = pA.Rotation * (1 - mix) + pB.Rotation * mix;
          if (pB.Opacity !== undefined && pA.Opacity !== undefined) pA.Opacity = pA.Opacity * (1 - mix) + pB.Opacity * mix;
        }
      }
    }

    return basePose;
  }

  private evaluateClipIntoPose(clip: KfxClip, t: number, pose: ScenePose) {
    if (!clip?.Tracks) return;

    for (const tr of clip.Tracks) {
      if (tr.IsActive === false) continue;
      const targetId = tr.TargetNodeId ?? String(tr.NodeId);
      if (!pose[targetId]) pose[targetId] = {};

      const val = evaluateTrack(tr, t);
      const propPath = (tr.PropertyPath || '').toLowerCase();

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
      if (layer.IsVisible === false) continue;

      ctx.save();
      const layerPose = pose[layer.Id] ?? (layer.NumericId ? pose[String(layer.NumericId)] : null) ?? {};

      const width = layerPose.VisualWidth ?? layerPose.Width ?? layer.Width ?? 100;
      const height = layerPose.VisualHeight ?? layerPose.Height ?? layer.Height ?? 100;
      const scaleX = layerPose.ScaleX ?? layer.ScaleX ?? 1;
      const scaleY = layerPose.ScaleY ?? layer.ScaleY ?? 1;
      const rotDeg = layerPose.Rotation ?? layer.Rotation ?? 0;
      const rot = (rotDeg * Math.PI) / 180;
      const opacity = Math.max(0, Math.min(1, layerPose.Opacity ?? layer.Opacity ?? 1));

      ctx.globalAlpha = opacity;

      const cx = layer.X + width * 0.5;
      const cy = layer.Y + height * 0.5;
      ctx.translate(cx, cy);
      ctx.rotate(rot);
      ctx.scale(scaleX, scaleY);
      ctx.translate(-cx, -cy);

      if (layer.Fills && layer.Fills.length > 0 && layer.Fills[0].ColorHex) {
        ctx.fillStyle = layer.Fills[0].ColorHex;
      } else {
        ctx.fillStyle = '#203BFF';
      }

      const r = layer.CornerRadius ?? 0;
      if (r > 0 && typeof ctx.roundRect === 'function') {
        ctx.beginPath();
        ctx.roundRect(layer.X, layer.Y, width, height, r);
        ctx.fill();
      } else {
        ctx.fillRect(layer.X, layer.Y, width, height);
      }

      ctx.restore();
    }

    ctx.restore();
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

