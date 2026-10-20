export type ElementType =
  | 'rect'
  | 'ellipse'
  | 'text'
  | 'frame'
  | 'line'
  | 'arrow'
  | 'star'
  | 'polygon'
  | 'pen'
  | 'pencil'
  | 'image';

export type ToolType =
  | 'select'
  | 'hand'
  | 'frame'
  | 'rect'
  | 'ellipse'
  | 'line'
  | 'arrow'
  | 'star'
  | 'polygon'
  | 'text'
  | 'pen'
  | 'pencil'
  | 'eraser';

export type FillType = 'solid' | 'linear-gradient' | 'image';

export interface Effect {
  id: string;
  type: 'drop-shadow' | 'inner-shadow' | 'layer-blur' | 'background-blur';
  x: number;
  y: number;
  blur: number;
  spread: number;
  color: string;
  enabled: boolean;
}

export interface FigmaElement {
  id: string;
  name: string;
  type: ElementType;
  x: number;
  y: number;
  width: number;
  height: number;
  rotation: number;
  opacity: number;
  locked: boolean;
  visible: boolean;

  // Fill
  fill: string;
  fillType?: FillType;
  fillGradient?: {
    start: string;
    end: string;
    angle: number;
  };

  // Stroke
  stroke: string;
  strokeWidth: number;
  strokeStyle: 'solid' | 'dashed' | 'dotted';

  // Corner radius
  cornerRadius: number;

  // Effects
  effects: Effect[];

  // Typography
  text?: string;
  fontSize?: number;
  fontFamily?: string;
  fontWeight?: string | number;
  textAlign?: 'left' | 'center' | 'right' | 'justify';
  lineHeight?: number;
  letterSpacing?: number;

  // Vector / Path / Custom points
  points?: { x: number; y: number }[];
  pathData?: string;

  // Image source
  src?: string;

  // Framing & Hierarchy
  isFrame?: boolean;
  isGroup?: boolean;
  collapsed?: boolean;
  clipContent?: boolean;
  parentId?: string | null;

  // Figma Auto Layout
  isAutoLayout?: boolean;
  layoutMode?: 'horizontal' | 'vertical' | 'wrap' | 'grid';
  itemSpacing?: number; // Gap between elements
  paddingTop?: number;
  paddingRight?: number;
  paddingBottom?: number;
  paddingLeft?: number;
  alignItems?: 'start' | 'center' | 'end' | 'baseline'; // Counter axis alignment
  justifyContent?: 'start' | 'center' | 'end' | 'space-between'; // Primary axis distribution
  layoutSizingHorizontal?: 'fixed' | 'hug' | 'fill';
  layoutSizingVertical?: 'fixed' | 'hug' | 'fill';
  layoutWrap?: boolean;
  gridColumns?: number;

  // Prototyping
  prototypeTargetId?: string;
  prototypeTransition?: 'instant' | 'slide-left' | 'dissolve';
}

export interface Page {
  id: string;
  name: string;
  elements: FigmaElement[];
}

export interface Project {
  id: string;
  name: string;
  pages: Page[];
  activePageId: string;
  workspaceId?: string;
  createdAt?: number;
  updatedAt?: number;
  description?: string;
  thumbnail?: string;
  deleted?: boolean;
  deletedAt?: number;
}

export interface Workspace {
  id: string;
  name: string;
  description?: string;
  icon?: string;
  color?: string;
  projectCount?: number;
  createdAt?: number;
}

export interface CanvasSettings {
  backgroundColor: string;
  gridType: 'dots' | 'lines' | 'isometric' | 'none';
  gridSize: number;
  gridColor?: string;
  showRulers: boolean;
  showGuides: boolean;
  preset?: 'infinite' | 'desktop' | 'mobile' | 'tablet';
  fixedWidth?: number;
  fixedHeight?: number;
}

export interface GuideLine {
  id: string;
  axis: 'horizontal' | 'vertical';
  position: number; // in canvas coordinate space
}

export type AppViewMode = 'home' | 'editor';

export interface Collaborator {
  id: string;
  name: string;
  color: string;
  avatar: string;
  x: number;
  y: number;
  activeElementId?: string;
}

export interface Viewport {
  x: number;
  y: number;
  zoom: number;
}

export interface ContextMenuState {
  isOpen: boolean;
  x: number;
  y: number;
  targetElementId?: string | null;
  canvasX: number;
  canvasY: number;
}

export interface EngineStats {
  fps: number;
  frameTimeUs: number; // in microseconds
  wasmInstructions: number;
  wasmMemoryBytes: number;
  webglDrawCalls: number;
  webglVertexCount: number;
  culledElements: number;
  hardwareAcceleration: boolean;
  webglActive: boolean;
  renderMode: 'hybrid' | 'webgl' | 'wireframe';
}
