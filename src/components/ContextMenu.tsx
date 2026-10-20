import React, { useEffect, useRef } from 'react';
import {
  Copy,
  Scissors,
  Clipboard,
  Trash2,
  FolderPlus,
  FolderMinus,
  Lock,
  Unlock,
  Eye,
  EyeOff,
  ArrowUp,
  ArrowDown,
  ChevronsUp,
  ChevronsDown,
  Sliders,
  Code,
  Download,
  Edit2,
  Grid,
  Maximize,
  Cpu,
} from 'lucide-react';
import type { FigmaElement } from '../types';

export interface ContextMenuProps {
  x: number;
  y: number;
  targetElement: FigmaElement | null;
  selectedElements: FigmaElement[];
  onClose: () => void;
  onCut: () => void;
  onCopy: () => void;
  onPaste: () => void;
  onDuplicate: () => void;
  onDelete: () => void;
  onGroup: () => void;
  onUngroup: () => void;
  onBringForward: () => void;
  onSendBackward: () => void;
  onBringToFront: () => void;
  onSendToBack: () => void;
  onToggleLock: () => void;
  onToggleVisibility: () => void;
  onStartRename: () => void;
  onOpenProperties: () => void;
  onCopyCSS: () => void;
  onCopySVG: () => void;
  onExportElement: () => void;
  onToggleGrid?: () => void;
  onZoomReset?: () => void;
  onOpenEngineStats?: () => void;
  onAutoLayout?: () => void;
  onRemoveAutoLayout?: () => void;
}

export const ContextMenu: React.FC<ContextMenuProps> = ({
  x,
  y,
  targetElement,
  selectedElements,
  onClose,
  onCut,
  onCopy,
  onPaste,
  onDuplicate,
  onDelete,
  onGroup,
  onUngroup,
  onBringForward,
  onSendBackward,
  onBringToFront,
  onSendToBack,
  onToggleLock,
  onToggleVisibility,
  onStartRename,
  onOpenProperties,
  onCopyCSS,
  onCopySVG,
  onExportElement,
  onToggleGrid,
  onZoomReset,
  onOpenEngineStats,
  onAutoLayout,
  onRemoveAutoLayout,
}) => {
  const menuRef = useRef<HTMLDivElement>(null);

  // Auto-clamp menu inside window bounds
  useEffect(() => {
    if (!menuRef.current) return;
    const menu = menuRef.current;
    const rect = menu.getBoundingClientRect();
    const winWidth = window.innerWidth;
    const winHeight = window.innerHeight;

    if (rect.right > winWidth) {
      menu.style.left = `${Math.max(10, winWidth - rect.width - 12)}px`;
    }
    if (rect.bottom > winHeight) {
      menu.style.top = `${Math.max(10, winHeight - rect.height - 12)}px`;
    }
  }, [x, y]);

  // Click outside to close
  useEffect(() => {
    const handleDown = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        onClose();
      }
    };
    window.addEventListener('mousedown', handleDown);
    return () => window.removeEventListener('mousedown', handleDown);
  }, [onClose]);

  const hasSelection = !!targetElement || selectedElements.length > 0;
  const isSingle = selectedElements.length === 1 || !!targetElement;
  const isGroup = targetElement?.isGroup || selectedElements.some((e) => e.isGroup);

  return (
    <div
      ref={menuRef}
      style={{
        position: 'fixed',
        left: `${x}px`,
        top: `${y}px`,
        backgroundColor: '#1E1E1E',
        border: '1px solid #333333',
        borderRadius: '8px',
        boxShadow: '0 12px 32px rgba(0, 0, 0, 0.6), 0 2px 6px rgba(0, 0, 0, 0.4)',
        zIndex: 9999,
        minWidth: '220px',
        padding: '6px 0',
        color: '#E0E0E0',
        fontSize: '12px',
        userSelect: 'none',
        backdropFilter: 'blur(16px)',
      }}
      onClick={(e) => e.stopPropagation()}
    >
      {hasSelection ? (
        <>
          {/* Layer identity */}
          <div
            style={{
              padding: '6px 14px',
              fontSize: '11px',
              fontWeight: 600,
              color: '#9E9E9E',
              borderBottom: '1px solid #2C2C2C',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
            }}
          >
            <span style={{ maxWidth: '140px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
              {targetElement?.name || `${selectedElements.length} elements`}
            </span>
            <span style={{ fontSize: '10px', color: '#0D99FF', textTransform: 'uppercase' }}>
              {targetElement?.type || 'selection'}
            </span>
          </div>

          {/* Properties Inspector item - Highlighted */}
          <button
            className="context-menu-item"
            style={{ color: '#0D99FF', fontWeight: 600 }}
            onClick={() => {
              onOpenProperties();
              onClose();
            }}
          >
            <Sliders size={14} color="#0D99FF" />
            <span style={{ flex: 1 }}>Properties Inspector...</span>
            <kbd>Alt+Enter</kbd>
          </button>

          <div style={{ height: '1px', backgroundColor: '#2C2C2C', margin: '4px 0' }} />

          {/* Reordering */}
          <button
            className="context-menu-item"
            onClick={() => {
              onBringToFront();
              onClose();
            }}
          >
            <ChevronsUp size={14} />
            <span style={{ flex: 1 }}>Bring to Front</span>
            <kbd>Ctrl+]</kbd>
          </button>
          <button
            className="context-menu-item"
            onClick={() => {
              onBringForward();
              onClose();
            }}
          >
            <ArrowUp size={14} />
            <span style={{ flex: 1 }}>Bring Forward</span>
            <kbd>]</kbd>
          </button>
          <button
            className="context-menu-item"
            onClick={() => {
              onSendBackward();
              onClose();
            }}
          >
            <ArrowDown size={14} />
            <span style={{ flex: 1 }}>Send Backward</span>
            <kbd>[</kbd>
          </button>
          <button
            className="context-menu-item"
            onClick={() => {
              onSendToBack();
              onClose();
            }}
          >
            <ChevronsDown size={14} />
            <span style={{ flex: 1 }}>Send to Back</span>
            <kbd>Ctrl+[</kbd>
          </button>

          <div style={{ height: '1px', backgroundColor: '#2C2C2C', margin: '4px 0' }} />

          {/* Clipboard & Duplicate */}
          <button
            className="context-menu-item"
            onClick={() => {
              onCut();
              onClose();
            }}
          >
            <Scissors size={14} />
            <span style={{ flex: 1 }}>Cut</span>
            <kbd>Ctrl+X</kbd>
          </button>
          <button
            className="context-menu-item"
            onClick={() => {
              onCopy();
              onClose();
            }}
          >
            <Copy size={14} />
            <span style={{ flex: 1 }}>Copy</span>
            <kbd>Ctrl+C</kbd>
          </button>
          <button
            className="context-menu-item"
            onClick={() => {
              onPaste();
              onClose();
            }}
          >
            <Clipboard size={14} />
            <span style={{ flex: 1 }}>Paste</span>
            <kbd>Ctrl+V</kbd>
          </button>
          <button
            className="context-menu-item"
            onClick={() => {
              onDuplicate();
              onClose();
            }}
          >
            <Copy size={14} />
            <span style={{ flex: 1 }}>Duplicate</span>
            <kbd>Ctrl+D</kbd>
          </button>

          <div style={{ height: '1px', backgroundColor: '#2C2C2C', margin: '4px 0' }} />

          {/* Grouping */}
          <button
            className="context-menu-item"
            onClick={() => {
              onGroup();
              onClose();
            }}
          >
            <FolderPlus size={14} />
            <span style={{ flex: 1 }}>Group Selection</span>
            <kbd>Ctrl+G</kbd>
          </button>
          {isGroup && (
            <button
              className="context-menu-item"
              onClick={() => {
                onUngroup();
                onClose();
              }}
            >
              <FolderMinus size={14} />
              <span style={{ flex: 1 }}>Ungroup Selection</span>
              <kbd>Ctrl+Shift+G</kbd>
            </button>
          )}

          {/* Auto Layout (Shift+A) */}
          {onAutoLayout && (
            <button
              className="context-menu-item"
              onClick={() => {
                onAutoLayout();
                onClose();
              }}
            >
              <Grid size={14} color="#0D99FF" />
              <span style={{ flex: 1 }}>
                {targetElement?.isAutoLayout ? 'Update Auto Layout' : 'Add Auto Layout'}
              </span>
              <kbd>Shift+A</kbd>
            </button>
          )}
          {targetElement?.isAutoLayout && onRemoveAutoLayout && (
            <button
              className="context-menu-item"
              onClick={() => {
                onRemoveAutoLayout();
                onClose();
              }}
            >
              <FolderMinus size={14} color="#EF4444" />
              <span style={{ flex: 1 }}>Remove Auto Layout</span>
              <kbd>Alt+Shift+A</kbd>
            </button>
          )}

          <div style={{ height: '1px', backgroundColor: '#2C2C2C', margin: '4px 0' }} />

          {/* State Toggles */}
          <button
            className="context-menu-item"
            onClick={() => {
              onToggleLock();
              onClose();
            }}
          >
            {targetElement?.locked ? <Unlock size={14} /> : <Lock size={14} />}
            <span style={{ flex: 1 }}>{targetElement?.locked ? 'Unlock Layer' : 'Lock Layer'}</span>
            <kbd>Ctrl+Shift+L</kbd>
          </button>
          <button
            className="context-menu-item"
            onClick={() => {
              onToggleVisibility();
              onClose();
            }}
          >
            {targetElement?.visible ? <EyeOff size={14} /> : <Eye size={14} />}
            <span style={{ flex: 1 }}>{targetElement?.visible ? 'Hide Layer' : 'Show Layer'}</span>
            <kbd>Ctrl+Shift+H</kbd>
          </button>
          {isSingle && (
            <button
              className="context-menu-item"
              onClick={() => {
                onStartRename();
                onClose();
              }}
            >
              <Edit2 size={14} />
              <span style={{ flex: 1 }}>Rename</span>
              <kbd>F2</kbd>
            </button>
          )}

          <div style={{ height: '1px', backgroundColor: '#2C2C2C', margin: '4px 0' }} />

          {/* Code & Export */}
          <button
            className="context-menu-item"
            onClick={() => {
              onCopyCSS();
              onClose();
            }}
          >
            <Code size={14} />
            <span style={{ flex: 1 }}>Copy CSS</span>
          </button>
          <button
            className="context-menu-item"
            onClick={() => {
              onCopySVG();
              onClose();
            }}
          >
            <Code size={14} />
            <span style={{ flex: 1 }}>Copy SVG</span>
          </button>
          <button
            className="context-menu-item"
            onClick={() => {
              onExportElement();
              onClose();
            }}
          >
            <Download size={14} />
            <span style={{ flex: 1 }}>Export as PNG</span>
          </button>

          <div style={{ height: '1px', backgroundColor: '#2C2C2C', margin: '4px 0' }} />

          {/* Delete */}
          <button
            className="context-menu-item"
            style={{ color: '#EF4444' }}
            onClick={() => {
              onDelete();
              onClose();
            }}
          >
            <Trash2 size={14} color="#EF4444" />
            <span style={{ flex: 1 }}>Delete</span>
            <kbd>Del</kbd>
          </button>
        </>
      ) : (
        /* Canvas empty space background right click menu */
        <>
          <div
            style={{
              padding: '6px 14px',
              fontSize: '11px',
              fontWeight: 600,
              color: '#9E9E9E',
              borderBottom: '1px solid #2C2C2C',
            }}
          >
            Canvas Actions
          </div>
          <button
            className="context-menu-item"
            onClick={() => {
              onPaste();
              onClose();
            }}
          >
            <Clipboard size={14} />
            <span style={{ flex: 1 }}>Paste Here</span>
            <kbd>Ctrl+V</kbd>
          </button>

          <div style={{ height: '1px', backgroundColor: '#2C2C2C', margin: '4px 0' }} />

          {onToggleGrid && (
            <button
              className="context-menu-item"
              onClick={() => {
                onToggleGrid();
                onClose();
              }}
            >
              <Grid size={14} />
              <span style={{ flex: 1 }}>Toggle Pixel Grid</span>
              <kbd>Ctrl+'</kbd>
            </button>
          )}

          {onZoomReset && (
            <button
              className="context-menu-item"
              onClick={() => {
                onZoomReset();
                onClose();
              }}
            >
              <Maximize size={14} />
              <span style={{ flex: 1 }}>Zoom to 100%</span>
              <kbd>Shift+0</kbd>
            </button>
          )}

          {onOpenEngineStats && (
            <button
              className="context-menu-item"
              onClick={() => {
                onOpenEngineStats();
                onClose();
              }}
            >
              <Cpu size={14} color="#10B981" />
              <span style={{ flex: 1 }}>WebGL & WASM Engine...</span>
            </button>
          )}
        </>
      )}
    </div>
  );
};
