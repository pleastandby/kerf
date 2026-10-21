import { useState, useEffect, useCallback, useRef } from 'react';
import type {
  Project,
  FigmaElement,
  ToolType,
  Viewport,
  Collaborator,
  ContextMenuState,
  EngineStats,
  Workspace,
  CanvasSettings,
  GuideLine,
  AppViewMode,
} from './types';
import { initialProject, bankingTemplateElements, landingPageTemplateElements } from './templates';
import { TopNav } from './components/TopNav';
import { LeftSidebar } from './components/LeftSidebar';
import { RightSidebar } from './components/RightSidebar';
import { Canvas } from './components/Canvas';
import { PrototypeModal } from './components/PrototypeModal';
import { ShortcutsModal } from './components/ShortcutsModal';
import { ShareModal } from './components/ShareModal';
import { ContextMenu } from './components/ContextMenu';
import { PropertiesModal } from './components/PropertiesModal';
import { ProjectsModal } from './components/ProjectsModal';
import { EngineModal } from './components/EngineModal';
import { PythonConsoleModal } from './components/PythonConsoleModal';
import { CanvasSettingsModal } from './components/CanvasSettingsModal';
import { HomePage } from './components/HomePage';
import { deviceDB } from './storage/db';
import { webglRenderer } from './engine/webglRenderer';
import { exportSVG, exportRasterImage, exportProjectJSON } from './utils/export';
import { generateCSS, generateSVGCode } from './utils/codeGenerator';
import { createAutoLayoutFrame, applyAutoLayoutToFrame, removeAutoLayout, reorderAutoLayoutChild } from './utils/autoLayout';

const STORAGE_KEY = 'kerf_studio_project_v1';
const LEGACY_STORAGE_KEY = 'figma_studio_project_v1';

export function App() {
  // Load initial project from localStorage or default
  const [project, setProject] = useState<Project>(() => {
    try {
      const saved = localStorage.getItem(STORAGE_KEY) || localStorage.getItem(LEGACY_STORAGE_KEY);
      if (saved) return JSON.parse(saved);
    } catch (e) {
      console.warn('Failed to load saved project', e);
    }
    return initialProject;
  });

  // Current active page
  const activePage =
    project.pages.find((p) => p.id === project.activePageId) || project.pages[0];

  // Active Tool
  const [currentTool, setCurrentTool] = useState<ToolType>('select');

  // Canvas Viewport (pan and zoom)
  const [viewport, setViewport] = useState<Viewport>({ x: 80, y: 60, zoom: 0.85 });

  // App view mode: 'home' dashboard or 'editor' canvas (defaults to 'home')
  const [viewMode, setViewMode] = useState<AppViewMode>('home');

  // Workspaces Management
  const [workspaces, setWorkspaces] = useState<Workspace[]>([]);
  const [activeWorkspaceId, setActiveWorkspaceId] = useState<string>('ws-personal');

  // All Projects & Device Storage
  const [allProjects, setAllProjects] = useState<Project[]>([]);
  const [storageUsage, setStorageUsage] = useState({ usedBytes: 0, quotaBytes: 0, projectCount: 0 });

  const refreshAllProjects = useCallback(async () => {
    try {
      const list = await deviceDB.getAllProjects();
      setAllProjects(list);
      const usage = await deviceDB.getStorageUsage();
      setStorageUsage(usage);
    } catch (e) {
      console.warn('Failed to refresh projects', e);
    }
  }, []);

  // Custom Canvas Settings
  const [canvasSettings, setCanvasSettings] = useState<CanvasSettings>({
    backgroundColor: '#1E1E1E',
    gridType: 'dots',
    gridSize: 20,
    showRulers: false,
    showGuides: true,
    preset: 'infinite',
  });
  const [canvasSettingsOpen, setCanvasSettingsOpen] = useState(false);
  const [guideLines, setGuideLines] = useState<GuideLine[]>([]);

  // Selected Elements
  const [selectedIds, setSelectedIds] = useState<string[]>([]);

  // Show pixel grid
  const [showGrid, setShowGrid] = useState<boolean>(true);

  // Modals
  const [prototypeOpen, setPrototypeOpen] = useState(false);
  const [shortcutsOpen, setShortcutsOpen] = useState(false);
  const [shareOpen, setShareOpen] = useState(false);
  const [propertiesModalOpen, setPropertiesModalOpen] = useState(false);
  const [projectsModalOpen, setProjectsModalOpen] = useState(false);
  const [engineModalOpen, setEngineModalOpen] = useState(false);
  const [pythonConsoleOpen, setPythonConsoleOpen] = useState(false);

  // Context Menu State
  const [contextMenu, setContextMenu] = useState<ContextMenuState>({
    isOpen: false,
    x: 0,
    y: 0,
    canvasX: 0,
    canvasY: 0,
    targetElementId: null,
  });

  // Clipboard for copy/cut/paste
  const [clipboard, setClipboard] = useState<FigmaElement[]>([]);

  // Undo / Redo History Stack
  const [history, setHistory] = useState<FigmaElement[][]>([activePage.elements]);
  const [historyIndex, setHistoryIndex] = useState(0);

  // Engine telemetry stats
  const [engineStats, setEngineStats] = useState<EngineStats>(() => webglRenderer.getStats());

  // Collaborators (disabled simulated floating cursors)
  const [collaborators] = useState<Collaborator[]>([]);

  // Initialize Device Storage Database (IndexedDB)
  useEffect(() => {
    deviceDB.init().then(async () => {
      const existing = await deviceDB.getAllProjects();
      if (existing.length === 0) {
        await deviceDB.saveProject(initialProject);
      }
      const ws = await deviceDB.getAllWorkspaces();
      setWorkspaces(ws);
      if (ws.length > 0) {
        setActiveWorkspaceId(ws[0].id);
      }
      refreshAllProjects();
    });
  }, [refreshAllProjects]);

  // Persist project changes to Device Storage Database & localStorage
  const saveTimeoutRef = useRef<any>(null);
  useEffect(() => {
    deviceDB.saveAutosave(project);
    if (saveTimeoutRef.current) clearTimeout(saveTimeoutRef.current);
    saveTimeoutRef.current = setTimeout(() => {
      deviceDB.saveProject(project);
    }, 1000);
    return () => {
      if (saveTimeoutRef.current) clearTimeout(saveTimeoutRef.current);
    };
  }, [project]);

  // Engine telemetry update loop
  useEffect(() => {
    const timer = setInterval(() => {
      setEngineStats(webglRenderer.getStats());
    }, 600);
    return () => clearInterval(timer);
  }, []);



  // Update elements in active page and save to history
  const updatePageElements = useCallback(
    (newElements: FigmaElement[], recordHistory = true) => {
      setProject((prev) => ({
        ...prev,
        pages: prev.pages.map((p) =>
          p.id === prev.activePageId ? { ...p, elements: newElements } : p
        ),
      }));

      if (recordHistory) {
        setHistory((prev) => {
          const next = prev.slice(0, historyIndex + 1);
          return [...next, newElements].slice(-50);
        });
        setHistoryIndex((prev) => Math.min(prev + 1, 49));
      }
    },
    [historyIndex]
  );

  // Undo / Redo
  const handleUndo = useCallback(() => {
    if (historyIndex > 0) {
      const prevElements = history[historyIndex - 1];
      setHistoryIndex(historyIndex - 1);
      setProject((prev) => ({
        ...prev,
        pages: prev.pages.map((p) =>
          p.id === prev.activePageId ? { ...p, elements: prevElements } : p
        ),
      }));
    }
  }, [history, historyIndex]);

  const handleRedo = useCallback(() => {
    if (historyIndex < history.length - 1) {
      const nextElements = history[historyIndex + 1];
      setHistoryIndex(historyIndex + 1);
      setProject((prev) => ({
        ...prev,
        pages: prev.pages.map((p) =>
          p.id === prev.activePageId ? { ...p, elements: nextElements } : p
        ),
      }));
    }
  }, [history, historyIndex]);

  // Selection handlers
  const handleSelect = useCallback((id: string, multi = false) => {
    if (multi) {
      setSelectedIds((prev) =>
        prev.includes(id) ? prev.filter((i) => i !== id) : [...prev, id]
      );
    } else {
      setSelectedIds([id]);
    }
  }, []);

  const handleClearSelection = useCallback(() => {
    setSelectedIds([]);
  }, []);

  // Update single element
  const handleUpdateElement = useCallback(
    (id: string, updates: Partial<FigmaElement>, recordHistory = false) => {
      let updated = activePage.elements.map((el) =>
        el.id === id ? { ...el, ...updates } : el
      );

      // If the modified element is an Auto Layout frame, recompute its children
      const target = updated.find((el) => el.id === id);
      if (target?.isAutoLayout) {
        updated = applyAutoLayoutToFrame(target, updated);
      } else if (target?.parentId) {
        const parentFrame = updated.find((el) => el.id === target.parentId);
        if (parentFrame?.isAutoLayout) {
          updated = applyAutoLayoutToFrame(parentFrame, updated);
        }
      }

      updatePageElements(updated, recordHistory);
    },
    [activePage.elements, updatePageElements]
  );

  // Add element
  const handleAddElement = useCallback(
    (elem: FigmaElement) => {
      const updated = [...activePage.elements, elem];
      updatePageElements(updated, true);
    },
    [activePage.elements, updatePageElements]
  );

  // Delete element
  const handleDeleteElement = useCallback(
    (id: string) => {
      const updated = activePage.elements.filter((el) => el.id !== id && el.parentId !== id);
      setSelectedIds((prev) => prev.filter((i) => i !== id));
      updatePageElements(updated, true);
    },
    [activePage.elements, updatePageElements]
  );

  // Duplicate element
  const handleDuplicateElement = useCallback(
    (id: string) => {
      const target = activePage.elements.find((el) => el.id === id);
      if (!target) return;
      const dup: FigmaElement = {
        ...target,
        id: `${target.type}-${Date.now()}`,
        name: `${target.name} (Copy)`,
        x: target.x + 24,
        y: target.y + 24,
      };
      const updated = [...activePage.elements, dup];
      updatePageElements(updated, true);
      setSelectedIds([dup.id]);
    },
    [activePage.elements, updatePageElements]
  );

  // Reorder elements
  const handleReorderElement = useCallback(
    (id: string, direction: 'up' | 'down' | 'front' | 'back') => {
      const idx = activePage.elements.findIndex((el) => el.id === id);
      if (idx === -1) return;

      const elements = [...activePage.elements];
      const [item] = elements.splice(idx, 1);

      if (direction === 'up') {
        elements.splice(Math.min(elements.length, idx + 1), 0, item);
      } else if (direction === 'down') {
        elements.splice(Math.max(0, idx - 1), 0, item);
      } else if (direction === 'front') {
        elements.push(item);
      } else if (direction === 'back') {
        elements.unshift(item);
      }

      updatePageElements(elements, true);
    },
    [activePage.elements, updatePageElements]
  );

  // Move Layer Order via Drag-and-Drop
  const handleMoveLayerOrder = useCallback(
    (sourceId: string, targetId: string, position: 'before' | 'after' | 'inside') => {
      if (sourceId === targetId) return;

      if (position === 'inside') {
        const elements = activePage.elements.map((el) =>
          el.id === sourceId ? { ...el, parentId: targetId } : el
        );
        updatePageElements(elements, true);
        return;
      }

      // Visual list is reverse of elements array (top visual layer is last in array)
      const visualList = [...activePage.elements].reverse();
      const vSourceIdx = visualList.findIndex((el) => el.id === sourceId);
      const vTargetIdx = visualList.findIndex((el) => el.id === targetId);
      if (vSourceIdx === -1 || vTargetIdx === -1) return;

      const [movedItem] = visualList.splice(vSourceIdx, 1);
      const targetElement = activePage.elements.find((el) => el.id === targetId);
      movedItem.parentId = targetElement?.parentId || null;

      const newVTargetIdx = visualList.findIndex((el) => el.id === targetId);
      const vInsertIdx = position === 'before' ? newVTargetIdx : newVTargetIdx + 1;
      visualList.splice(vInsertIdx, 0, movedItem);

      // Convert back from visual list to rendering order
      const updatedElements = [...visualList].reverse();
      updatePageElements(updatedElements, true);
    },
    [activePage.elements, updatePageElements]
  );

  // Group Selection (Ctrl+G)
  const handleGroup = useCallback(() => {
    if (selectedIds.length === 0) return;
    const targets = activePage.elements.filter((el) => selectedIds.includes(el.id));
    if (targets.length === 0) return;

    const minX = Math.min(...targets.map((e) => e.x));
    const minY = Math.min(...targets.map((e) => e.y));
    const maxX = Math.max(...targets.map((e) => e.x + e.width));
    const maxY = Math.max(...targets.map((e) => e.y + e.height));

    const groupId = `group-${Date.now()}`;
    const groupCount = activePage.elements.filter((e) => e.isGroup).length + 1;
    const groupElem: FigmaElement = {
      id: groupId,
      name: `Group ${groupCount}`,
      type: 'rect',
      isGroup: true,
      x: minX,
      y: minY,
      width: Math.max(20, maxX - minX),
      height: Math.max(20, maxY - minY),
      rotation: 0,
      opacity: 1,
      locked: false,
      visible: true,
      fill: 'transparent',
      stroke: 'transparent',
      strokeWidth: 0,
      strokeStyle: 'solid',
      cornerRadius: 0,
      effects: [],
    };

    const updated = activePage.elements.map((el) =>
      selectedIds.includes(el.id) ? { ...el, parentId: groupId } : el
    );

    updatePageElements([...updated, groupElem], true);
    setSelectedIds([groupId]);
  }, [activePage.elements, selectedIds, updatePageElements]);

  // Ungroup Selection (Ctrl+Shift+G)
  const handleUngroup = useCallback(() => {
    if (selectedIds.length === 0) return;
    const groupIdsToDissolve = selectedIds.filter((id) =>
      activePage.elements.some((el) => el.id === id && el.isGroup)
    );
    if (groupIdsToDissolve.length === 0) return;

    const remainingElements = activePage.elements
      .filter((el) => !groupIdsToDissolve.includes(el.id))
      .map((el) =>
        el.parentId && groupIdsToDissolve.includes(el.parentId)
          ? { ...el, parentId: null }
          : el
      );

    updatePageElements(remainingElements, true);
    setSelectedIds([]);
  }, [activePage.elements, selectedIds, updatePageElements]);

  // Auto Layout (Shift+A)
  const handleAutoLayout = useCallback(() => {
    if (selectedIds.length === 0) return;
    const targets = activePage.elements.filter((el) => selectedIds.includes(el.id));
    if (targets.length === 0) return;

    const { updatedElements, newFrameId } = createAutoLayoutFrame(
      targets,
      activePage.elements
    );
    updatePageElements(updatedElements, true);
    if (newFrameId) {
      setSelectedIds([newFrameId]);
    }
  }, [activePage.elements, selectedIds, updatePageElements]);

  // Remove Auto Layout (Alt+Shift+A)
  const handleRemoveAutoLayout = useCallback(() => {
    if (selectedIds.length === 0) return;
    let updated = [...activePage.elements];
    selectedIds.forEach((id) => {
      updated = removeAutoLayout(id, updated);
    });
    updatePageElements(updated, true);
  }, [activePage.elements, selectedIds, updatePageElements]);

  // Reorder child in Auto Layout frame
  const handleReorderChild = useCallback((childId: string, direction: 'prev' | 'next') => {
    const updated = reorderAutoLayoutChild(childId, direction, activePage.elements);
    updatePageElements(updated, true);
  }, [activePage.elements, updatePageElements]);

  // Clipboard: Cut, Copy, Paste
  const handleCut = useCallback(() => {
    if (selectedIds.length === 0) return;
    const targets = activePage.elements.filter((el) => selectedIds.includes(el.id));
    setClipboard(targets);
    selectedIds.forEach((id) => handleDeleteElement(id));
  }, [activePage.elements, selectedIds, handleDeleteElement]);

  const handleCopy = useCallback(() => {
    if (selectedIds.length === 0) return;
    const targets = activePage.elements.filter((el) => selectedIds.includes(el.id));
    setClipboard(targets);
  }, [activePage.elements, selectedIds]);

  const handlePaste = useCallback(() => {
    if (clipboard.length === 0) return;
    const newItems: FigmaElement[] = clipboard.map((item, idx) => ({
      ...item,
      id: `${item.type}-${Date.now()}-${idx}`,
      name: `${item.name} (Pasted)`,
      x: item.x + 20,
      y: item.y + 20,
    }));
    updatePageElements([...activePage.elements, ...newItems], true);
    setSelectedIds(newItems.map((i) => i.id));
  }, [activePage.elements, clipboard, updatePageElements]);

  // Context Menu trigger
  const handleOpenContextMenu = useCallback(
    (e: React.MouseEvent, elementId: string | null) => {
      e.preventDefault();
      if (elementId && !selectedIds.includes(elementId)) {
        setSelectedIds([elementId]);
      }
      setContextMenu({
        isOpen: true,
        x: e.clientX,
        y: e.clientY,
        canvasX: (e.clientX - viewport.x) / viewport.zoom,
        canvasY: (e.clientY - viewport.y) / viewport.zoom,
        targetElementId: elementId,
      });
    },
    [selectedIds, viewport]
  );

  const handleCloseContextMenu = useCallback(() => {
    setContextMenu((prev) => ({ ...prev, isOpen: false }));
  }, []);

  // Alignment (handles multiple elements alignment & single element alignment to artboard/parent)
  const handleAlign = useCallback(
    (alignment: 'left' | 'center' | 'right' | 'top' | 'middle' | 'bottom') => {
      const selected = activePage.elements.filter((el) => selectedIds.includes(el.id));
      if (selected.length === 0) return;

      let boundLeft = 0;
      let boundRight = canvasSettings?.fixedWidth || 1440;
      let boundTop = 0;
      let boundBottom = canvasSettings?.fixedHeight || 900;

      if (selected.length > 1) {
        boundLeft = Math.min(...selected.map((e) => e.x));
        boundRight = Math.max(...selected.map((e) => e.x + e.width));
        boundTop = Math.min(...selected.map((e) => e.y));
        boundBottom = Math.max(...selected.map((e) => e.y + e.height));
      } else {
        // Single element: check if inside parent frame
        const elem = selected[0];
        if (elem.parentId) {
          const parent = activePage.elements.find((p) => p.id === elem.parentId);
          if (parent) {
            boundLeft = parent.x;
            boundRight = parent.x + parent.width;
            boundTop = parent.y;
            boundBottom = parent.y + parent.height;
          }
        }
      }

      const boundMidX = (boundLeft + boundRight) / 2;
      const boundMidY = (boundTop + boundBottom) / 2;

      const updated = activePage.elements.map((el) => {
        if (!selectedIds.includes(el.id)) return el;

        if (alignment === 'left') return { ...el, x: boundLeft };
        if (alignment === 'right') return { ...el, x: boundRight - el.width };
        if (alignment === 'center') return { ...el, x: Math.round(boundMidX - el.width / 2) };
        if (alignment === 'top') return { ...el, y: boundTop };
        if (alignment === 'bottom') return { ...el, y: boundBottom - el.height };
        if (alignment === 'middle') return { ...el, y: Math.round(boundMidY - el.height / 2) };
        return el;
      });

      updatePageElements(updated, true);
    },
    [activePage.elements, selectedIds, canvasSettings, updatePageElements]
  );

  // Distribute
  const handleDistribute = useCallback(
    (axis: 'horizontal' | 'vertical') => {
      const selected = activePage.elements.filter((el) => selectedIds.includes(el.id));
      if (selected.length < 3) return;

      if (axis === 'horizontal') {
        const sorted = [...selected].sort((a, b) => a.x - b.x);
        const minX = sorted[0].x;
        const last = sorted[sorted.length - 1];
        const maxX = last.x + last.width;
        const totalElemWidth = sorted.reduce((sum, el) => sum + el.width, 0);
        const availableSpace = maxX - minX - totalElemWidth;
        const gap = availableSpace / (sorted.length - 1);

        let currentX = minX;
        const updated = activePage.elements.map((el) => {
          const matchIndex = sorted.findIndex((s) => s.id === el.id);
          if (matchIndex === -1) return el;
          if (matchIndex === 0) {
            currentX += el.width + gap;
            return el;
          }
          const newX = currentX;
          currentX += el.width + gap;
          return { ...el, x: Math.round(newX) };
        });
        updatePageElements(updated, true);
      }
    },
    [activePage.elements, selectedIds, updatePageElements]
  );

  // Insert Asset from Kit
  const handleInsertAsset = (assetType: 'primary-button' | 'glass-card' | 'search-bar' | 'badge' | 'avatar') => {
    const centerX = Math.round((-viewport.x + window.innerWidth / 2) / viewport.zoom);
    const centerY = Math.round((-viewport.y + window.innerHeight / 2) / viewport.zoom);

    let newElement: FigmaElement;
    if (assetType === 'primary-button') {
      newElement = {
        id: `btn-${Date.now()}`,
        name: 'Primary Button',
        type: 'rect',
        x: centerX - 80,
        y: centerY - 24,
        width: 160,
        height: 48,
        rotation: 0,
        opacity: 1,
        locked: false,
        visible: true,
        fill: '#0D99FF',
        stroke: '#FFFFFF',
        strokeWidth: 0,
        strokeStyle: 'solid',
        cornerRadius: 10,
        effects: [
          {
            id: 'eff-btn-shadow',
            type: 'drop-shadow',
            x: 0,
            y: 8,
            blur: 16,
            spread: 0,
            color: 'rgba(13, 153, 255, 0.4)',
            enabled: true,
          },
        ],
      };
    } else if (assetType === 'glass-card') {
      newElement = {
        id: `card-${Date.now()}`,
        name: 'Glass Card Frame',
        type: 'frame',
        isFrame: true,
        x: centerX - 140,
        y: centerY - 90,
        width: 280,
        height: 180,
        rotation: 0,
        opacity: 1,
        locked: false,
        visible: true,
        fill: '#181C26',
        stroke: '#2F374A',
        strokeWidth: 1.5,
        strokeStyle: 'solid',
        cornerRadius: 20,
        effects: [
          {
            id: 'eff-card',
            type: 'drop-shadow',
            x: 0,
            y: 12,
            blur: 28,
            spread: 0,
            color: 'rgba(0, 0, 0, 0.5)',
            enabled: true,
          },
        ],
      };
    } else if (assetType === 'search-bar') {
      newElement = {
        id: `search-${Date.now()}`,
        name: 'Search Bar',
        type: 'rect',
        x: centerX - 140,
        y: centerY - 22,
        width: 280,
        height: 44,
        rotation: 0,
        opacity: 1,
        locked: false,
        visible: true,
        fill: '#151922',
        stroke: '#282F3E',
        strokeWidth: 1,
        strokeStyle: 'solid',
        cornerRadius: 22,
        effects: [],
      };
    } else if (assetType === 'badge') {
      newElement = {
        id: `badge-${Date.now()}`,
        name: 'Active Status Badge',
        type: 'rect',
        x: centerX - 50,
        y: centerY - 15,
        width: 100,
        height: 30,
        rotation: 0,
        opacity: 1,
        locked: false,
        visible: true,
        fill: 'rgba(16, 185, 129, 0.15)',
        stroke: '#10B981',
        strokeWidth: 1,
        strokeStyle: 'solid',
        cornerRadius: 15,
        effects: [],
      };
    } else {
      newElement = {
        id: `avatar-${Date.now()}`,
        name: 'User Avatar',
        type: 'ellipse',
        x: centerX - 30,
        y: centerY - 30,
        width: 60,
        height: 60,
        rotation: 0,
        opacity: 1,
        locked: false,
        visible: true,
        fill: '#0D99FF',
        stroke: '#FFFFFF',
        strokeWidth: 2,
        strokeStyle: 'solid',
        cornerRadius: 30,
        effects: [],
      };
    }

    handleAddElement(newElement);
    setSelectedIds([newElement.id]);
  };

  // Add Frame Presets
  const handleAddFramePreset = (preset: 'iphone' | 'macbook' | 'desktop' | 'square') => {
    const centerX = Math.round((-viewport.x + window.innerWidth / 2) / viewport.zoom);
    const centerY = Math.round((-viewport.y + window.innerHeight / 2) / viewport.zoom);

    let w = 393;
    let h = 852;
    let name = 'iPhone 16 Pro';
    let radius = 54;

    if (preset === 'macbook') {
      w = 1512;
      h = 982;
      name = 'MacBook Pro 14"';
      radius = 16;
    } else if (preset === 'desktop') {
      w = 1440;
      h = 1024;
      name = 'Desktop Display';
      radius = 8;
    } else if (preset === 'square') {
      w = 1080;
      h = 1080;
      name = 'Instagram Post';
      radius = 0;
    }

    const frameElement: FigmaElement = {
      id: `frame-${Date.now()}`,
      name,
      type: 'frame',
      isFrame: true,
      x: centerX - Math.round(w / 2),
      y: centerY - Math.round(h / 2),
      width: w,
      height: h,
      rotation: 0,
      opacity: 1,
      locked: false,
      visible: true,
      fill: '#12151D',
      stroke: '#282F3E',
      strokeWidth: 1,
      strokeStyle: 'solid',
      cornerRadius: radius,
      effects: [
        {
          id: 'eff-frame-shadow',
          type: 'drop-shadow',
          x: 0,
          y: 20,
          blur: 40,
          spread: 0,
          color: 'rgba(0, 0, 0, 0.6)',
          enabled: true,
        },
      ],
    };

    handleAddElement(frameElement);
    setSelectedIds([frameElement.id]);
  };

  // Add Page
  const handleAddPage = () => {
    const newPage = {
      id: `page-${Date.now()}`,
      name: `Page ${project.pages.length + 1}`,
      elements: [],
    };
    setProject((prev) => ({
      ...prev,
      pages: [...prev.pages, newPage],
      activePageId: newPage.id,
    }));
    setSelectedIds([]);
  };

  // Load Templates
  const handleLoadTemplate = (templateName: 'banking' | 'landing' | 'blank') => {
    let elements: FigmaElement[] = [];
    if (templateName === 'banking') {
      elements = bankingTemplateElements;
    } else if (templateName === 'landing') {
      elements = landingPageTemplateElements;
    }
    updatePageElements(elements, true);
    setSelectedIds([]);
  };

  // Add Image File
  const handleAddImage = (file: File) => {
    const reader = new FileReader();
    reader.onload = (e) => {
      const src = e.target?.result as string;
      const img = new Image();
      img.onload = () => {
        const centerX = Math.round((-viewport.x + window.innerWidth / 2) / viewport.zoom);
        const centerY = Math.round((-viewport.y + window.innerHeight / 2) / viewport.zoom);
        const maxDim = 400;
        let w = img.width;
        let h = img.height;
        if (w > maxDim || h > maxDim) {
          const ratio = w / h;
          if (w > h) {
            w = maxDim;
            h = Math.round(maxDim / ratio);
          } else {
            h = maxDim;
            w = Math.round(maxDim * ratio);
          }
        }

        const imgElement: FigmaElement = {
          id: `img-${Date.now()}`,
          name: file.name.split('.')[0] || 'Image',
          type: 'image',
          x: centerX - Math.round(w / 2),
          y: centerY - Math.round(h / 2),
          width: w,
          height: h,
          rotation: 0,
          opacity: 1,
          locked: false,
          visible: true,
          fill: '#ffffff',
          stroke: 'transparent',
          strokeWidth: 0,
          strokeStyle: 'solid',
          cornerRadius: 8,
          src,
          effects: [],
        };
        handleAddElement(imgElement);
        setSelectedIds([imgElement.id]);
      };
      img.src = src;
    };
    reader.readAsDataURL(file);
  };

  // Create New Project in Local Database
  const handleCreateNewProject = () => {
    const newProject: Project = {
      id: `project-${Date.now()}`,
      name: `Untitled Design ${Date.now().toString().slice(-4)}`,
      workspaceId: activeWorkspaceId,
      pages: [
        {
          id: 'page-1',
          name: 'Page 1',
          elements: [],
        },
      ],
      activePageId: 'page-1',
      createdAt: Date.now(),
      updatedAt: Date.now(),
    };
    deviceDB.saveProject(newProject);
    setProject(newProject);
    setSelectedIds([]);
    setHistory([[]]);
    setHistoryIndex(0);
  };

  // Workspace management handlers
  const handleSelectWorkspace = useCallback((wsId: string) => {
    setActiveWorkspaceId(wsId);
  }, []);

  const handleCreateWorkspace = useCallback(async (name: string, description?: string, color?: string) => {
    const newWs: Workspace = {
      id: `ws-${Date.now()}`,
      name,
      description,
      color: color || '#0D99FF',
      createdAt: Date.now(),
    };
    await deviceDB.saveWorkspace(newWs);
    const updated = await deviceDB.getAllWorkspaces();
    setWorkspaces(updated);
    setActiveWorkspaceId(newWs.id);
  }, []);

  const handleDeleteWorkspace = useCallback(async (id: string) => {
    await deviceDB.deleteWorkspace(id);
    const updated = await deviceDB.getAllWorkspaces();
    setWorkspaces(updated);
    if (activeWorkspaceId === id && updated.length > 0) {
      setActiveWorkspaceId(updated[0].id);
    }
  }, [activeWorkspaceId]);

  // Project management handlers for Home page
  const handleOpenProjectFromHome = useCallback(async (proj: Project) => {
    await deviceDB.saveProject(proj);
    await refreshAllProjects();
    setProject(proj);
    setSelectedIds([]);
    const page = proj.pages.find((p) => p.id === proj.activePageId) || proj.pages[0];
    setHistory([page?.elements || []]);
    setHistoryIndex(0);
    if (proj.workspaceId) {
      setActiveWorkspaceId(proj.workspaceId);
    }
    setViewMode('editor');
  }, [refreshAllProjects]);

  const handleCreateProjectFromHome = useCallback(async (templateElements?: any[], projectName?: string) => {
    const newProj: Project = {
      id: `project-${Date.now()}`,
      name: projectName || `Untitled Design ${Date.now().toString().slice(-4)}`,
      workspaceId: activeWorkspaceId,
      pages: [
        {
          id: 'page-1',
          name: 'Page 1',
          elements: templateElements || [],
        },
      ],
      activePageId: 'page-1',
      createdAt: Date.now(),
      updatedAt: Date.now(),
    };

    await deviceDB.saveProject(newProj);
    await refreshAllProjects();
    setProject(newProj);
    setSelectedIds([]);
    setHistory([templateElements || []]);
    setHistoryIndex(0);
    setViewMode('editor');
  }, [activeWorkspaceId, refreshAllProjects]);

  const handleDuplicateProjectFromHome = useCallback(async (id: string, targetWorkspaceId?: string) => {
    const source = allProjects.find((p) => p.id === id) || (await deviceDB.getProject(id));
    if (!source) return;
    const dup: Project = {
      ...source,
      id: `project-${Date.now()}`,
      name: `${source.name} (Copy)`,
      workspaceId: targetWorkspaceId || source.workspaceId || activeWorkspaceId,
      deleted: false,
      deletedAt: undefined,
      createdAt: Date.now(),
      updatedAt: Date.now(),
    };
    await deviceDB.saveProject(dup);
    await refreshAllProjects();
  }, [allProjects, refreshAllProjects, activeWorkspaceId]);

  const handleMoveToBin = useCallback(async (id: string) => {
    await deviceDB.softDeleteProject(id);
    await refreshAllProjects();
    if (project.id === id) {
      const all = await deviceDB.getAllProjects();
      const activeNonDeleted = all.filter((p) => !p.deleted && p.id !== id);
      if (activeNonDeleted.length > 0) {
        setProject(activeNonDeleted[0]);
        setSelectedIds([]);
        const pg = activeNonDeleted[0].pages.find((p) => p.id === activeNonDeleted[0].activePageId) || activeNonDeleted[0].pages[0];
        setHistory([pg?.elements || []]);
        setHistoryIndex(0);
      }
    }
  }, [project.id, refreshAllProjects]);

  const handleRestoreFromBin = useCallback(async (id: string) => {
    await deviceDB.restoreProject(id);
    await refreshAllProjects();
  }, [refreshAllProjects]);

  const handlePermanentDelete = useCallback(async (id: string) => {
    await deviceDB.deleteProject(id);
    await refreshAllProjects();
    if (project.id === id) {
      const all = await deviceDB.getAllProjects();
      const activeNonDeleted = all.filter((p) => !p.deleted && p.id !== id);
      if (activeNonDeleted.length > 0) {
        setProject(activeNonDeleted[0]);
        setSelectedIds([]);
        const pg = activeNonDeleted[0].pages.find((p) => p.id === activeNonDeleted[0].activePageId) || activeNonDeleted[0].pages[0];
        setHistory([pg?.elements || []]);
        setHistoryIndex(0);
      }
    }
  }, [project.id, refreshAllProjects]);

  const handleEmptyBin = useCallback(async () => {
    await deviceDB.emptyBin();
    await refreshAllProjects();
  }, [refreshAllProjects]);

  const handleRenameProjectFromHome = useCallback(async (id: string, newName: string) => {
    const target = await deviceDB.getProject(id);
    if (target) {
      target.name = newName;
      target.updatedAt = Date.now();
      await deviceDB.saveProject(target);
      await refreshAllProjects();
      if (project.id === id) {
        setProject((prev) => ({ ...prev, name: newName }));
      }
    }
  }, [project.id, refreshAllProjects]);

  const handleMoveProjectToWorkspace = useCallback(async (projectId: string, targetWsId: string) => {
    await deviceDB.updateProjectWorkspace(projectId, targetWsId);
    await refreshAllProjects();
    if (project.id === projectId) {
      setProject((prev) => ({ ...prev, workspaceId: targetWsId }));
    }
  }, [project.id, refreshAllProjects]);

  // Global Keyboard Shortcuts
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement) {
        return;
      }

      // Group (Ctrl+G)
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'g' && !e.shiftKey) {
        e.preventDefault();
        handleGroup();
        return;
      }

      // Ungroup (Ctrl+Shift+G)
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'g' && e.shiftKey) {
        e.preventDefault();
        handleUngroup();
        return;
      }

      // Cut (Ctrl+X)
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'x') {
        e.preventDefault();
        handleCut();
        return;
      }

      // Copy (Ctrl+C)
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'c') {
        e.preventDefault();
        handleCopy();
        return;
      }

      // Paste (Ctrl+V)
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'v') {
        e.preventDefault();
        handlePaste();
        return;
      }

      // Properties Inspector (Alt+Enter)
      if (e.altKey && e.key === 'Enter') {
        e.preventDefault();
        setPropertiesModalOpen(true);
        return;
      }

      // Undo (Ctrl+Z)
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'z' && !e.shiftKey) {
        handleUndo();
        e.preventDefault();
        return;
      }

      // Redo (Ctrl+Y or Ctrl+Shift+Z)
      if (
        ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'z' && e.shiftKey) ||
        ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'y')
      ) {
        handleRedo();
        e.preventDefault();
        return;
      }

      // Duplicate (Ctrl+D)
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'd') {
        if (selectedIds.length === 1) {
          handleDuplicateElement(selectedIds[0]);
        }
        e.preventDefault();
        return;
      }

      // Delete / Backspace
      if (e.key === 'Delete' || e.key === 'Backspace') {
        if (selectedIds.length > 0) {
          selectedIds.forEach((id) => handleDeleteElement(id));
          e.preventDefault();
        }
        return;
      }

      // Auto Layout (Shift+A)
      if (e.shiftKey && e.key.toLowerCase() === 'a' && !e.altKey && !e.ctrlKey && !e.metaKey) {
        e.preventDefault();
        handleAutoLayout();
        return;
      }

      // Remove Auto Layout (Alt+Shift+A)
      if (e.shiftKey && e.altKey && e.key.toLowerCase() === 'a') {
        e.preventDefault();
        handleRemoveAutoLayout();
        return;
      }

      // Tool shortcuts
      if (e.key.toLowerCase() === 'v') setCurrentTool('select');
      if (e.key.toLowerCase() === 'h') setCurrentTool('hand');
      if (e.key.toLowerCase() === 'r') setCurrentTool('rect');
      if (e.key.toLowerCase() === 'o') setCurrentTool('ellipse');
      if (e.key.toLowerCase() === 't') setCurrentTool('text');
      if (e.key.toLowerCase() === 'f') setCurrentTool('frame');
      if (e.key.toLowerCase() === 'p') setCurrentTool('pen');
      if (e.key.toLowerCase() === 'l') setCurrentTool('line');

      // Shortcuts modal
      if (e.key === '?') setShortcutsOpen((prev) => !prev);

      // Nudge with arrow keys
      if (['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].includes(e.key) && selectedIds.length > 0) {
        const step = e.shiftKey ? 10 : 1;
        const dx = e.key === 'ArrowLeft' ? -step : e.key === 'ArrowRight' ? step : 0;
        const dy = e.key === 'ArrowUp' ? -step : e.key === 'ArrowDown' ? step : 0;

        const allNudgeIds = new Set<string>(selectedIds);
        const findNudgeDescendants = (parentIds: string[]) => {
          const direct = activePage.elements.filter(
            (el) => el.parentId && parentIds.includes(el.parentId) && !allNudgeIds.has(el.id)
          );
          if (direct.length > 0) {
            direct.forEach((c) => allNudgeIds.add(c.id));
            findNudgeDescendants(direct.map((c) => c.id));
          }
        };
        findNudgeDescendants(selectedIds);

        const updated = activePage.elements.map((el) => {
          if (!allNudgeIds.has(el.id)) return el;
          return { ...el, x: el.x + dx, y: el.y + dy };
        });
        updatePageElements(updated, false);
        e.preventDefault();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [
    activePage.elements,
    selectedIds,
    handleGroup,
    handleUngroup,
    handleAutoLayout,
    handleRemoveAutoLayout,
    handleCut,
    handleCopy,
    handlePaste,
    handleUndo,
    handleRedo,
    handleDuplicateElement,
    handleDeleteElement,
    updatePageElements,
  ]);

  // Export handler
  const handleExport = (format: 'png' | 'svg' | 'json', scale = 2) => {
    const targets =
      selectedIds.length > 0
        ? activePage.elements.filter((el) => selectedIds.includes(el.id))
        : activePage.elements;

    if (format === 'json') {
      exportProjectJSON(project);
    } else if (format === 'svg') {
      exportSVG(targets, `${project.name.toLowerCase().replace(/\s+/g, '-')}.svg`);
    } else {
      exportRasterImage(targets, 'png', scale, project.name.toLowerCase().replace(/\s+/g, '-'));
    }
  };

  // Find all frames in current page for prototyping dropdown
  const allFrames = activePage.elements.filter((el) => el.type === 'frame');

  // Currently targeted element for Properties Modal or Context Menu
  const primarySelectedElement =
    activePage.elements.find((el) => el.id === contextMenu.targetElementId) ||
    activePage.elements.find((el) => el.id === selectedIds[0]) ||
    null;

  if (viewMode === 'home') {
    return (
      <div className="figma-app">
        <HomePage
          projects={allProjects}
          workspaces={workspaces}
          activeWorkspaceId={activeWorkspaceId}
          onSelectWorkspace={handleSelectWorkspace}
          onCreateWorkspace={handleCreateWorkspace}
          onDeleteWorkspace={handleDeleteWorkspace}
          onOpenProject={handleOpenProjectFromHome}
          onCreateNewProject={handleCreateProjectFromHome}
          onDuplicateProject={handleDuplicateProjectFromHome}
          onDeleteProject={handleMoveToBin}
          onMoveToBin={handleMoveToBin}
          onRestoreFromBin={handleRestoreFromBin}
          onPermanentDelete={handlePermanentDelete}
          onEmptyBin={handleEmptyBin}
          onRenameProject={handleRenameProjectFromHome}
          onMoveProjectToWorkspace={handleMoveProjectToWorkspace}
          storageUsage={storageUsage}
        />
      </div>
    );
  }

  return (
    <div className="figma-app">
      {/* Top Navigation Bar */}
      <TopNav
        currentTool={currentTool}
        setCurrentTool={setCurrentTool}
        project={project}
        setProject={setProject}
        zoom={viewport.zoom}
        setZoom={(z) => setViewport((prev) => ({ ...prev, zoom: z }))}
        canUndo={historyIndex > 0}
        canRedo={historyIndex < history.length - 1}
        onUndo={handleUndo}
        onRedo={handleRedo}
        onPresent={() => setPrototypeOpen(true)}
        onShare={() => setShareOpen(true)}
        onShowShortcuts={() => setShortcutsOpen(true)}
        onExport={handleExport}
        onLoadTemplate={handleLoadTemplate}
        collaborators={collaborators}
        showGrid={showGrid}
        setShowGrid={setShowGrid}
        onAddImage={handleAddImage}
        onAddFramePreset={handleAddFramePreset}
        onOpenProjects={() => setProjectsModalOpen(true)}
        onOpenEngineModal={() => setEngineModalOpen(true)}
        onOpenPythonConsole={() => setPythonConsoleOpen(true)}
        onNavigateHome={() => setViewMode('home')}
        onOpenCanvasSettings={() => setCanvasSettingsOpen(true)}
        activeWorkspaceName={workspaces.find((w) => w.id === activeWorkspaceId)?.name || 'Personal Space'}
        engineStats={engineStats}
      />

      {/* Main Workspace Layout */}
      <div className="figma-workspace">
        <LeftSidebar
          elements={activePage.elements}
          selectedIds={selectedIds}
          onSelect={handleSelect}
          onUpdateElement={handleUpdateElement}
          onDeleteElement={handleDeleteElement}
          onDuplicateElement={handleDuplicateElement}
          onReorderElement={handleReorderElement}
          onMoveLayerOrder={handleMoveLayerOrder}
          pages={project.pages}
          activePageId={project.activePageId}
          onSelectPage={(id) => {
            setProject((prev) => ({ ...prev, activePageId: id }));
            setSelectedIds([]);
          }}
          onAddPage={handleAddPage}
          onInsertAsset={handleInsertAsset}
          onContextMenu={handleOpenContextMenu}
          onGroup={handleGroup}
          onUngroup={handleUngroup}
          onOpenProperties={() => setPropertiesModalOpen(true)}
        />

        <Canvas
          elements={activePage.elements}
          selectedIds={selectedIds}
          currentTool={currentTool}
          setCurrentTool={setCurrentTool}
          viewport={viewport}
          setViewport={setViewport}
          onSelect={handleSelect}
          onClearSelection={handleClearSelection}
          onUpdateElement={handleUpdateElement}
          onAddElement={handleAddElement}
          collaborators={collaborators}
          showGrid={showGrid}
          onContextMenu={handleOpenContextMenu}
          canvasSettings={canvasSettings}
          onOpenCanvasSettings={() => setCanvasSettingsOpen(true)}
          guideLines={guideLines}
          onAddGuide={(guide: GuideLine) => setGuideLines((prev) => [...prev, guide])}
        />

        <RightSidebar
          selectedElements={activePage.elements.filter((el) => selectedIds.includes(el.id))}
          allFrames={allFrames}
          allElements={activePage.elements}
          onUpdateElement={handleUpdateElement}
          onAlign={handleAlign}
          onDistribute={handleDistribute}
          onAutoLayout={handleAutoLayout}
          onRemoveAutoLayout={handleRemoveAutoLayout}
          onReorderChild={handleReorderChild}
        />
      </div>

      {/* Right Click Context Menu */}
      {contextMenu.isOpen && (
        <ContextMenu
          x={contextMenu.x}
          y={contextMenu.y}
          targetElement={primarySelectedElement}
          selectedElements={activePage.elements.filter((el) => selectedIds.includes(el.id))}
          onClose={handleCloseContextMenu}
          onCut={handleCut}
          onCopy={handleCopy}
          onPaste={handlePaste}
          onDuplicate={() => {
            if (primarySelectedElement) handleDuplicateElement(primarySelectedElement.id);
          }}
          onDelete={() => {
            if (primarySelectedElement) handleDeleteElement(primarySelectedElement.id);
          }}
          onGroup={handleGroup}
          onUngroup={handleUngroup}
          onBringForward={() => {
            if (primarySelectedElement) handleReorderElement(primarySelectedElement.id, 'up');
          }}
          onSendBackward={() => {
            if (primarySelectedElement) handleReorderElement(primarySelectedElement.id, 'down');
          }}
          onBringToFront={() => {
            if (primarySelectedElement) handleReorderElement(primarySelectedElement.id, 'front');
          }}
          onSendToBack={() => {
            if (primarySelectedElement) handleReorderElement(primarySelectedElement.id, 'back');
          }}
          onToggleLock={() => {
            if (primarySelectedElement) {
              handleUpdateElement(primarySelectedElement.id, { locked: !primarySelectedElement.locked });
            }
          }}
          onToggleVisibility={() => {
            if (primarySelectedElement) {
              handleUpdateElement(primarySelectedElement.id, { visible: !primarySelectedElement.visible });
            }
          }}
          onStartRename={() => {
            // Handled via LeftSidebar double click or Properties Inspector
            setPropertiesModalOpen(true);
          }}
          onOpenProperties={() => setPropertiesModalOpen(true)}
          onCopyCSS={() => {
            if (primarySelectedElement) {
              navigator.clipboard.writeText(generateCSS(primarySelectedElement));
            }
          }}
          onCopySVG={() => {
            if (primarySelectedElement) {
              navigator.clipboard.writeText(generateSVGCode([primarySelectedElement]));
            }
          }}
          onExportElement={() => {
            if (primarySelectedElement) {
              exportRasterImage([primarySelectedElement], 'png', 2, primarySelectedElement.name);
            }
          }}
          onToggleGrid={() => setShowGrid((prev) => !prev)}
          onZoomReset={() => setViewport((prev) => ({ ...prev, zoom: 1 }))}
          onOpenEngineStats={() => setEngineModalOpen(true)}
          onAutoLayout={handleAutoLayout}
          onRemoveAutoLayout={handleRemoveAutoLayout}
        />
      )}

      {/* Custom Canvas Settings Modal */}
      <CanvasSettingsModal
        isOpen={canvasSettingsOpen}
        onClose={() => setCanvasSettingsOpen(false)}
        settings={canvasSettings}
        onUpdateSettings={(newSettings: Partial<CanvasSettings>) => {
          setCanvasSettings((prev) => ({ ...prev, ...newSettings }));
        }}
      />

      {/* Properties Inspector Modal */}
      <PropertiesModal
        element={primarySelectedElement}
        isOpen={propertiesModalOpen}
        onClose={() => setPropertiesModalOpen(false)}
        onUpdateElement={handleUpdateElement}
      />

      {/* Device Storage Local Database Projects Modal */}
      <ProjectsModal
        isOpen={projectsModalOpen}
        onClose={() => setProjectsModalOpen(false)}
        currentProject={project}
        onSwitchProject={(p) => {
          setProject(p);
          setSelectedIds([]);
          const page = p.pages.find((pg) => pg.id === p.activePageId) || p.pages[0];
          setHistory([page?.elements || []]);
          setHistoryIndex(0);
        }}
        onCreateNewProject={handleCreateNewProject}
        onDeleteProject={handleMoveToBin}
      />

      {/* C++ WebAssembly & WebGL Engine Telemetry Modal */}
      <EngineModal
        isOpen={engineModalOpen}
        onClose={() => setEngineModalOpen(false)}
        engineStats={engineStats}
        onSetRenderMode={(mode) => webglRenderer.setRenderMode(mode)}
        onToggleHardwareAcceleration={(enabled) => webglRenderer.toggleHardwareAcceleration(enabled)}
      />

      {/* Python Scripting Console Modal */}
      <PythonConsoleModal
        isOpen={pythonConsoleOpen}
        onClose={() => setPythonConsoleOpen(false)}
        elements={activePage.elements}
        onApplyElements={(elems) => updatePageElements(elems, true)}
      />

      {/* Prototype Player Modal */}
      <PrototypeModal
        isOpen={prototypeOpen}
        onClose={() => setPrototypeOpen(false)}
        elements={activePage.elements}
        initialFrameId={selectedIds.find((id) =>
          activePage.elements.some((el) => el.id === id && el.type === 'frame')
        )}
      />

      {/* Keyboard Shortcuts Cheat Sheet Modal */}
      <ShortcutsModal isOpen={shortcutsOpen} onClose={() => setShortcutsOpen(false)} />

      {/* Collaborative Share Modal */}
      <ShareModal
        isOpen={shareOpen}
        onClose={() => setShareOpen(false)}
        projectName={project.name}
      />
    </div>
  );
}

export default App;
