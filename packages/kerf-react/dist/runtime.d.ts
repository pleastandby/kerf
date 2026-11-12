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
    Interp: number;
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
    LoopMode?: number;
    Tracks: KfxTrack[];
}
export interface KfxInput {
    InputId: string;
    Id: number;
    Name: string;
    Kind: number;
    Value: number;
    InitialValue?: number;
    Fired?: boolean;
}
export interface KfxCondition {
    InputId?: string;
    InputIndex?: number;
    PropertyName: string;
    Op: number;
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
    Kind: number;
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
    Kind: number;
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
/**
 * 1D Cubic Bézier solver with Newton-Raphson & bisection fallback
 */
export declare function cubicBezier(t: number, x1: number, y1: number, x2: number, y2: number): number;
/**
 * Evaluates a track at time t
 */
export declare function evaluateTrack(track: KfxTrack, t: number): number;
export declare function loadKfxFromUrl(url: string): Promise<KfxPackage>;
export declare function loadKfxFromArrayBuffer(buffer: ArrayBuffer): KfxPackage;
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
export declare class KerfRuntimePlayer {
    pkg: KfxPackage;
    isPlaying: boolean;
    currentTime: number;
    playbackSpeed: number;
    inputs: Record<string, number>;
    currentState: KfxState | null;
    activeClip: KfxClip | null;
    isTransitioning: boolean;
    transitionFromState: KfxState | null;
    transitionToState: KfxState | null;
    transitionTime: number;
    transitionDuration: number;
    transitionMix: number;
    activeTransition: KfxTransition | null;
    private hoveredLayers;
    onStateChange?: (stateName: string) => void;
    onInputChange?: (name: string, value: number) => void;
    constructor(pkg: KfxPackage);
    private init;
    private updateActiveClip;
    setBool(name: string, value: boolean): void;
    setNumber(name: string, value: number): void;
    fireTrigger(name: string): void;
    handlePointerMove(artboardX: number, artboardY: number): void;
    handlePointerLeave(): void;
    private dispatchLayerEvent;
    private evaluateStateTransitions;
    private testCondition;
    private startTransition;
    advance(dtSeconds: number): void;
    computePose(): ScenePose;
    private evaluateClipIntoPose;
    renderToCanvas(canvas: HTMLCanvasElement): void;
    getArtboardCoords(canvas: HTMLCanvasElement, clientX: number, clientY: number): {
        x: number;
        y: number;
    };
}
