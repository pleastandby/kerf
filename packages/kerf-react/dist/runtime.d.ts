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
    Anchor: {
        X: number;
        Y: number;
    };
    InHandle: {
        X: number;
        Y: number;
    };
    OutHandle: {
        X: number;
        Y: number;
    };
    Type?: number;
}
export interface KfxConstraint {
    Type: number;
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
    DistanceMode?: number;
    Distance?: number;
    BoneLength1?: number;
    BoneLength2?: number;
    InvertBend?: boolean;
    PathDistance?: number;
    PathOffset?: number;
    OrientToPath?: boolean;
    ScrollDirection?: number;
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
    Interp: number;
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
export type KfxLoopMode = 0 | 1 | 2;
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
    Kind: number;
    ParentFolderId?: string;
    Value: number;
    InitialValue?: number;
    Fired?: boolean;
}
export interface KfxCondition {
    InputId?: string;
    InputIndex?: number;
    PropertyName?: string;
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
    StateType?: number;
    ClipIndex: number;
    ClipName?: string;
    BlendInputIndex?: number;
    Blend1DItems?: Array<{
        Threshold: number;
        ClipIndex: number;
    }>;
    BlendDirectItems?: Array<{
        WeightInputIndex: number;
        ClipIndex: number;
    }>;
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
    Kind: number;
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
/**
 * 1D Cubic Bézier solver with Newton-Raphson & bisection fallback
 */
export declare function cubicBezier(t: number, x1: number, y1: number, x2: number, y2: number): number;
/**
 * Evaluates a track at time t
 */
export declare function evaluateTrack(track: KfxTrack, t: number): number;
export declare function evaluateGeometryTrack(track: KfxTrack, time: number): KfxVectorGeometry | null;
export declare function evaluateGeometryNodeTrack(track: KfxTrack, time: number): KfxBezierNode | null;
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
    PathGeometry?: KfxVectorGeometry;
}
export type ScenePose = Record<string, LayerPose>;
export declare class KerfRuntimePlayer {
    pkg: KfxPackage;
    isPlaying: boolean;
    currentTime: number;
    playbackSpeed: number;
    /** Optional playback override. Null preserves the mode authored in the package. */
    loop: boolean | null;
    inputs: Record<string, number>;
    textInputs: Record<string, string>;
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
    private pressedLayers;
    private alignedLayers;
    private firedTriggers;
    private triggerTimeouts;
    private pointerPosition;
    private isPointerDown;
    private playbackDirection;
    private focusedNodeId;
    onStateChange?: (stateName: string) => void;
    onInputChange?: (name: string, value: number) => void;
    onTextInputChange?: (name: string, value: string) => void;
    onEvent?: (eventName: string) => void;
    reportedEvents: string[];
    constructor(pkg: KfxPackage);
    private init;
    private updateActiveClip;
    private getClipForState;
    private getSceneLayers;
    private findInput;
    private inputKey;
    private getInputValue;
    private getNumericLayerId;
    setBool(name: string, value: boolean): void;
    setNumber(name: string, value: number): void;
    fireTrigger(name: string): void;
    handlePointerMove(artboardX: number, artboardY: number): void;
    handlePointerLeave(): void;
    handlePointerDown(): void;
    handlePointerUp(): void;
    handlePointerCancel(): void;
    handlePointerClick(): void;
    handleKeyDown(key: string): void;
    handleTextInput(text: string): void;
    private containsPoint;
    private dispatchLayerEvent;
    private focusTarget;
    private dispatchListenerTarget;
    private findLayerByTarget;
    private executeListenerAction;
    private evaluateStateTransitions;
    private testCondition;
    private startTransition;
    private getPlaybackLoopMode;
    advance(dtSeconds: number): void;
    computePose(): ScenePose;
    private evaluateStateIntoPose;
    private blendPoses;
    private getLayerPose;
    private applyPointerAlignment;
    private solveConstraints;
    private constrainFollowPath;
    private getFollowPathPosition;
    private evaluateClipIntoPose;
    renderToCanvas(canvas: HTMLCanvasElement): void;
    private renderLayer;
    private makeLayerPath;
    private getLayerTransformMatrix;
    private makeClipSourcePath;
    private fillLayerPath;
    getArtboardCoords(canvas: HTMLCanvasElement, clientX: number, clientY: number): {
        x: number;
        y: number;
    };
}
