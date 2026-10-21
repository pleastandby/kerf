import React, { useState, useRef, useEffect } from 'react';
import {
  MousePointer2,
  Hand,
  Frame,
  Square,
  Circle,
  Minus,
  ArrowRight,
  Star,
  Triangle,
  PenTool,
  Pencil,
  Type,
  Image as ImageIcon,
  Play,
  Share2,
  Download,
  Undo2,
  Redo2,
  ChevronDown,
  HelpCircle,
  HardDrive,
  Cpu,
  Terminal,
  Home,
  Palette,
  ChevronRight,
} from 'lucide-react';
import type { ToolType, Project, Collaborator, EngineStats } from '../types';

interface TopNavProps {
  currentTool: ToolType;
  setCurrentTool: (tool: ToolType) => void;
  project: Project;
  setProject: React.Dispatch<React.SetStateAction<Project>>;
  zoom: number;
  setZoom: (z: number) => void;
  canUndo: boolean;
  canRedo: boolean;
  onUndo: () => void;
  onRedo: () => void;
  onPresent: () => void;
  onShare: () => void;
  onShowShortcuts: () => void;
  onExport: (format: 'png' | 'svg' | 'json', scale?: number) => void;
  onLoadTemplate: (templateName: 'banking' | 'landing' | 'blank') => void;
  collaborators: Collaborator[];
  showGrid: boolean;
  setShowGrid: (v: boolean) => void;
  onAddImage: (file: File) => void;
  onAddFramePreset: (preset: 'iphone' | 'macbook' | 'desktop' | 'square') => void;
  onOpenProjects?: () => void;
  onOpenEngineModal?: () => void;
  onOpenPythonConsole?: () => void;
  onNavigateHome?: () => void;
  onOpenCanvasSettings?: () => void;
  activeWorkspaceName?: string;
  engineStats?: EngineStats;
}

export const TopNav: React.FC<TopNavProps> = ({
  currentTool,
  setCurrentTool,
  project,
  setProject,
  zoom,
  setZoom,
  canUndo,
  canRedo,
  onUndo,
  onRedo,
  onPresent,
  onShare,
  onShowShortcuts,
  onExport,
  onLoadTemplate,
  collaborators,
  showGrid,
  setShowGrid,
  onAddImage,
  onAddFramePreset,
  onOpenProjects,
  onOpenEngineModal,
  onOpenPythonConsole,
  onNavigateHome,
  onOpenCanvasSettings,
  activeWorkspaceName,
  engineStats,
}) => {
  const [activeMenu, setActiveMenu] = useState<string | null>(null);
  const [shapesOpen, setShapesOpen] = useState(false);
  const [framesOpen, setFramesOpen] = useState(false);
  const [exportOpen, setExportOpen] = useState(false);
  const [isEditingTitle, setIsEditingTitle] = useState(false);
  const [titleInput, setTitleInput] = useState(project.name);
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    setTitleInput(project.name);
  }, [project.name]);

  const handleTitleSubmit = () => {
    setIsEditingTitle(false);
    if (titleInput.trim()) {
      setProject((prev) => ({ ...prev, name: titleInput.trim() }));
    }
  };

  const handleImageFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      onAddImage(file);
    }
  };

  return (
    <header
      style={{
        height: '48px',
        backgroundColor: 'var(--bg-header)',
        borderBottom: '1px solid var(--border-color)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '0 12px',
        zIndex: 100,
        position: 'relative',
      }}
    >
      <input
        type="file"
        ref={fileInputRef}
        style={{ display: 'none' }}
        accept="image/*"
        onChange={handleImageFileChange}
      />

      {/* Left: Brand Icon + Tools */}
      <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
        {/* Kerf Main Menu */}
        <div style={{ position: 'relative' }}>
          <button
            className="figma-btn"
            style={{
              padding: '4px 6px',
              backgroundColor: activeMenu === 'figma' ? 'var(--bg-active)' : 'transparent',
              display: 'flex',
              alignItems: 'center',
              gap: '4px',
            }}
            onClick={() => setActiveMenu(activeMenu === 'figma' ? null : 'figma')}
            title="Kerf Menu"
          >
            <img src="/kerf-icon.svg" alt="Kerf" width="22" height="22" style={{ display: 'block' }} />
            <ChevronDown size={12} color="var(--text-secondary)" />
          </button>

          {activeMenu === 'figma' && (
            <div className="figma-dropdown" style={{ width: '220px' }}>
              <div
                className="figma-dropdown-item"
                onClick={() => {
                  onLoadTemplate('banking');
                  setActiveMenu(null);
                }}
              >
                <span>Mobile Banking Template</span>
                <span className="figma-badge">Preset</span>
              </div>
              <div
                className="figma-dropdown-item"
                onClick={() => {
                  onLoadTemplate('landing');
                  setActiveMenu(null);
                }}
              >
                <span>SaaS Landing Template</span>
                <span className="figma-badge">Preset</span>
              </div>
              <div
                className="figma-dropdown-item"
                onClick={() => {
                  onLoadTemplate('blank');
                  setActiveMenu(null);
                }}
              >
                <span>Blank Canvas</span>
                <span className="figma-badge">New</span>
              </div>
              <div style={{ height: '1px', background: 'var(--border-subtle)', margin: '4px 0' }} />
              <div
                className="figma-dropdown-item"
                onClick={() => {
                  setShowGrid(!showGrid);
                  setActiveMenu(null);
                }}
              >
                <span>Show Pixel Grid</span>
                <span>{showGrid ? '✓' : ''}</span>
              </div>
              <div
                className="figma-dropdown-item"
                onClick={() => {
                  setZoom(1);
                  setActiveMenu(null);
                }}
              >
                <span>Zoom to 100%</span>
                <span className="figma-badge">Shift+0</span>
              </div>
              <div
                className="figma-dropdown-item"
                onClick={() => {
                  onShowShortcuts();
                  setActiveMenu(null);
                }}
              >
                <span>Keyboard Shortcuts</span>
                <span className="figma-badge">?</span>
              </div>
            </div>
          )}
        </div>

        <div style={{ width: '1px', height: '20px', background: 'var(--border-color)', margin: '0 4px' }} />

        {/* Undo / Redo */}
        <button
          className="figma-btn"
          style={{ padding: '6px' }}
          onClick={onUndo}
          disabled={!canUndo}
          title="Undo (Ctrl+Z)"
        >
          <Undo2 size={15} color={canUndo ? 'var(--text-primary)' : 'var(--text-muted)'} />
        </button>
        <button
          className="figma-btn"
          style={{ padding: '6px' }}
          onClick={onRedo}
          disabled={!canRedo}
          title="Redo (Ctrl+Y)"
        >
          <Redo2 size={15} color={canRedo ? 'var(--text-primary)' : 'var(--text-muted)'} />
        </button>

        <div style={{ width: '1px', height: '20px', background: 'var(--border-color)', margin: '0 4px' }} />

        {/* Move / Pointer Tool (V) */}
        <button
          className={`figma-btn ${currentTool === 'select' ? 'active' : ''}`}
          style={{ padding: '7px' }}
          onClick={() => setCurrentTool('select')}
          title="Move (V)"
        >
          <MousePointer2 size={16} />
        </button>

        {/* Hand Tool (H) */}
        <button
          className={`figma-btn ${currentTool === 'hand' ? 'active' : ''}`}
          style={{ padding: '7px' }}
          onClick={() => setCurrentTool('hand')}
          title="Hand tool (H / Space+Drag)"
        >
          <Hand size={16} />
        </button>

        {/* Frame Tool (F) */}
        <div style={{ position: 'relative' }}>
          <div style={{ display: 'flex', alignItems: 'center' }}>
            <button
              className={`figma-btn ${currentTool === 'frame' ? 'active' : ''}`}
              style={{ padding: '7px', borderTopRightRadius: 0, borderBottomRightRadius: 0 }}
              onClick={() => setCurrentTool('frame')}
              title="Frame (F)"
            >
              <Frame size={16} />
            </button>
            <button
              className="figma-btn"
              style={{
                padding: '7px 4px',
                borderTopLeftRadius: 0,
                borderBottomLeftRadius: 0,
                borderLeft: '1px solid var(--border-color)',
              }}
              onClick={() => setFramesOpen(!framesOpen)}
            >
              <ChevronDown size={11} />
            </button>
          </div>

          {framesOpen && (
            <div className="figma-dropdown" style={{ width: '200px' }}>
              <div
                className="figma-dropdown-item"
                onClick={() => {
                  onAddFramePreset('iphone');
                  setFramesOpen(false);
                }}
              >
                <span>iPhone 16 Pro</span>
                <span className="figma-badge">393 × 852</span>
              </div>
              <div
                className="figma-dropdown-item"
                onClick={() => {
                  onAddFramePreset('macbook');
                  setFramesOpen(false);
                }}
              >
                <span>MacBook Pro 14"</span>
                <span className="figma-badge">1512 × 982</span>
              </div>
              <div
                className="figma-dropdown-item"
                onClick={() => {
                  onAddFramePreset('desktop');
                  setFramesOpen(false);
                }}
              >
                <span>Desktop HD</span>
                <span className="figma-badge">1440 × 900</span>
              </div>
              <div
                className="figma-dropdown-item"
                onClick={() => {
                  onAddFramePreset('square');
                  setFramesOpen(false);
                }}
              >
                <span>Social Square</span>
                <span className="figma-badge">1080 × 1080</span>
              </div>
            </div>
          )}
        </div>

        {/* Shapes Menu (R, O, Line, Star, etc.) */}
        <div style={{ position: 'relative' }}>
          <div style={{ display: 'flex', alignItems: 'center' }}>
            <button
              className={`figma-btn ${
                ['rect', 'ellipse', 'line', 'arrow', 'star', 'polygon'].includes(currentTool)
                  ? 'active'
                  : ''
              }`}
              style={{ padding: '7px', borderTopRightRadius: 0, borderBottomRightRadius: 0 }}
              onClick={() => setCurrentTool('rect')}
              title="Rectangle (R)"
            >
              {currentTool === 'ellipse' ? (
                <Circle size={16} />
              ) : currentTool === 'line' ? (
                <Minus size={16} />
              ) : currentTool === 'star' ? (
                <Star size={16} />
              ) : currentTool === 'polygon' ? (
                <Triangle size={16} />
              ) : (
                <Square size={16} />
              )}
            </button>
            <button
              className="figma-btn"
              style={{
                padding: '7px 4px',
                borderTopLeftRadius: 0,
                borderBottomLeftRadius: 0,
                borderLeft: '1px solid var(--border-color)',
              }}
              onClick={() => setShapesOpen(!shapesOpen)}
            >
              <ChevronDown size={11} />
            </button>
          </div>

          {shapesOpen && (
            <div className="figma-dropdown" style={{ width: '190px' }}>
              <div
                className="figma-dropdown-item"
                onClick={() => {
                  setCurrentTool('rect');
                  setShapesOpen(false);
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <Square size={14} /> Rectangle
                </div>
                <span className="figma-badge">R</span>
              </div>
              <div
                className="figma-dropdown-item"
                onClick={() => {
                  setCurrentTool('ellipse');
                  setShapesOpen(false);
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <Circle size={14} /> Ellipse
                </div>
                <span className="figma-badge">O</span>
              </div>
              <div
                className="figma-dropdown-item"
                onClick={() => {
                  setCurrentTool('line');
                  setShapesOpen(false);
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <Minus size={14} /> Line
                </div>
                <span className="figma-badge">L</span>
              </div>
              <div
                className="figma-dropdown-item"
                onClick={() => {
                  setCurrentTool('arrow');
                  setShapesOpen(false);
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <ArrowRight size={14} /> Arrow
                </div>
                <span className="figma-badge">Shift+L</span>
              </div>
              <div
                className="figma-dropdown-item"
                onClick={() => {
                  setCurrentTool('polygon');
                  setShapesOpen(false);
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <Triangle size={14} /> Polygon
                </div>
              </div>
              <div
                className="figma-dropdown-item"
                onClick={() => {
                  setCurrentTool('star');
                  setShapesOpen(false);
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <Star size={14} /> Star
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Pen Tool (P) */}
        <button
          className={`figma-btn ${currentTool === 'pen' ? 'active' : ''}`}
          style={{ padding: '7px' }}
          onClick={() => setCurrentTool('pen')}
          title="Pen (P)"
        >
          <PenTool size={16} />
        </button>

        {/* Pencil Tool */}
        <button
          className={`figma-btn ${currentTool === 'pencil' ? 'active' : ''}`}
          style={{ padding: '7px' }}
          onClick={() => setCurrentTool('pencil')}
          title="Pencil / Freehand"
        >
          <Pencil size={16} />
        </button>

        {/* Text Tool (T) */}
        <button
          className={`figma-btn ${currentTool === 'text' ? 'active' : ''}`}
          style={{ padding: '7px' }}
          onClick={() => setCurrentTool('text')}
          title="Text (T)"
        >
          <Type size={16} />
        </button>

        {/* Image upload */}
        <button
          className="figma-btn"
          style={{ padding: '7px' }}
          onClick={() => fileInputRef.current?.click()}
          title="Place Image"
        >
          <ImageIcon size={16} />
        </button>
      </div>

      {/* Middle: Workspace Breadcrumb & Project Title */}
      <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
        {onNavigateHome && (
          <button
            className="figma-btn"
            style={{
              padding: '4px 7px',
              color: 'var(--text-secondary)',
              fontSize: '12px',
              display: 'flex',
              alignItems: 'center',
              gap: '5px',
            }}
            onClick={onNavigateHome}
            title="Return to Home / Workspace Dashboard"
          >
            <Home size={13} />
            <span>Home</span>
          </button>
        )}
        {activeWorkspaceName && (
          <>
            <ChevronRight size={12} color="var(--text-muted)" />
            <span
              style={{
                fontSize: '12px',
                color: 'var(--text-secondary)',
                padding: '2px 6px',
                borderRadius: '4px',
                background: 'var(--bg-subtle)',
                fontWeight: 500,
                maxWidth: '120px',
                overflow: 'hidden',
                textOverflow: 'ellipsis',
                whiteSpace: 'nowrap',
              }}
              title={`Workspace: ${activeWorkspaceName}`}
            >
              {activeWorkspaceName}
            </span>
            <ChevronRight size={12} color="var(--text-muted)" />
          </>
        )}

        {isEditingTitle ? (
          <input
            type="text"
            className="figma-input"
            value={titleInput}
            onChange={(e) => setTitleInput(e.target.value)}
            onBlur={handleTitleSubmit}
            onKeyDown={(e) => e.key === 'Enter' && handleTitleSubmit()}
            autoFocus
            style={{ width: '180px', textAlign: 'center', fontWeight: 600, fontSize: '13px' }}
          />
        ) : (
          <div
            style={{
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: '6px',
              padding: '4px 8px',
              borderRadius: '4px',
            }}
            onClick={() => setIsEditingTitle(true)}
            className="figma-btn"
          >
            <span style={{ fontWeight: 600, fontSize: '13px', color: 'var(--text-primary)' }}>
              {project.name}
            </span>
            <span
              style={{
                width: '6px',
                height: '6px',
                borderRadius: '50%',
                background: 'var(--success)',
                display: 'inline-block',
              }}
              title="Cloud Synced"
            />
          </div>
        )}

        {onOpenProjects && (
          <button
            className="figma-btn"
            style={{
              padding: '4px 8px',
              fontSize: '11px',
              display: 'flex',
              gap: '5px',
              alignItems: 'center',
              backgroundColor: 'rgba(13, 153, 255, 0.1)',
              border: '1px solid rgba(13, 153, 255, 0.25)',
              color: '#0D99FF',
            }}
            onClick={onOpenProjects}
            title="Open Local Device Storage Projects Database (IndexedDB)"
          >
            <HardDrive size={13} color="#0D99FF" />
            <span>Local DB</span>
          </button>
        )}
      </div>

      {/* Right: Presence, Presentation, Zoom, Export & Share */}
      <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
        {/* Collaborators Avatar Stack */}
        <div style={{ display: 'flex', alignItems: 'center', marginRight: '4px' }}>
          {collaborators.slice(0, 3).map((collab, idx) => (
            <div
              key={collab.id}
              style={{
                width: '26px',
                height: '26px',
                borderRadius: '50%',
                backgroundColor: collab.color,
                color: '#fff',
                fontSize: '11px',
                fontWeight: 600,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                marginLeft: idx === 0 ? 0 : '-6px',
                border: '2px solid var(--bg-header)',
                boxShadow: 'var(--shadow-sm)',
                position: 'relative',
              }}
              title={`${collab.name} (Active)`}
            >
              {collab.name.charAt(0)}
            </div>
          ))}
        </div>

        {/* Python Scripting Console Button */}
        {onOpenPythonConsole && (
          <button
            className="figma-btn"
            style={{
              padding: '5px 8px',
              gap: '4px',
              fontSize: '11px',
              backgroundColor: 'rgba(245, 158, 11, 0.1)',
              border: '1px solid rgba(245, 158, 11, 0.25)',
              color: '#F59E0B',
            }}
            onClick={onOpenPythonConsole}
            title="Python 3.12 & C++ WebAssembly Scripting Console"
          >
            <Terminal size={13} color="#F59E0B" />
            <span>Python</span>
          </button>
        )}

        {/* Graphics Engine Telemetry Button */}
        {onOpenEngineModal && (
          <button
            className="figma-btn"
            style={{
              padding: '5px 8px',
              gap: '4px',
              fontSize: '11px',
              backgroundColor: 'rgba(16, 185, 129, 0.1)',
              border: '1px solid rgba(16, 185, 129, 0.25)',
              color: '#10B981',
            }}
            onClick={onOpenEngineModal}
            title="C++ WebAssembly & WebGL Engine Telemetry"
          >
            <Cpu size={13} color="#10B981" />
            <span>{engineStats?.fps || 60} FPS</span>
          </button>
        )}

        {/* Custom Canvas Settings Button */}
        {onOpenCanvasSettings && (
          <button
            className="figma-btn"
            style={{
              padding: '5px 8px',
              gap: '4px',
              fontSize: '11px',
              backgroundColor: 'rgba(168, 85, 247, 0.1)',
              border: '1px solid rgba(168, 85, 247, 0.25)',
              color: '#C084FC',
            }}
            onClick={onOpenCanvasSettings}
            title="Custom Canvas (Backgrounds, Grid styles, Pixel Rulers, Artboard Presets)"
          >
            <Palette size={13} color="#C084FC" />
            <span>Canvas</span>
          </button>
        )}

        {/* Present / Prototype Button */}
        <button
          className="figma-btn"
          style={{
            background: 'var(--bg-active)',
            color: '#fff',
            padding: '5px 10px',
            gap: '6px',
          }}
          onClick={onPresent}
          title="Present Prototype (Preview)"
        >
          <Play size={14} fill="#fff" />
          <span>Present</span>
        </button>

        {/* Share Button */}
        <button
          className="figma-btn figma-btn-primary"
          style={{ padding: '5px 12px' }}
          onClick={onShare}
          title="Share Project"
        >
          <Share2 size={13} />
          <span>Share</span>
        </button>

        {/* Export Dropdown */}
        <div style={{ position: 'relative' }}>
          <button
            className="figma-btn"
            style={{ padding: '6px' }}
            onClick={() => setExportOpen(!exportOpen)}
            title="Export"
          >
            <Download size={15} />
            <ChevronDown size={11} />
          </button>

          {exportOpen && (
            <div className="figma-dropdown" style={{ right: 0, left: 'auto', width: '180px' }}>
              <div
                className="figma-dropdown-item"
                onClick={() => {
                  onExport('png', 2);
                  setExportOpen(false);
                }}
              >
                <span>Export PNG (2x)</span>
                <span className="figma-badge">Retina</span>
              </div>
              <div
                className="figma-dropdown-item"
                onClick={() => {
                  onExport('svg');
                  setExportOpen(false);
                }}
              >
                <span>Export SVG</span>
                <span className="figma-badge">Vector</span>
              </div>
              <div
                className="figma-dropdown-item"
                onClick={() => {
                  onExport('json');
                  setExportOpen(false);
                }}
              >
                <span>Export Project (.kerf)</span>
                <span className="figma-badge">.kerf</span>
              </div>
            </div>
          )}
        </div>

        <div style={{ width: '1px', height: '20px', background: 'var(--border-color)' }} />

        {/* Zoom Controls */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '2px' }}>
          <button
            className="figma-btn"
            style={{ padding: '4px 6px', fontSize: '11px', fontFamily: 'var(--font-mono)' }}
            onClick={() => setZoom(Math.max(0.1, zoom - 0.1))}
            title="Zoom Out"
          >
            -
          </button>
          <button
            className="figma-btn"
            style={{
              padding: '4px 6px',
              fontSize: '11px',
              fontFamily: 'var(--font-mono)',
              minWidth: '45px',
            }}
            onClick={() => setZoom(1)}
            title="Click to reset zoom to 100%"
          >
            {Math.round(zoom * 100)}%
          </button>
          <button
            className="figma-btn"
            style={{ padding: '4px 6px', fontSize: '11px', fontFamily: 'var(--font-mono)' }}
            onClick={() => setZoom(Math.min(3, zoom + 0.1))}
            title="Zoom In"
          >
            +
          </button>
        </div>

        {/* Keyboard Shortcuts (?) */}
        <button
          className="figma-btn"
          style={{ padding: '6px' }}
          onClick={onShowShortcuts}
          title="Keyboard Shortcuts"
        >
          <HelpCircle size={15} color="var(--text-secondary)" />
        </button>
      </div>
    </header>
  );
};
