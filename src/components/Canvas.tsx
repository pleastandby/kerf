import React, { useRef, useState, useEffect, useCallback } from 'react';
import type { FigmaElement, ToolType, Viewport, Collaborator, CanvasSettings, GuideLine } from '../types';
import { webglRenderer } from '../engine/webglRenderer';
import { getRelativePathData } from '../utils/vectorUtils';

interface CanvasProps {
  elements: FigmaElement[];
  selectedIds: string[];
  currentTool: ToolType;
  setCurrentTool: (t: ToolType) => void;
  viewport: Viewport;
  setViewport: React.Dispatch<React.SetStateAction<Viewport>>;
  onSelect: (id: string, multi?: boolean) => void;
  onClearSelection: () => void;
  onUpdateElement: (id: string, updates: Partial<FigmaElement>) => void;
  onAddElement: (element: FigmaElement) => void;
  collaborators: Collaborator[];
  showGrid: boolean;
  onContextMenu?: (e: React.MouseEvent, elementId: string | null) => void;
  canvasSettings?: CanvasSettings;
  guideLines?: GuideLine[];
  onAddGuide?: (guide: GuideLine) => void;
  onOpenCanvasSettings?: () => void;
}

type DragMode =
  | 'none'
  | 'pan'
  | 'move'
  | 'resize'
  | 'rotate'
  | 'create'
  | 'select-box'
  | 'pencil'
  | 'pen'
  | 'corner-radius';

interface PenNode {
  x: number;
  y: number;
  cp1?: { x: number; y: number };
  cp2?: { x: number; y: number };
}

interface SnapGuide {
  id: string;
  axis: 'horizontal' | 'vertical';
  pos: number;
}

type ResizeHandle = 'nw' | 'n' | 'ne' | 'e' | 'se' | 's' | 'sw' | 'w';
type CornerHandle = 'tl' | 'tr' | 'br' | 'bl';

export const Canvas: React.FC<CanvasProps> = ({
  elements,
  selectedIds,
  currentTool,
  setCurrentTool,
  viewport,
  setViewport,
  onSelect,
  onClearSelection,
  onUpdateElement,
  onAddElement,
  collaborators,
  showGrid,
  onContextMenu,
  canvasSettings,
  guideLines = [],
  onAddGuide,
  onOpenCanvasSettings,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const webglCanvasRef = useRef<HTMLCanvasElement>(null);
  const [dragMode, setDragMode] = useState<DragMode>('none');
  const [activeHandle, setActiveHandle] = useState<ResizeHandle | null>(null);
  const [dragStart, setDragStart] = useState<{ x: number; y: number }>({ x: 0, y: 0 });
  const [initialElemBounds, setInitialElemBounds] = useState<
    Record<string, { x: number; y: number; width: number; height: number; rotation: number }>
  >({});
  
  // Corner radius node state
  const [activeCorner, setActiveCorner] = useState<CornerHandle | null>(null);
  const [radiusDragValue, setRadiusDragValue] = useState<number | null>(null);
  const [initialCornerRadius, setInitialCornerRadius] = useState<number>(0);
  const [hoveredCorner, setHoveredCorner] = useState<CornerHandle | null>(null);
  const dragRadiusRef = useRef<{
    corner: CornerHandle;
    startX: number;
    startY: number;
    initialRadius: number;
    elementId: string;
    width: number;
    height: number;
    rotation: number;
  } | null>(null);
  
  // Selection rubberband box
  const [selectionBox, setSelectionBox] = useState<{ x: number; y: number; w: number; h: number } | null>(null);

  // Creation preview box
  const [createPreview, setCreatePreview] = useState<{
    x: number;
    y: number;
    w: number;
    h: number;
  } | null>(null);

  // Pencil points
  const [pencilPoints, setPencilPoints] = useState<{ x: number; y: number }[]>([]);

  // Pen tool nodes and state
  const [penNodes, setPenNodes] = useState<PenNode[]>([]);
  const [penCurrentPos, setPenCurrentPos] = useState<{ x: number; y: number } | null>(null);
  const [isDraggingPenHandle, setIsDraggingPenHandle] = useState<boolean>(false);

  // Dynamic snapping & smart guides
  const [activeSnapGuides, setActiveSnapGuides] = useState<SnapGuide[]>([]);

  // Spacebar state for panning
  const [isSpacePressed, setIsSpacePressed] = useState(false);

  // Alt key state for distance measuring
  const [isAltPressed, setIsAltPressed] = useState(false);

  // Track cursor canvas coordinates
  const [mouseCanvasPos, setMouseCanvasPos] = useState<{ x: number; y: number }>({ x: 0, y: 0 });

  // Convert client window coords to canvas coordinates
  const toCanvasCoords = useCallback(
    (clientX: number, clientY: number) => {
      if (!containerRef.current) return { x: 0, y: 0 };
      const rect = containerRef.current.getBoundingClientRect();
      const x = (clientX - rect.left - viewport.x) / viewport.zoom;
      const y = (clientY - rect.top - viewport.y) / viewport.zoom;
      return { x, y };
    },
    [viewport]
  );

  // Listen to keyboard shortcuts
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement) {
        return;
      }
      if (e.code === 'Space' && !e.repeat) {
        setIsSpacePressed(true);
      }
      if (e.key === 'Alt') {
        setIsAltPressed(true);
      }
      if ((e.key === 'Enter' || e.key === 'Escape') && currentTool === 'pen') {
        finishPenPath(false);
      }
    };

    const handleKeyUp = (e: KeyboardEvent) => {
      if (e.code === 'Space') {
        setIsSpacePressed(false);
      }
      if (e.key === 'Alt') {
        setIsAltPressed(false);
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    window.addEventListener('keyup', handleKeyUp);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      window.removeEventListener('keyup', handleKeyUp);
    };
  }, []);

  // Initialize and synchronize WebGL Hardware Acceleration Renderer
  useEffect(() => {
    if (webglCanvasRef.current) {
      webglRenderer.init(webglCanvasRef.current);
    }
  }, []);

  useEffect(() => {
    if (containerRef.current) {
      const rect = containerRef.current.getBoundingClientRect();
      webglRenderer.render(elements, viewport, rect.width, rect.height);
    }
  }, [elements, viewport]);

  // Non-passive wheel event listener to prevent entire browser page from zooming
  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const onWheelNative = (e: WheelEvent) => {
      // PREVENT BROWSER DEFAULT ZOOM: Stops whole page/UI from zooming
      e.preventDefault();
      e.stopPropagation();

      if (e.ctrlKey || e.metaKey) {
        // Smooth exponential zoom toward cursor position
        const zoomDelta = -e.deltaY * 0.0035;
        const zoomFactor = Math.exp(zoomDelta);
        const rect = container.getBoundingClientRect();
        const mouseX = e.clientX - rect.left;
        const mouseY = e.clientY - rect.top;

        setViewport((prev) => {
          const newZoom = Math.min(32.0, Math.max(0.02, prev.zoom * zoomFactor));
          const newX = mouseX - (mouseX - prev.x) * (newZoom / prev.zoom);
          const newY = mouseY - (mouseY - prev.y) * (newZoom / prev.zoom);
          return { x: newX, y: newY, zoom: newZoom };
        });
      } else {
        // Smooth 2D canvas pan
        const dx = e.shiftKey ? e.deltaY : e.deltaX;
        const dy = e.shiftKey ? 0 : e.deltaY;
        setViewport((prev) => ({
          ...prev,
          x: prev.x - dx,
          y: prev.y - dy,
        }));
      }
    };

    container.addEventListener('wheel', onWheelNative, { passive: false });
    return () => container.removeEventListener('wheel', onWheelNative);
  }, [setViewport]);

  // Window-level guard: never allow browser page zoom when hovering canvas
  useEffect(() => {
    const onWindowWheel = (e: WheelEvent) => {
      if ((e.ctrlKey || e.metaKey) && containerRef.current?.contains(e.target as Node)) {
        e.preventDefault();
      }
    };
    window.addEventListener('wheel', onWindowWheel, { passive: false });
    return () => window.removeEventListener('wheel', onWindowWheel);
  }, []);

  // Global window listener for corner radius node dragging to guarantee 100% smooth tracking without dropping
  useEffect(() => {
    const onGlobalMouseMove = (e: MouseEvent) => {
      if (!dragRadiusRef.current) return;
      const state = dragRadiusRef.current;
      const coords = toCanvasCoords(e.clientX, e.clientY);
      const dx = coords.x - state.startX;
      const dy = coords.y - state.startY;

      let localDx = dx;
      let localDy = dy;
      if (state.rotation) {
        const rad = (-state.rotation * Math.PI) / 180;
        localDx = dx * Math.cos(rad) - dy * Math.sin(rad);
        localDy = dx * Math.sin(rad) + dy * Math.cos(rad);
      }

      let delta = 0;
      if (state.corner === 'tl') {
        delta = (localDx + localDy) / 2;
      } else if (state.corner === 'tr') {
        delta = (-localDx + localDy) / 2;
      } else if (state.corner === 'br') {
        delta = (-localDx - localDy) / 2;
      } else if (state.corner === 'bl') {
        delta = (localDx - localDy) / 2;
      }

      const maxR = Math.round(Math.min(state.width, state.height) / 2);
      const newRadius = Math.max(0, Math.min(maxR, Math.round(state.initialRadius + delta)));
      setRadiusDragValue(newRadius);
      onUpdateElement(state.elementId, { cornerRadius: newRadius });
    };

    const onGlobalMouseUp = () => {
      if (dragRadiusRef.current) {
        dragRadiusRef.current = null;
        setDragMode('none');
        setActiveCorner(null);
        setRadiusDragValue(null);
      }
    };

    window.addEventListener('mousemove', onGlobalMouseMove);
    window.addEventListener('mouseup', onGlobalMouseUp);
    return () => {
      window.removeEventListener('mousemove', onGlobalMouseMove);
      window.removeEventListener('mouseup', onGlobalMouseUp);
    };
  }, [toCanvasCoords, onUpdateElement]);

  // Primary Mouse Down
  const handleMouseDown = (e: React.MouseEvent) => {
    if (e.button === 1 || isSpacePressed || currentTool === 'hand') {
      // Middle click or space pan
      setDragMode('pan');
      setDragStart({ x: e.clientX, y: e.clientY });
      return;
    }

    if (e.button !== 0) return;

    const coords = toCanvasCoords(e.clientX, e.clientY);
    setDragStart({ x: coords.x, y: coords.y });

    // Pen tool
    if (currentTool === 'pen') {
      if (penNodes.length >= 2) {
        const startNode = penNodes[0];
        const dist = Math.hypot(coords.x - startNode.x, coords.y - startNode.y);
        if (dist < 14 / viewport.zoom) {
          finishPenPath(true);
          return;
        }
      }
      const newNode: PenNode = {
        x: coords.x,
        y: coords.y,
        cp1: { x: coords.x, y: coords.y },
        cp2: { x: coords.x, y: coords.y },
      };
      setPenNodes((prev) => [...prev, newNode]);
      setDragMode('pen');
      setIsDraggingPenHandle(true);
      return;
    }

    // Creation tools
    if (['rect', 'ellipse', 'frame', 'line', 'arrow', 'star', 'polygon'].includes(currentTool)) {
      setDragMode('create');
      setCreatePreview({ x: coords.x, y: coords.y, w: 0, h: 0 });
      return;
    }

    if (currentTool === 'pencil') {
      setDragMode('pencil');
      setPencilPoints([{ x: coords.x, y: coords.y }]);
      return;
    }

    if (currentTool === 'text') {
      const newTextElem: FigmaElement = {
        id: `text-${Date.now()}`,
        name: 'Text Label',
        type: 'text',
        x: Math.round(coords.x),
        y: Math.round(coords.y),
        width: 140,
        height: 28,
        rotation: 0,
        opacity: 1,
        locked: false,
        visible: true,
        fill: '#FFFFFF',
        stroke: 'transparent',
        strokeWidth: 0,
        strokeStyle: 'solid',
        cornerRadius: 0,
        effects: [],
        text: 'Type your text',
        fontSize: 16,
        fontFamily: 'Inter',
        fontWeight: 400,
      };
      onAddElement(newTextElem);
      onSelect(newTextElem.id);
      setCurrentTool('select');
      return;
    }

    // Check if clicked an element directly (reversed for topmost click detection)
    const reversed = [...elements].reverse();
    const hit = reversed.find((el) => {
      if (!el.visible || el.locked) return false;
      const pad = el.type === 'line' || el.type === 'arrow' || el.type === 'pen' || el.type === 'pencil' ? 8 : 0;
      return (
        coords.x >= el.x - pad &&
        coords.x <= el.x + el.width + pad &&
        coords.y >= el.y - pad &&
        coords.y <= el.y + el.height + pad
      );
    });

    if (hit) {
      if (!selectedIds.includes(hit.id)) {
        onSelect(hit.id, e.shiftKey || e.metaKey || e.ctrlKey);
      }
      setDragMode('move');
      // Record initial positions for smooth dragging
      const bounds: Record<
        string,
        { x: number; y: number; width: number; height: number; rotation: number }
      > = {};
      const targetIds = selectedIds.includes(hit.id) ? selectedIds : [hit.id];

      // Recursively include all descendants so frames and groups move their children along smoothly
      const movingIds = new Set<string>(targetIds);
      const addDescendants = (parentIds: string[]) => {
        const children = elements.filter(
          (el) => el.parentId && parentIds.includes(el.parentId) && !movingIds.has(el.id)
        );
        if (children.length > 0) {
          children.forEach((c) => movingIds.add(c.id));
          addDescendants(children.map((c) => c.id));
        }
      };
      addDescendants(targetIds);

      elements
        .filter((el) => movingIds.has(el.id))
        .forEach((el) => {
          bounds[el.id] = {
            x: el.x,
            y: el.y,
            width: el.width,
            height: el.height,
            rotation: el.rotation,
          };
        });
      setInitialElemBounds(bounds);
    } else {
      // Clicked on empty canvas -> Rubberband selection box
      if (!e.shiftKey) {
        onClearSelection();
      }
      setDragMode('select-box');
      setSelectionBox({ x: coords.x, y: coords.y, w: 0, h: 0 });
    }
  };

  // Mouse Move
  const handleMouseMove = (e: React.MouseEvent) => {
    const coords = toCanvasCoords(e.clientX, e.clientY);
    setMouseCanvasPos(coords);

    // Pen tool tracking and Bezier curve handle dragging
    if (currentTool === 'pen') {
      setPenCurrentPos(coords);
      if (isDraggingPenHandle && penNodes.length > 0) {
        setPenNodes((prev) => {
          if (prev.length === 0) return prev;
          const next = [...prev];
          const lastIdx = next.length - 1;
          const node = next[lastIdx];
          const cp2 = { x: coords.x, y: coords.y };
          const cp1 = { x: 2 * node.x - coords.x, y: 2 * node.y - coords.y };
          next[lastIdx] = { ...node, cp1, cp2 };
          return next;
        });
      }
    }

    if (dragMode === 'pan') {
      const dx = e.clientX - dragStart.x;
      const dy = e.clientY - dragStart.y;
      setViewport((prev) => ({
        ...prev,
        x: prev.x + dx,
        y: prev.y + dy,
      }));
      setDragStart({ x: e.clientX, y: e.clientY });
      return;
    }

    if (dragMode === 'move') {
      const rawDx = coords.x - dragStart.x;
      const rawDy = coords.y - dragStart.y;

      const targetIds = Object.keys(initialElemBounds);
      if (targetIds.length === 0) return;

      const snapDist = 7 / viewport.zoom;
      const guides: SnapGuide[] = [];

      let snappedDx = rawDx;
      let snappedDy = rawDy;

      // Smart Snapping & Guides (holding Ctrl/Cmd temporarily disables snap)
      if (!e.ctrlKey && !e.metaKey) {
        const minOrigX = Math.min(...targetIds.map((id) => initialElemBounds[id].x));
        const minOrigY = Math.min(...targetIds.map((id) => initialElemBounds[id].y));
        const maxOrigX = Math.max(...targetIds.map((id) => initialElemBounds[id].x + initialElemBounds[id].width));
        const maxOrigY = Math.max(...targetIds.map((id) => initialElemBounds[id].y + initialElemBounds[id].height));
        const totalW = maxOrigX - minOrigX;
        const totalH = maxOrigY - minOrigY;

        const currentLeft = minOrigX + rawDx;
        const currentCenterX = currentLeft + totalW / 2;
        const currentRight = currentLeft + totalW;

        const currentTop = minOrigY + rawDy;
        const currentCenterY = currentTop + totalH / 2;
        const currentBottom = currentTop + totalH;

        const xTargets: number[] = [0];
        const yTargets: number[] = [0];

        if (canvasSettings?.fixedWidth) {
          xTargets.push(canvasSettings.fixedWidth / 2, canvasSettings.fixedWidth);
        }
        if (canvasSettings?.fixedHeight) {
          yTargets.push(canvasSettings.fixedHeight / 2, canvasSettings.fixedHeight);
        }

        elements.forEach((other) => {
          if (targetIds.includes(other.id) || !other.visible) return;
          xTargets.push(other.x, other.x + other.width / 2, other.x + other.width);
          yTargets.push(other.y, other.y + other.height / 2, other.y + other.height);
        });

        let bestDiffX = snapDist;
        for (const targetX of xTargets) {
          if (Math.abs(currentLeft - targetX) < bestDiffX) {
            bestDiffX = Math.abs(currentLeft - targetX);
            snappedDx = rawDx + (targetX - currentLeft);
            guides.length = guides.filter((g) => g.axis !== 'vertical').length;
            guides.push({ id: `snap-x-${targetX}`, axis: 'vertical', pos: targetX });
          }
          if (Math.abs(currentCenterX - targetX) < bestDiffX) {
            bestDiffX = Math.abs(currentCenterX - targetX);
            snappedDx = rawDx + (targetX - currentCenterX);
            guides.length = guides.filter((g) => g.axis !== 'vertical').length;
            guides.push({ id: `snap-x-c-${targetX}`, axis: 'vertical', pos: targetX });
          }
          if (Math.abs(currentRight - targetX) < bestDiffX) {
            bestDiffX = Math.abs(currentRight - targetX);
            snappedDx = rawDx + (targetX - currentRight);
            guides.length = guides.filter((g) => g.axis !== 'vertical').length;
            guides.push({ id: `snap-x-r-${targetX}`, axis: 'vertical', pos: targetX });
          }
        }

        let bestDiffY = snapDist;
        for (const targetY of yTargets) {
          if (Math.abs(currentTop - targetY) < bestDiffY) {
            bestDiffY = Math.abs(currentTop - targetY);
            snappedDy = rawDy + (targetY - currentTop);
            guides.length = guides.filter((g) => g.axis !== 'horizontal').length;
            guides.push({ id: `snap-y-${targetY}`, axis: 'horizontal', pos: targetY });
          }
          if (Math.abs(currentCenterY - targetY) < bestDiffY) {
            bestDiffY = Math.abs(currentCenterY - targetY);
            snappedDy = rawDy + (targetY - currentCenterY);
            guides.length = guides.filter((g) => g.axis !== 'horizontal').length;
            guides.push({ id: `snap-y-c-${targetY}`, axis: 'horizontal', pos: targetY });
          }
          if (Math.abs(currentBottom - targetY) < bestDiffY) {
            bestDiffY = Math.abs(currentBottom - targetY);
            snappedDy = rawDy + (targetY - currentBottom);
            guides.length = guides.filter((g) => g.axis !== 'horizontal').length;
            guides.push({ id: `snap-y-b-${targetY}`, axis: 'horizontal', pos: targetY });
          }
        }
      }

      setActiveSnapGuides(guides);

      Object.entries(initialElemBounds).forEach(([id, bounds]) => {
        onUpdateElement(id, {
          x: Math.round(bounds.x + snappedDx),
          y: Math.round(bounds.y + snappedDy),
        });
      });
      return;
    }

    if (dragMode === 'resize' && activeHandle && selectedIds.length === 1) {
      const id = selectedIds[0];
      const initial = initialElemBounds[id];
      if (!initial) return;

      const dx = coords.x - dragStart.x;
      const dy = coords.y - dragStart.y;
      let newX = initial.x;
      let newY = initial.y;
      let newW = initial.width;
      let newH = initial.height;

      if (activeHandle.includes('e')) newW = Math.max(10, initial.width + dx);
      if (activeHandle.includes('s')) newH = Math.max(10, initial.height + dy);
      if (activeHandle.includes('w')) {
        const potentialW = initial.width - dx;
        if (potentialW > 10) {
          newW = potentialW;
          newX = initial.x + dx;
        }
      }
      if (activeHandle.includes('n')) {
        const potentialH = initial.height - dy;
        if (potentialH > 10) {
          newH = potentialH;
          newY = initial.y + dy;
        }
      }

      onUpdateElement(id, {
        x: Math.round(newX),
        y: Math.round(newY),
        width: Math.round(newW),
        height: Math.round(newH),
      });
      return;
    }

    if (dragMode === 'rotate' && selectedIds.length === 1) {
      const id = selectedIds[0];
      const initial = initialElemBounds[id];
      if (!initial) return;

      const centerX = initial.x + initial.width / 2;
      const centerY = initial.y + initial.height / 2;
      const angle = (Math.atan2(coords.y - centerY, coords.x - centerX) * 180) / Math.PI - 90;
      onUpdateElement(id, { rotation: Math.round(angle) });
      return;
    }

    if (dragMode === 'corner-radius' && activeCorner && selectedIds.length === 1) {
      const id = selectedIds[0];
      const el = elements.find((item) => item.id === id);
      if (!el) return;

      const maxR = Math.round(Math.min(el.width, el.height) / 2);

      // Delta from drag start
      const dx = coords.x - dragStart.x;
      const dy = coords.y - dragStart.y;

      let localDx = dx;
      let localDy = dy;
      if (el.rotation) {
        const rad = (-el.rotation * Math.PI) / 180;
        localDx = dx * Math.cos(rad) - dy * Math.sin(rad);
        localDy = dx * Math.sin(rad) + dy * Math.cos(rad);
      }

      let delta = 0;
      if (activeCorner === 'tl') {
        delta = (localDx + localDy) / 2;
      } else if (activeCorner === 'tr') {
        delta = (-localDx + localDy) / 2;
      } else if (activeCorner === 'br') {
        delta = (-localDx - localDy) / 2;
      } else if (activeCorner === 'bl') {
        delta = (localDx - localDy) / 2;
      }

      const newRadius = Math.max(0, Math.min(maxR, Math.round(initialCornerRadius + delta)));
      setRadiusDragValue(newRadius);
      onUpdateElement(id, { cornerRadius: newRadius });
      return;
    }

    if (dragMode === 'create' && createPreview) {
      const w = coords.x - dragStart.x;
      const h = coords.y - dragStart.y;
      setCreatePreview({
        x: w < 0 ? coords.x : dragStart.x,
        y: h < 0 ? coords.y : dragStart.y,
        w: Math.abs(w),
        h: Math.abs(h),
      });
      return;
    }

    if (dragMode === 'pencil') {
      setPencilPoints((prev) => [...prev, { x: coords.x, y: coords.y }]);
      return;
    }

    if (dragMode === 'select-box' && selectionBox) {
      const w = coords.x - dragStart.x;
      const h = coords.y - dragStart.y;
      const box = {
        x: w < 0 ? coords.x : dragStart.x,
        y: h < 0 ? coords.y : dragStart.y,
        w: Math.abs(w),
        h: Math.abs(h),
      };
      setSelectionBox(box);

      // Select elements inside box
      const matched = elements
        .filter((el) => el.visible && !el.locked)
        .filter(
          (el) =>
            el.x + el.width >= box.x &&
            el.x <= box.x + box.w &&
            el.y + el.height >= box.y &&
            el.y <= box.y + box.h
        )
        .map((el) => el.id);

      matched.forEach((id) => onSelect(id, true));
    }
  };

  // Mouse Up
  const handleMouseUp = () => {
    if (dragMode === 'create' && createPreview) {
      if (createPreview.w > 5 && createPreview.h > 5) {
        const isFrame = currentTool === 'frame';
        const newElem: FigmaElement = {
          id: `${currentTool}-${Date.now()}`,
          name: `${currentTool.charAt(0).toUpperCase() + currentTool.slice(1)} ${elements.length + 1}`,
          type: isFrame ? 'frame' : (currentTool as any),
          x: Math.round(createPreview.x),
          y: Math.round(createPreview.y),
          width: Math.round(createPreview.w),
          height: Math.round(createPreview.h),
          rotation: 0,
          opacity: 1,
          locked: false,
          visible: true,
          fill: isFrame ? '#141720' : '#0D99FF',
          stroke: isFrame ? '#282F3E' : 'transparent',
          strokeWidth: isFrame ? 1 : 0,
          strokeStyle: 'solid',
          cornerRadius: isFrame ? 16 : 0,
          effects: [],
          clipContent: isFrame,
        };
        onAddElement(newElem);
        onSelect(newElem.id);
      }
      setCreatePreview(null);
      setCurrentTool('select');
    }

    if (dragMode === 'pencil' && pencilPoints.length > 2) {
      const minX = Math.min(...pencilPoints.map((p) => p.x));
      const minY = Math.min(...pencilPoints.map((p) => p.y));
      const maxX = Math.max(...pencilPoints.map((p) => p.x));
      const maxY = Math.max(...pencilPoints.map((p) => p.y));

      const pathData =
        `M ${Math.round(pencilPoints[0].x - minX)} ${Math.round(pencilPoints[0].y - minY)} ` +
        pencilPoints.slice(1).map((p) => `L ${Math.round(p.x - minX)} ${Math.round(p.y - minY)}`).join(' ');

      const newPencilElem: FigmaElement = {
        id: `pencil-${Date.now()}`,
        name: `Drawing ${elements.length + 1}`,
        type: 'pencil',
        x: Math.round(minX),
        y: Math.round(minY),
        width: Math.max(10, Math.round(maxX - minX)),
        height: Math.max(10, Math.round(maxY - minY)),
        rotation: 0,
        opacity: 1,
        locked: false,
        visible: true,
        fill: 'transparent',
        stroke: '#0D99FF',
        strokeWidth: 2,
        strokeStyle: 'solid',
        cornerRadius: 0,
        effects: [],
        pathData,
        points: pencilPoints.map((p) => ({ x: Math.round(p.x - minX), y: Math.round(p.y - minY) })),
      };
      onAddElement(newPencilElem);
      onSelect(newPencilElem.id);
      setPencilPoints([]);
      setCurrentTool('select');
    }

    if (dragMode === 'pen') {
      setIsDraggingPenHandle(false);
      setDragMode('none');
      return;
    }

    setActiveSnapGuides([]);
    setDragMode('none');
    setActiveHandle(null);
    setActiveCorner(null);
    setRadiusDragValue(null);
    setSelectionBox(null);
  };

  // Finish and commit Pen vector path
  const finishPenPath = useCallback(
    (close = false) => {
      if (penNodes.length < 2) {
        setPenNodes([]);
        setPenCurrentPos(null);
        setIsDraggingPenHandle(false);
        return;
      }

      const minX = Math.min(...penNodes.map((n) => n.x));
      const minY = Math.min(...penNodes.map((n) => n.y));
      const maxX = Math.max(...penNodes.map((n) => n.x));
      const maxY = Math.max(...penNodes.map((n) => n.y));

      let pathData = `M ${Math.round(penNodes[0].x - minX)} ${Math.round(penNodes[0].y - minY)}`;
      for (let i = 1; i < penNodes.length; i++) {
        const prev = penNodes[i - 1];
        const curr = penNodes[i];
        const hasCurves =
          (prev.cp2 && (Math.abs(prev.cp2.x - prev.x) > 1 || Math.abs(prev.cp2.y - prev.y) > 1)) ||
          (curr.cp1 && (Math.abs(curr.cp1.x - curr.x) > 1 || Math.abs(curr.cp1.y - curr.y) > 1));

        if (hasCurves && prev.cp2 && curr.cp1) {
          pathData += ` C ${Math.round(prev.cp2.x - minX)} ${Math.round(prev.cp2.y - minY)}, ${Math.round(curr.cp1.x - minX)} ${Math.round(curr.cp1.y - minY)}, ${Math.round(curr.x - minX)} ${Math.round(curr.y - minY)}`;
        } else {
          pathData += ` L ${Math.round(curr.x - minX)} ${Math.round(curr.y - minY)}`;
        }
      }

      if (close) {
        pathData += ' Z';
      }

      const newPenElem: FigmaElement = {
        id: `pen-${Date.now()}`,
        name: `Vector ${elements.length + 1}`,
        type: 'pen',
        x: Math.round(minX),
        y: Math.round(minY),
        width: Math.max(10, Math.round(maxX - minX)),
        height: Math.max(10, Math.round(maxY - minY)),
        rotation: 0,
        opacity: 1,
        locked: false,
        visible: true,
        fill: close ? 'rgba(13, 153, 255, 0.15)' : 'none',
        stroke: '#0D99FF',
        strokeWidth: 2,
        strokeStyle: 'solid',
        cornerRadius: 0,
        effects: [],
        pathData,
        points: penNodes.map((n) => ({ x: Math.round(n.x - minX), y: Math.round(n.y - minY) })),
      };

      onAddElement(newPenElem);
      onSelect(newPenElem.id);
      setPenNodes([]);
      setPenCurrentPos(null);
      setIsDraggingPenHandle(false);
      setCurrentTool('select');
    },
    [penNodes, elements.length, onAddElement, onSelect, setCurrentTool]
  );

  // Start Resizing Handle
  const handleStartResize = (e: React.MouseEvent, handle: ResizeHandle) => {
    e.stopPropagation();
    const coords = toCanvasCoords(e.clientX, e.clientY);
    setDragMode('resize');
    setActiveHandle(handle);
    setDragStart({ x: coords.x, y: coords.y });

    const bounds: Record<string, any> = {};
    elements
      .filter((el) => selectedIds.includes(el.id))
      .forEach((el) => {
        bounds[el.id] = {
          x: el.x,
          y: el.y,
          width: el.width,
          height: el.height,
          rotation: el.rotation,
        };
      });
    setInitialElemBounds(bounds);
  };

  // Start Rotation Handle
  const handleStartRotate = (e: React.MouseEvent) => {
    e.stopPropagation();
    const coords = toCanvasCoords(e.clientX, e.clientY);
    setDragMode('rotate');
    setDragStart({ x: coords.x, y: coords.y });

    const bounds: Record<string, any> = {};
    elements
      .filter((el) => selectedIds.includes(el.id))
      .forEach((el) => {
        bounds[el.id] = {
          x: el.x,
          y: el.y,
          width: el.width,
          height: el.height,
          rotation: el.rotation,
        };
      });
    setInitialElemBounds(bounds);
  };

  // Start Corner Radius Handle
  const handleStartCornerRadius = (e: React.MouseEvent, corner: CornerHandle) => {
    e.stopPropagation();
    e.preventDefault();
    const coords = toCanvasCoords(e.clientX, e.clientY);
    setDragMode('corner-radius');
    setActiveCorner(corner);
    setDragStart({ x: coords.x, y: coords.y });
    if (selectedIds.length === 1) {
      const el = elements.find((item) => item.id === selectedIds[0]);
      if (el) {
        setInitialCornerRadius(el.cornerRadius || 0);
        setRadiusDragValue(el.cornerRadius || 0);
        dragRadiusRef.current = {
          corner,
          startX: coords.x,
          startY: coords.y,
          initialRadius: el.cornerRadius || 0,
          elementId: el.id,
          width: el.width,
          height: el.height,
          rotation: el.rotation || 0,
        };
      }
    }
  };

  // Compute selected bounding box
  const selectedElements = elements.filter((el) => selectedIds.includes(el.id));
  let boundingBox: { x: number; y: number; w: number; h: number; rot: number } | null = null;
  if (selectedElements.length === 1) {
    const el = selectedElements[0];
    boundingBox = { x: el.x, y: el.y, w: el.width, h: el.height, rot: el.rotation };
  } else if (selectedElements.length > 1) {
    const minX = Math.min(...selectedElements.map((e) => e.x));
    const minY = Math.min(...selectedElements.map((e) => e.y));
    const maxX = Math.max(...selectedElements.map((e) => e.x + e.width));
    const maxY = Math.max(...selectedElements.map((e) => e.y + e.height));
    boundingBox = { x: minX, y: minY, w: maxX - minX, h: maxY - minY, rot: 0 };
  }

  // Dynamic Custom Canvas Background & Grid Styling
  const customBg = canvasSettings?.backgroundColor || '#1E1E1E';
  const customGridType = canvasSettings?.gridType || (showGrid ? 'dots' : 'none');
  const customGridSize = (canvasSettings?.gridSize || 20) * viewport.zoom;
  const isLightBg = customBg === '#F8F9FA' || customBg === '#F5F2EB';
  const customGridColor = canvasSettings?.gridColor || (isLightBg ? 'rgba(0,0,0,0.08)' : 'rgba(255,255,255,0.12)');

  let dynamicBgImage = 'none';
  let dynamicBgSize = 'auto';

  if (customGridType === 'dots') {
    dynamicBgImage = `radial-gradient(${customGridColor} 1.2px, transparent 1.2px)`;
    dynamicBgSize = `${customGridSize}px ${customGridSize}px`;
  } else if (customGridType === 'lines') {
    dynamicBgImage = `linear-gradient(to right, ${customGridColor} 1px, transparent 1px), linear-gradient(to bottom, ${customGridColor} 1px, transparent 1px)`;
    dynamicBgSize = `${customGridSize}px ${customGridSize}px`;
  } else if (customGridType === 'isometric') {
    dynamicBgImage = `linear-gradient(30deg, ${customGridColor} 1px, transparent 1px), linear-gradient(150deg, ${customGridColor} 1px, transparent 1px)`;
    dynamicBgSize = `${customGridSize * 1.732}px ${customGridSize}px`;
  }

  return (
    <div
      ref={containerRef}
      className={`canvas-container ${
        isSpacePressed || currentTool === 'hand'
          ? 'tool-hand'
          : currentTool !== 'select'
          ? 'tool-draw'
          : ''
      }`}
      onMouseDown={handleMouseDown}
      onMouseMove={handleMouseMove}
      onMouseUp={handleMouseUp}
      onContextMenu={(e) => {
        e.preventDefault();
        if (onContextMenu) onContextMenu(e, null);
      }}
      style={{
        backgroundColor: customBg,
        backgroundImage: dynamicBgImage,
        backgroundSize: dynamicBgSize,
        backgroundPosition: `${viewport.x}px ${viewport.y}px`,
      }}
    >
      {/* WebGL Hardware Rasterization Layer */}
      <canvas
        ref={webglCanvasRef}
        style={{
          position: 'absolute',
          left: 0,
          top: 0,
          width: '100%',
          height: '100%',
          pointerEvents: 'none',
          zIndex: 0,
        }}
      />

      {/* Transformed Canvas World Container */}
      <div
        style={{
          position: 'absolute',
          left: 0,
          top: 0,
          transform: `translate(${viewport.x}px, ${viewport.y}px) scale(${viewport.zoom})`,
          transformOrigin: '0 0',
          width: '100%',
          height: '100%',
          pointerEvents: 'none',
        }}
      >
        <svg
          style={{
            position: 'absolute',
            left: 0,
            top: 0,
            overflow: 'visible',
            width: '1px',
            height: '1px',
            pointerEvents: 'none',
          }}
        >
          <defs>
            {/* Gradients */}
            {elements
              .filter((e) => e.fillType === 'linear-gradient' && e.fillGradient)
              .map((e) => (
                <linearGradient
                  key={e.id}
                  id={`canvas-grad-${e.id}`}
                  x1="0%"
                  y1="0%"
                  x2="100%"
                  y2="100%"
                >
                  <stop offset="0%" stopColor={e.fillGradient?.start || '#000'} />
                  <stop offset="100%" stopColor={e.fillGradient?.end || '#fff'} />
                </linearGradient>
              ))}

            {/* Prototype Connector Arrow Marker */}
            <marker
              id="proto-arrow"
              viewBox="0 0 10 10"
              refX="6"
              refY="5"
              markerWidth="6"
              markerHeight="6"
              orient="auto-start-reverse"
            >
              <path d="M 0 1 L 8 5 L 0 9 z" fill="#0D99FF" />
            </marker>
          </defs>

          {/* Custom Artboard Boundaries */}
          {canvasSettings?.preset && canvasSettings.preset !== 'infinite' && (
            <g pointerEvents="none">
              <rect
                x={0}
                y={0}
                width={canvasSettings.fixedWidth || 1440}
                height={canvasSettings.fixedHeight || 900}
                fill="transparent"
                stroke="rgba(13, 153, 255, 0.45)"
                strokeWidth={2 / viewport.zoom}
                strokeDasharray="6,4"
              />
              <text
                x={0}
                y={-8 / viewport.zoom}
                fill="#0D99FF"
                fontSize={12 / viewport.zoom}
                fontFamily="Inter"
                fontWeight="600"
              >
                {canvasSettings.preset.toUpperCase()} ({canvasSettings.fixedWidth || 1440} × {canvasSettings.fixedHeight || 900}px)
              </text>
            </g>
          )}

          {/* Alignment Guide Lines */}
          {canvasSettings?.showGuides && guideLines.map((g) => (
            <line
              key={g.id}
              x1={g.axis === 'vertical' ? g.position : -100000}
              y1={g.axis === 'horizontal' ? g.position : -100000}
              x2={g.axis === 'vertical' ? g.position : 100000}
              y2={g.axis === 'horizontal' ? g.position : 100000}
              stroke="#0D99FF"
              strokeWidth={1 / viewport.zoom}
              strokeDasharray="4,4"
              pointerEvents="none"
            />
          ))}

          {/* Dynamic Smart Snapping Alignment Guides */}
          {activeSnapGuides.map((g) => (
            <g key={g.id} pointerEvents="none">
              <line
                x1={g.axis === 'vertical' ? g.pos : -100000}
                y1={g.axis === 'horizontal' ? g.pos : -100000}
                x2={g.axis === 'vertical' ? g.pos : 100000}
                y2={g.axis === 'horizontal' ? g.pos : 100000}
                stroke="#FF385C"
                strokeWidth={1.5 / viewport.zoom}
                strokeDasharray="4,3"
              />
              <circle
                cx={g.axis === 'vertical' ? g.pos : mouseCanvasPos.x}
                cy={g.axis === 'horizontal' ? g.pos : mouseCanvasPos.y}
                r={3.5 / viewport.zoom}
                fill="#FF385C"
                stroke="#FFFFFF"
                strokeWidth={1 / viewport.zoom}
              />
            </g>
          ))}

          {/* Render Elements */}
          {elements.map((elem) => {
            if (!elem.visible) return null;

            const fillValue =
              elem.fillType === 'linear-gradient'
                ? `url(#canvas-grad-${elem.id})`
                : elem.fill;
            const rot = elem.rotation
              ? `rotate(${elem.rotation} ${elem.x + elem.width / 2} ${elem.y + elem.height / 2})`
              : undefined;

            // Frame label
            const isFrame = elem.type === 'frame';

            return (
              <g
                key={elem.id}
                transform={rot}
                opacity={elem.opacity}
                style={{ pointerEvents: 'auto' }}
                onContextMenu={(e) => {
                  e.preventDefault();
                  e.stopPropagation();
                  onSelect(elem.id, false);
                  if (onContextMenu) onContextMenu(e, elem.id);
                }}
              >
                {/* Frame Name Tag at top */}
                {isFrame && (
                  <text
                    x={elem.x}
                    y={elem.y - 8}
                    fill={elem.isAutoLayout ? '#0D99FF' : '#9BA1B0'}
                    fontSize="11"
                    fontFamily="Inter"
                    fontWeight="600"
                  >
                    {elem.isAutoLayout ? `❖ ${elem.name} (${elem.layoutMode || 'horizontal'})` : elem.name}
                  </text>
                )}

                {/* Auto Layout padding indicator when selected */}
                {elem.isAutoLayout && selectedIds.includes(elem.id) && (
                  <rect
                    x={elem.x + (elem.paddingLeft ?? 16)}
                    y={elem.y + (elem.paddingTop ?? 16)}
                    width={Math.max(0, elem.width - (elem.paddingLeft ?? 16) - (elem.paddingRight ?? 16))}
                    height={Math.max(0, elem.height - (elem.paddingTop ?? 16) - (elem.paddingBottom ?? 16))}
                    fill="none"
                    stroke="#0D99FF"
                    strokeWidth={1}
                    strokeDasharray="4,4"
                    opacity={0.4}
                    style={{ pointerEvents: 'none' }}
                  />
                )}

                {/* Rectangle / Frame */}
                {(elem.type === 'rect' || elem.type === 'frame') && (
                  <rect
                    x={elem.x}
                    y={elem.y}
                    width={elem.width}
                    height={elem.height}
                    rx={elem.cornerRadius || 0}
                    ry={elem.cornerRadius || 0}
                    fill={fillValue}
                    stroke={elem.stroke}
                    strokeWidth={elem.strokeWidth}
                    strokeDasharray={
                      elem.strokeStyle === 'dashed'
                        ? '6,6'
                        : elem.strokeStyle === 'dotted'
                        ? '2,4'
                        : undefined
                    }
                  />
                )}

                {/* Ellipse */}
                {elem.type === 'ellipse' && (
                  <ellipse
                    cx={elem.x + elem.width / 2}
                    cy={elem.y + elem.height / 2}
                    rx={elem.width / 2}
                    ry={elem.height / 2}
                    fill={fillValue}
                    stroke={elem.stroke}
                    strokeWidth={elem.strokeWidth}
                  />
                )}

                {/* Star */}
                {elem.type === 'star' && (
                  <polygon
                    points={`
                      ${elem.x + elem.width * 0.5},${elem.y}
                      ${elem.x + elem.width * 0.62},${elem.y + elem.height * 0.38}
                      ${elem.x + elem.width},${elem.y + elem.height * 0.38}
                      ${elem.x + elem.width * 0.69},${elem.y + elem.height * 0.62}
                      ${elem.x + elem.width * 0.81},${elem.y + elem.height}
                      ${elem.x + elem.width * 0.5},${elem.y + elem.height * 0.77}
                      ${elem.x + elem.width * 0.19},${elem.y + elem.height}
                      ${elem.x + elem.width * 0.31},${elem.y + elem.height * 0.62}
                      ${elem.x},${elem.y + elem.height * 0.38}
                      ${elem.x + elem.width * 0.38},${elem.y + elem.height * 0.38}
                    `}
                    fill={fillValue}
                    stroke={elem.stroke}
                    strokeWidth={elem.strokeWidth}
                  />
                )}

                {/* Polygon / Triangle */}
                {elem.type === 'polygon' && (
                  <polygon
                    points={`
                      ${elem.x + elem.width / 2},${elem.y}
                      ${elem.x + elem.width},${elem.y + elem.height}
                      ${elem.x},${elem.y + elem.height}
                    `}
                    fill={fillValue}
                    stroke={elem.stroke}
                    strokeWidth={elem.strokeWidth}
                  />
                )}

                {/* Line / Arrow */}
                {(elem.type === 'line' || elem.type === 'arrow') && (
                  <line
                    x1={elem.x}
                    y1={elem.y}
                    x2={elem.x + elem.width}
                    y2={elem.y + elem.height}
                    stroke={elem.stroke || elem.fill}
                    strokeWidth={elem.strokeWidth || 2}
                    markerEnd={elem.type === 'arrow' ? 'url(#proto-arrow)' : undefined}
                  />
                )}

                {/* Pencil / Pen Vector Path */}
                {(elem.type === 'pencil' || elem.type === 'pen') && elem.pathData && (
                  <g transform={`translate(${elem.x}, ${elem.y})`}>
                    <path
                      d={getRelativePathData(elem)}
                      fill={elem.fill === 'none' ? 'none' : elem.fill}
                      stroke={elem.stroke || '#0D99FF'}
                      strokeWidth={elem.strokeWidth || 2}
                      strokeDasharray={
                        elem.strokeStyle === 'dashed'
                          ? '6,6'
                          : elem.strokeStyle === 'dotted'
                          ? '2,4'
                          : undefined
                      }
                      strokeLinecap="round"
                      strokeLinejoin="round"
                    />
                  </g>
                )}

                {/* Image */}
                {elem.type === 'image' && elem.src && (
                  <image
                    href={elem.src}
                    x={elem.x}
                    y={elem.y}
                    width={elem.width}
                    height={elem.height}
                    preserveAspectRatio="cover"
                  />
                )}

                {/* Text */}
                {elem.type === 'text' && (
                  <text
                    x={
                      elem.textAlign === 'center'
                        ? elem.x + elem.width / 2
                        : elem.textAlign === 'right'
                        ? elem.x + elem.width
                        : elem.x
                    }
                    y={elem.y + (elem.fontSize || 16)}
                    fill={elem.fill}
                    fontFamily={elem.fontFamily || 'Inter'}
                    fontSize={elem.fontSize || 16}
                    fontWeight={elem.fontWeight || 400}
                    textAnchor={
                      elem.textAlign === 'center'
                        ? 'middle'
                        : elem.textAlign === 'right'
                        ? 'end'
                        : 'start'
                    }
                    letterSpacing={elem.letterSpacing ? `${elem.letterSpacing}px` : undefined}
                  >
                    {elem.text}
                  </text>
                )}

                {/* Prototype Connection Line */}
                {elem.prototypeTargetId && (
                  (() => {
                    const target = elements.find((t) => t.id === elem.prototypeTargetId);
                    if (!target) return null;
                    const startX = elem.x + elem.width;
                    const startY = elem.y + elem.height / 2;
                    const endX = target.x;
                    const endY = target.y + target.height / 2;
                    const ctrlX1 = startX + 60;
                    const ctrlY1 = startY;
                    const ctrlX2 = endX - 60;
                    const ctrlY2 = endY;

                    return (
                      <path
                        d={`M ${startX} ${startY} C ${ctrlX1} ${ctrlY1}, ${ctrlX2} ${ctrlY2}, ${endX} ${endY}`}
                        fill="none"
                        stroke="#0D99FF"
                        strokeWidth="2"
                        strokeDasharray="5,5"
                        markerEnd="url(#proto-arrow)"
                        opacity="0.8"
                      />
                    );
                  })()
                )}
              </g>
            );
          })}

          {/* Live Freehand Pencil Preview while drawing */}
          {dragMode === 'pencil' && pencilPoints.length > 1 && (
            <path
              d={
                `M ${pencilPoints[0].x} ${pencilPoints[0].y} ` +
                pencilPoints.slice(1).map((p) => `L ${p.x} ${p.y}`).join(' ')
              }
              fill="none"
              stroke="#0D99FF"
              strokeWidth="2"
              strokeLinecap="round"
            />
          )}

          {/* Creation Preview Rect */}
          {dragMode === 'create' && createPreview && (
            <rect
              x={createPreview.x}
              y={createPreview.y}
              width={createPreview.w}
              height={createPreview.h}
              fill="rgba(13, 153, 255, 0.15)"
              stroke="#0D99FF"
              strokeWidth="1.5"
              strokeDasharray="4,4"
            />
          )}

          {/* Active Pen Tool Drawing Preview */}
          {currentTool === 'pen' && penNodes.length > 0 && (() => {
            let activePathD = `M ${Math.round(penNodes[0].x)} ${Math.round(penNodes[0].y)}`;
            for (let i = 1; i < penNodes.length; i++) {
              const prev = penNodes[i - 1];
              const curr = penNodes[i];
              const hasCurves =
                (prev.cp2 && (Math.abs(prev.cp2.x - prev.x) > 1 || Math.abs(prev.cp2.y - prev.y) > 1)) ||
                (curr.cp1 && (Math.abs(curr.cp1.x - curr.x) > 1 || Math.abs(curr.cp1.y - curr.y) > 1));

              if (hasCurves && prev.cp2 && curr.cp1) {
                activePathD += ` C ${Math.round(prev.cp2.x)} ${Math.round(prev.cp2.y)}, ${Math.round(curr.cp1.x)} ${Math.round(curr.cp1.y)}, ${Math.round(curr.x)} ${Math.round(curr.y)}`;
              } else {
                activePathD += ` L ${Math.round(curr.x)} ${Math.round(curr.y)}`;
              }
            }

            const lastNode = penNodes[penNodes.length - 1];

            return (
              <g pointerEvents="none" className="pen-drawing-preview">
                {/* Placed vector segments */}
                <path
                  d={activePathD}
                  fill="none"
                  stroke="#0D99FF"
                  strokeWidth={2 / viewport.zoom}
                  strokeLinecap="round"
                  strokeLinejoin="round"
                />

                {/* Live connecting guide to cursor position */}
                {penCurrentPos && (
                  <line
                    x1={lastNode.x}
                    y1={lastNode.y}
                    x2={penCurrentPos.x}
                    y2={penCurrentPos.y}
                    stroke="#0D99FF"
                    strokeWidth={1.5 / viewport.zoom}
                    strokeDasharray="4,4"
                  />
                )}

                {/* Placed anchor points */}
                {penNodes.map((n, idx) => (
                  <g key={idx}>
                    <rect
                      x={n.x - 4 / viewport.zoom}
                      y={n.y - 4 / viewport.zoom}
                      width={8 / viewport.zoom}
                      height={8 / viewport.zoom}
                      fill="#FFFFFF"
                      stroke="#0D99FF"
                      strokeWidth={1.5 / viewport.zoom}
                    />
                    {/* Control handles if node has Bezier curve */}
                    {n.cp1 && n.cp2 && (Math.abs(n.cp2.x - n.x) > 2 || Math.abs(n.cp2.y - n.y) > 2) && (
                      <g>
                        <line
                          x1={n.cp1.x}
                          y1={n.cp1.y}
                          x2={n.cp2.x}
                          y2={n.cp2.y}
                          stroke="#0D99FF"
                          strokeWidth={1 / viewport.zoom}
                          opacity={0.6}
                        />
                        <circle
                          cx={n.cp1.x}
                          cy={n.cp1.y}
                          r={3 / viewport.zoom}
                          fill="#0D99FF"
                        />
                        <circle
                          cx={n.cp2.x}
                          cy={n.cp2.y}
                          r={3 / viewport.zoom}
                          fill="#0D99FF"
                        />
                      </g>
                    )}
                  </g>
                ))}
              </g>
            );
          })()}

          {/* Rubberband Selection Box */}
          {dragMode === 'select-box' && selectionBox && (
            <rect
              x={selectionBox.x}
              y={selectionBox.y}
              width={selectionBox.w}
              height={selectionBox.h}
              fill="rgba(13, 153, 255, 0.1)"
              stroke="#0D99FF"
              strokeWidth="1"
            />
          )}

          {/* Interactive Bounding Box & 8 Resize Handles */}
          {boundingBox && (
            <g
              transform={
                boundingBox.rot
                  ? `rotate(${boundingBox.rot} ${boundingBox.x + boundingBox.w / 2} ${
                      boundingBox.y + boundingBox.h / 2
                    })`
                  : undefined
              }
              style={{ pointerEvents: 'auto' }}
            >
              {/* Main selection outline */}
              <rect
                x={boundingBox.x}
                y={boundingBox.y}
                width={boundingBox.w}
                height={boundingBox.h}
                fill="none"
                stroke="#0D99FF"
                strokeWidth="1.5"
                pointerEvents="none"
              />

              {/* Dimension Tooltip Badge */}
              <g transform={`translate(${boundingBox.x + boundingBox.w / 2}, ${boundingBox.y - 14})`}>
                <rect
                  x="-45"
                  y="-14"
                  width="90"
                  height="16"
                  rx="3"
                  fill="#0D99FF"
                />
                <text
                  x="0"
                  y="-3"
                  fill="#FFFFFF"
                  fontSize="10"
                  fontFamily="Inter"
                  fontWeight="600"
                  textAnchor="middle"
                >
                  {`${Math.round(boundingBox.w)} × ${Math.round(boundingBox.h)}`}
                </text>
              </g>

              {/* Rotation Handle */}
              <circle
                cx={boundingBox.x + boundingBox.w / 2}
                cy={boundingBox.y - 28}
                r="4.5"
                fill="#FFFFFF"
                stroke="#0D99FF"
                strokeWidth="1.5"
                style={{ cursor: 'grab' }}
                onMouseDown={handleStartRotate}
              />
              <line
                x1={boundingBox.x + boundingBox.w / 2}
                y1={boundingBox.y}
                x2={boundingBox.x + boundingBox.w / 2}
                y2={boundingBox.y - 24}
                stroke="#0D99FF"
                strokeWidth="1"
                strokeDasharray="2,2"
              />

              {/* 8 Resize Handles */}
              {(
                [
                  { h: 'nw', x: boundingBox.x, y: boundingBox.y, c: 'nwse-resize' },
                  { h: 'n', x: boundingBox.x + boundingBox.w / 2, y: boundingBox.y, c: 'ns-resize' },
                  { h: 'ne', x: boundingBox.x + boundingBox.w, y: boundingBox.y, c: 'nesw-resize' },
                  { h: 'e', x: boundingBox.x + boundingBox.w, y: boundingBox.y + boundingBox.h / 2, c: 'ew-resize' },
                  { h: 'se', x: boundingBox.x + boundingBox.w, y: boundingBox.y + boundingBox.h, c: 'nwse-resize' },
                  { h: 's', x: boundingBox.x + boundingBox.w / 2, y: boundingBox.y + boundingBox.h, c: 'ns-resize' },
                  { h: 'sw', x: boundingBox.x, y: boundingBox.y + boundingBox.h, c: 'nesw-resize' },
                  { h: 'w', x: boundingBox.x, y: boundingBox.y + boundingBox.h / 2, c: 'ew-resize' },
                ] as const
              ).map((handle) => (
                <rect
                  key={handle.h}
                  x={handle.x - 4}
                  y={handle.y - 4}
                  width="8"
                  height="8"
                  fill="#FFFFFF"
                  stroke="#0D99FF"
                  strokeWidth="1.5"
                  style={{ cursor: handle.c }}
                  onMouseDown={(e) => handleStartResize(e, handle.h)}
                />
              ))}

              {/* Figma-Style Corner Radius Handles (Nodes) */}
              {selectedElements.length === 1 &&
                (selectedElements[0].type === 'rect' || selectedElements[0].type === 'frame') &&
                (() => {
                  const el = selectedElements[0];
                  const r = radiusDragValue !== null ? radiusDragValue : (el.cornerRadius || 0);
                  const maxR = Math.min(boundingBox.w, boundingBox.h) / 2;
                  const minInset = Math.min(16, maxR * 0.4);
                  const inset = Math.max(minInset, Math.min(maxR - 4, r));

                  const corners: { corner: CornerHandle; cx: number; cy: number; cursor: string }[] = [
                    { corner: 'tl', cx: boundingBox.x + inset, cy: boundingBox.y + inset, cursor: 'nwse-resize' },
                    { corner: 'tr', cx: boundingBox.x + boundingBox.w - inset, cy: boundingBox.y + inset, cursor: 'nesw-resize' },
                    { corner: 'br', cx: boundingBox.x + boundingBox.w - inset, cy: boundingBox.y + boundingBox.h - inset, cursor: 'nwse-resize' },
                    { corner: 'bl', cx: boundingBox.x + inset, cy: boundingBox.y + boundingBox.h - inset, cursor: 'nesw-resize' },
                  ];

                  return (
                    <g className="figma-corner-radius-nodes">
                      {corners.map(({ corner, cx, cy, cursor }) => {
                        const isHovered = hoveredCorner === corner;
                        const isActive = (activeCorner === corner && dragMode === 'corner-radius') || dragRadiusRef.current?.corner === corner;
                        return (
                          <g key={corner}>
                            {/* Large Hitbox circle with guaranteed pointer event capture */}
                            <circle
                              cx={cx}
                              cy={cy}
                              r={14}
                              fill="#000000"
                              fillOpacity="0.001"
                              pointerEvents="all"
                              style={{ cursor }}
                              onMouseEnter={() => setHoveredCorner(corner)}
                              onMouseLeave={() => setHoveredCorner(null)}
                              onMouseDown={(e) => handleStartCornerRadius(e, corner)}
                            />
                            {/* Outer halo ring on hover or active drag */}
                            {(isHovered || isActive) && (
                              <circle
                                cx={cx}
                                cy={cy}
                                r={9}
                                fill="rgba(13, 153, 255, 0.35)"
                                pointerEvents="none"
                              />
                            )}
                            {/* Figma circular node handle */}
                            <circle
                              cx={cx}
                              cy={cy}
                              r={isHovered || isActive ? 5.2 : 4}
                              fill="#FFFFFF"
                              stroke="#0D99FF"
                              strokeWidth="1.8"
                              pointerEvents="none"
                              style={{
                                filter: 'drop-shadow(0 1px 3px rgba(0,0,0,0.5))',
                                transition: 'r 0.08s ease',
                              }}
                            />
                          </g>
                        );
                      })}

                      {/* Live Corner Radius Value Tooltip Badge */}
                      {(dragMode === 'corner-radius' || hoveredCorner) && (
                        <g transform={`translate(${boundingBox.x + boundingBox.w / 2}, ${boundingBox.y + boundingBox.h + 20})`}>
                          <rect
                            x="-42"
                            y="-11"
                            width="84"
                            height="18"
                            rx="4"
                            fill="#1E1E20"
                            stroke="#0D99FF"
                            strokeWidth="1"
                            style={{ filter: 'drop-shadow(0 2px 8px rgba(0,0,0,0.5))' }}
                          />
                          <text
                            x="0"
                            y="2"
                            fill="#FFFFFF"
                            fontSize="10"
                            fontFamily="Inter"
                            fontWeight="600"
                            textAnchor="middle"
                          >
                            {`Radius: ${radiusDragValue !== null ? radiusDragValue : r}px`}
                          </text>
                        </g>
                      )}
                    </g>
                  );
                })()}
            </g>
          )}

          {/* Alt key distance measurement guides */}
          {isAltPressed && boundingBox && (
            <g style={{ pointerEvents: 'none' }}>
              <line
                x1={boundingBox.x + boundingBox.w / 2}
                y1={boundingBox.y}
                x2={boundingBox.x + boundingBox.w / 2}
                y2={0}
                stroke="#F43F5E"
                strokeWidth="1"
                strokeDasharray="3,3"
              />
              <rect
                x={boundingBox.x + boundingBox.w / 2 - 18}
                y={boundingBox.y / 2 - 8}
                width="36"
                height="16"
                rx="3"
                fill="#F43F5E"
              />
              <text
                x={boundingBox.x + boundingBox.w / 2}
                y={boundingBox.y / 2 + 4}
                fill="#FFFFFF"
                fontSize="10"
                fontFamily="Inter"
                fontWeight="600"
                textAnchor="middle"
              >
                {Math.round(boundingBox.y)}
              </text>
            </g>
          )}

          {/* Collaborators Live Cursors */}
          {collaborators && collaborators.length > 0 && collaborators.map((c) => (
            <g
              key={c.id}
              transform={`translate(${c.x}, ${c.y})`}
              style={{ transition: 'all 0.15s ease-out' }}
            >
              {/* Cursor arrow SVG */}
              <path
                d="M 0 0 L 0 16 L 4 12 L 8 20 L 11 19 L 7 11 L 13 11 Z"
                fill={c.color}
                stroke="#FFFFFF"
                strokeWidth="1"
              />
              {/* Name pill */}
              <rect
                x="14"
                y="14"
                width={c.name.length * 7 + 12}
                height="20"
                rx="4"
                fill={c.color}
              />
              <text
                x="20"
                y="28"
                fill="#FFFFFF"
                fontSize="11"
                fontFamily="Inter"
                fontWeight="600"
              >
                {c.name}
              </text>
            </g>
          ))}
        </svg>
      </div>

      {/* Interactive Pixel Rulers */}
      {canvasSettings?.showRulers && (
        <>
          {/* Horizontal Top Ruler */}
          <div
            style={{
              position: 'absolute',
              top: 0,
              left: 20,
              right: 0,
              height: '20px',
              backgroundColor: '#1E1E1F',
              borderBottom: '1px solid #333333',
              zIndex: 35,
              overflow: 'hidden',
              userSelect: 'none',
              cursor: 'crosshair',
            }}
            onClick={() => {
              if (onAddGuide) onAddGuide({ id: `guide-${Date.now()}`, axis: 'horizontal', position: mouseCanvasPos.y });
            }}
            title="Click to place horizontal guide line"
          >
            {Array.from({ length: 40 }).map((_, i) => {
              const canvasPixel = (i - 10) * 100;
              const screenX = viewport.x + canvasPixel * viewport.zoom;
              return (
                <div
                  key={i}
                  style={{
                    position: 'absolute',
                    left: `${screenX}px`,
                    top: 0,
                    bottom: 0,
                    borderLeft: '1px solid #444',
                    paddingLeft: '3px',
                    fontSize: '9px',
                    color: '#888',
                    fontFamily: 'monospace',
                  }}
                >
                  {canvasPixel}
                </div>
              );
            })}
            <div
              style={{
                position: 'absolute',
                left: `${viewport.x + mouseCanvasPos.x * viewport.zoom}px`,
                top: 0,
                bottom: 0,
                width: '2px',
                backgroundColor: '#0D99FF',
              }}
            />
          </div>

          {/* Vertical Left Ruler */}
          <div
            style={{
              position: 'absolute',
              top: 20,
              left: 0,
              bottom: 0,
              width: '20px',
              backgroundColor: '#1E1E1F',
              borderRight: '1px solid #333333',
              zIndex: 35,
              overflow: 'hidden',
              userSelect: 'none',
              cursor: 'crosshair',
            }}
            onClick={() => {
              if (onAddGuide) onAddGuide({ id: `guide-${Date.now()}`, axis: 'vertical', position: mouseCanvasPos.x });
            }}
            title="Click to place vertical guide line"
          >
            {Array.from({ length: 40 }).map((_, i) => {
              const canvasPixel = (i - 10) * 100;
              const screenY = viewport.y + canvasPixel * viewport.zoom;
              return (
                <div
                  key={i}
                  style={{
                    position: 'absolute',
                    top: `${screenY}px`,
                    left: 0,
                    right: 0,
                    borderTop: '1px solid #444',
                    fontSize: '8px',
                    color: '#888',
                    fontFamily: 'monospace',
                    paddingLeft: '2px',
                    transform: 'rotate(-90deg)',
                    transformOrigin: 'top left',
                  }}
                >
                  {canvasPixel}
                </div>
              );
            })}
            <div
              style={{
                position: 'absolute',
                top: `${viewport.y + mouseCanvasPos.y * viewport.zoom}px`,
                left: 0,
                right: 0,
                height: '2px',
                backgroundColor: '#0D99FF',
              }}
            />
          </div>

          {/* Ruler Origin Top-Left Corner */}
          <div
            style={{
              position: 'absolute',
              top: 0,
              left: 0,
              width: '20px',
              height: '20px',
              backgroundColor: '#18181A',
              borderRight: '1px solid #333',
              borderBottom: '1px solid #333',
              zIndex: 40,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              fontSize: '10px',
              color: '#888',
              cursor: 'pointer',
            }}
            onClick={() => setViewport({ x: 80, y: 60, zoom: 1 })}
            title="Reset Origin (0,0) and Zoom 100%"
          >
            px
          </div>
        </>
      )}

      {/* Floating Pen Tool Controls Pill */}
      {currentTool === 'pen' && penNodes.length > 0 && (
        <div
          style={{
            position: 'absolute',
            bottom: '62px',
            left: '50%',
            transform: 'translateX(-50%)',
            backgroundColor: '#1E1E22',
            border: '1px solid #0D99FF',
            borderRadius: '24px',
            padding: '6px 14px',
            display: 'flex',
            alignItems: 'center',
            gap: '12px',
            boxShadow: '0 4px 20px rgba(0,0,0,0.6)',
            zIndex: 100,
          }}
        >
          <span style={{ fontSize: '11px', color: '#E0E0E0', fontWeight: 500 }}>
            Pen Tool: <strong style={{ color: '#0D99FF' }}>{penNodes.length}</strong> points placed. Click start to close, drag for curves.
          </span>
          <button
            className="figma-btn primary"
            onClick={() => finishPenPath(false)}
            style={{
              padding: '4px 12px',
              fontSize: '11px',
              fontWeight: 600,
              backgroundColor: '#0D99FF',
              color: '#FFFFFF',
              borderRadius: '12px',
              cursor: 'pointer',
            }}
          >
            ✓ Done (Enter)
          </button>
        </div>
      )}

      {/* Floating Canvas Quick Info Pill */}
      <div className="floating-overlay-pill">
        <span style={{ fontSize: '11px', color: 'var(--text-secondary)' }}>
          Canvas: <strong style={{ color: '#fff' }}>{Math.round(mouseCanvasPos.x)}</strong>,{' '}
          <strong style={{ color: '#fff' }}>{Math.round(mouseCanvasPos.y)}</strong>
        </span>
        <div style={{ width: '1px', height: '12px', background: 'var(--border-color)' }} />
        <span style={{ fontSize: '11px', color: 'var(--text-secondary)' }}>
          {selectedIds.length > 0 ? `${selectedIds.length} Selected` : 'No selection'}
        </span>
        <div style={{ width: '1px', height: '12px', background: 'var(--border-color)' }} />
        <span style={{ fontSize: '11px', color: 'var(--accent)', fontWeight: 600 }}>
          {currentTool.toUpperCase()}
        </span>
        {onOpenCanvasSettings && (
          <>
            <div style={{ width: '1px', height: '12px', background: 'var(--border-color)' }} />
            <button
              className="figma-btn"
              onClick={onOpenCanvasSettings}
              style={{ padding: '2px 8px', fontSize: '11px', color: '#0D99FF', gap: '4px' }}
              title="Customize Canvas & Grid (Theme, Dimensions, Rulers)"
            >
              🎨 Canvas
            </button>
          </>
        )}
      </div>
    </div>
  );
};
