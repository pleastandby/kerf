import React, { useState } from 'react';
import {
  Layers as LayersIcon,
  Component,
  FileText,
  Eye,
  EyeOff,
  Lock,
  Unlock,
  Trash2,
  Copy,
  Frame,
  Square,
  Circle,
  Type,
  Minus,
  Star,
  Triangle,
  PenTool,
  Image as ImageIcon,
  Folder,
  ChevronRight,
  ChevronDown,
  Search,
  FolderPlus,
  Sliders,
  MoreVertical,
  Plus,
  ArrowUp,
  ArrowDown,
  LayoutGrid,
} from 'lucide-react';
import type { FigmaElement, Page } from '../types';

interface LeftSidebarProps {
  elements: FigmaElement[];
  selectedIds: string[];
  onSelect: (id: string, multi?: boolean) => void;
  onUpdateElement: (id: string, updates: Partial<FigmaElement>) => void;
  onDeleteElement: (id: string) => void;
  onDuplicateElement: (id: string) => void;
  onReorderElement: (id: string, direction: 'up' | 'down' | 'front' | 'back') => void;
  onMoveLayerOrder?: (sourceId: string, targetId: string, position: 'before' | 'after' | 'inside') => void;
  pages: Page[];
  activePageId: string;
  onSelectPage: (pageId: string) => void;
  onAddPage: () => void;
  onInsertAsset: (assetType: 'primary-button' | 'glass-card' | 'search-bar' | 'badge' | 'avatar') => void;
  onContextMenu?: (e: React.MouseEvent, elementId: string) => void;
  onGroup?: () => void;
  onUngroup?: () => void;
  onOpenProperties?: () => void;
}

export const LeftSidebar: React.FC<LeftSidebarProps> = ({
  elements,
  selectedIds,
  onSelect,
  onUpdateElement,
  onDeleteElement,
  onDuplicateElement,
  onReorderElement,
  onMoveLayerOrder,
  pages,
  activePageId,
  onSelectPage,
  onAddPage,
  onInsertAsset,
  onContextMenu,
  onGroup,
  onUngroup,
  onOpenProperties,
}) => {
  const [activeTab, setActiveTab] = useState<'layers' | 'assets' | 'pages'>('layers');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editName, setEditName] = useState('');
  const [searchQuery, setSearchQuery] = useState('');
  const [filterType, setFilterType] = useState<'all' | 'frames' | 'shapes' | 'text'>('all');
  const [collapsedGroups, setCollapsedGroups] = useState<Record<string, boolean>>({});

  // Drag and drop state
  const [draggedId, setDraggedId] = useState<string | null>(null);
  const [dropTargetId, setDropTargetId] = useState<string | null>(null);
  const [dropPosition, setDropPosition] = useState<'before' | 'after' | 'inside'>('before');

  const getElementIcon = (elem: FigmaElement) => {
    if (elem.isAutoLayout) return <LayoutGrid size={14} color="#0D99FF" />;
    if (elem.isGroup) return <Folder size={14} color="#F59E0B" />;
    switch (elem.type) {
      case 'frame':
        return <Frame size={14} color="#0D99FF" />;
      case 'rect':
        return <Square size={14} color="#9BA1B0" />;
      case 'ellipse':
        return <Circle size={14} color="#9BA1B0" />;
      case 'text':
        return <Type size={14} color="#10B981" />;
      case 'line':
      case 'arrow':
        return <Minus size={14} color="#9BA1B0" />;
      case 'star':
        return <Star size={14} color="#F59E0B" />;
      case 'polygon':
        return <Triangle size={14} color="#F59E0B" />;
      case 'pen':
      case 'pencil':
        return <PenTool size={14} color="#A855F7" />;
      case 'image':
        return <ImageIcon size={14} color="#EC4899" />;
      default:
        return <Square size={14} />;
    }
  };

  const handleStartRename = (elem: FigmaElement) => {
    setEditingId(elem.id);
    setEditName(elem.name);
  };

  const handleFinishRename = (id: string) => {
    if (editName.trim()) {
      onUpdateElement(id, { name: editName.trim() });
    }
    setEditingId(null);
  };

  const toggleGroupCollapse = (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    setCollapsedGroups((prev) => ({ ...prev, [id]: !prev[id] }));
  };

  // Drag & drop handlers
  const handleDragStart = (e: React.DragEvent, id: string) => {
    e.dataTransfer.setData('text/plain', id);
    setDraggedId(id);
  };

  const handleDragOver = (e: React.DragEvent, targetElem: FigmaElement) => {
    e.preventDefault();
    if (!draggedId || draggedId === targetElem.id) return;

    const rect = e.currentTarget.getBoundingClientRect();
    const relY = e.clientY - rect.top;
    const height = rect.height;

    if (targetElem.isFrame || targetElem.isGroup) {
      if (relY > height * 0.25 && relY < height * 0.75) {
        setDropPosition('inside');
      } else if (relY <= height * 0.25) {
        setDropPosition('before');
      } else {
        setDropPosition('after');
      }
    } else {
      if (relY < height * 0.5) {
        setDropPosition('before');
      } else {
        setDropPosition('after');
      }
    }
    setDropTargetId(targetElem.id);
  };

  const handleDragLeave = () => {
    setDropTargetId(null);
  };

  const handleDrop = (e: React.DragEvent, targetId: string) => {
    e.preventDefault();
    if (draggedId && draggedId !== targetId && onMoveLayerOrder) {
      onMoveLayerOrder(draggedId, targetId, dropPosition);
    }
    setDraggedId(null);
    setDropTargetId(null);
  };

  // Hierarchical element tree building
  const rootElements = elements.filter((el) => !el.parentId);
  const childMap: Record<string, FigmaElement[]> = {};
  elements.forEach((el) => {
    if (el.parentId) {
      if (!childMap[el.parentId]) childMap[el.parentId] = [];
      childMap[el.parentId].push(el);
    }
  });

  // Filter & Search
  const matchesSearch = (el: FigmaElement) => {
    if (searchQuery && !el.name.toLowerCase().includes(searchQuery.toLowerCase())) {
      return false;
    }
    if (filterType === 'frames' && el.type !== 'frame' && !el.isFrame) return false;
    if (filterType === 'shapes' && !['rect', 'ellipse', 'star', 'polygon', 'line'].includes(el.type)) return false;
    if (filterType === 'text' && el.type !== 'text') return false;
    return true;
  };

  // Recursive tree renderer
  const renderLayerItem = (elem: FigmaElement, depth = 0) => {
    if (!matchesSearch(elem) && !searchQuery) return null;

    const isSelected = selectedIds.includes(elem.id);
    const isEditing = editingId === elem.id;
    const isContainer = elem.isFrame || elem.isGroup || elem.type === 'frame';
    const isCollapsed = collapsedGroups[elem.id];
    const children = childMap[elem.id] || [];
    const isTarget = dropTargetId === elem.id;

    return (
      <div key={elem.id}>
        <div
          draggable
          onDragStart={(e) => handleDragStart(e, elem.id)}
          onDragOver={(e) => handleDragOver(e, elem)}
          onDragLeave={handleDragLeave}
          onDrop={(e) => handleDrop(e, elem.id)}
          onContextMenu={(e) => {
            e.preventDefault();
            e.stopPropagation();
            onSelect(elem.id, false);
            if (onContextMenu) onContextMenu(e, elem.id);
          }}
          style={{
            display: 'flex',
            alignItems: 'center',
            padding: '5px 8px 5px 4px',
            paddingLeft: `${depth * 14 + 6}px`,
            backgroundColor: isSelected ? 'rgba(13, 153, 255, 0.18)' : isTarget && dropPosition === 'inside' ? 'rgba(16, 185, 129, 0.2)' : 'transparent',
            borderLeft: isSelected ? '2px solid #0D99FF' : '2px solid transparent',
            borderTop: isTarget && dropPosition === 'before' ? '2px solid #0D99FF' : 'none',
            borderBottom: isTarget && dropPosition === 'after' ? '2px solid #0D99FF' : 'none',
            cursor: 'pointer',
            gap: '6px',
            transition: 'background 0.1s ease',
            position: 'relative',
          }}
          className="layer-row"
          onClick={(e) => onSelect(elem.id, e.shiftKey || e.metaKey || e.ctrlKey)}
          onDoubleClick={() => handleStartRename(elem)}
        >
          {/* Caret for collapsible containers */}
          {isContainer ? (
            <span
              onClick={(e) => toggleGroupCollapse(elem.id, e)}
              style={{ display: 'flex', alignItems: 'center', cursor: 'pointer', padding: '2px', color: '#888' }}
            >
              {isCollapsed ? <ChevronRight size={12} /> : <ChevronDown size={12} />}
            </span>
          ) : (
            <span style={{ width: '12px' }} />
          )}

          {/* Layer Icon */}
          <span style={{ display: 'flex', alignItems: 'center' }}>
            {getElementIcon(elem)}
          </span>

          {/* Name or Rename Input */}
          {isEditing ? (
            <input
              type="text"
              className="figma-input"
              value={editName}
              onChange={(e) => setEditName(e.target.value)}
              onBlur={() => handleFinishRename(elem.id)}
              onKeyDown={(e) => e.key === 'Enter' && handleFinishRename(elem.id)}
              autoFocus
              style={{ height: '22px', fontSize: '11px', padding: '2px 4px', flex: 1 }}
              onClick={(e) => e.stopPropagation()}
            />
          ) : (
            <span
              style={{
                flex: 1,
                whiteSpace: 'nowrap',
                overflow: 'hidden',
                textOverflow: 'ellipsis',
                fontSize: '12px',
                color: elem.visible ? (isSelected ? '#FFFFFF' : 'var(--text-primary)') : 'var(--text-muted)',
              }}
              title={elem.name}
            >
              {elem.name}
            </span>
          )}

          {/* Action icons on row */}
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: '2px',
              opacity: isSelected ? 1 : 0.4,
            }}
            onClick={(e) => e.stopPropagation()}
          >
            {/* Visibility Toggle */}
            <button
              className="figma-btn"
              style={{ padding: '2px' }}
              onClick={() => onUpdateElement(elem.id, { visible: !elem.visible })}
              title={elem.visible ? 'Hide Layer' : 'Show Layer'}
            >
              {elem.visible ? <Eye size={12} /> : <EyeOff size={12} color="#EF4444" />}
            </button>

            {/* Lock Toggle */}
            <button
              className="figma-btn"
              style={{ padding: '2px' }}
              onClick={() => onUpdateElement(elem.id, { locked: !elem.locked })}
              title={elem.locked ? 'Unlock Layer' : 'Lock Layer'}
            >
              {elem.locked ? <Lock size={12} color="#F59E0B" /> : <Unlock size={12} />}
            </button>

            {/* Context menu icon trigger */}
            <button
              className="figma-btn"
              style={{ padding: '2px' }}
              onClick={(e) => {
                onSelect(elem.id, false);
                if (onContextMenu) onContextMenu(e, elem.id);
              }}
              title="Properties & Options"
            >
              <MoreVertical size={12} />
            </button>
          </div>
        </div>

        {/* Render child layers if not collapsed */}
        {!isCollapsed && children.length > 0 && (
          <div>
            {[...children].reverse().map((child) => renderLayerItem(child, depth + 1))}
          </div>
        )}
      </div>
    );
  };

  // Top-level reversed elements for visual stack (top layer at top of list)
  const displayRoots = [...rootElements].reverse();

  return (
    <aside
      style={{
        width: '260px',
        backgroundColor: 'var(--bg-panel)',
        borderRight: '1px solid var(--border-color)',
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        zIndex: 50,
      }}
    >
      {/* Sidebar Navigation Tabs */}
      <div
        style={{
          display: 'flex',
          borderBottom: '1px solid var(--border-color)',
          backgroundColor: 'var(--bg-header)',
        }}
      >
        <button
          className="figma-btn"
          style={{
            flex: 1,
            borderRadius: 0,
            padding: '10px 0',
            fontSize: '11px',
            fontWeight: 600,
            borderBottom: activeTab === 'layers' ? '2px solid var(--accent)' : '2px solid transparent',
            color: activeTab === 'layers' ? 'var(--text-primary)' : 'var(--text-secondary)',
          }}
          onClick={() => setActiveTab('layers')}
        >
          <LayersIcon size={13} />
          <span>Layers ({elements.length})</span>
        </button>

        <button
          className="figma-btn"
          style={{
            flex: 1,
            borderRadius: 0,
            padding: '10px 0',
            fontSize: '11px',
            fontWeight: 600,
            borderBottom: activeTab === 'assets' ? '2px solid var(--accent)' : '2px solid transparent',
            color: activeTab === 'assets' ? 'var(--text-primary)' : 'var(--text-secondary)',
          }}
          onClick={() => setActiveTab('assets')}
        >
          <Component size={13} />
          <span>Assets</span>
        </button>

        <button
          className="figma-btn"
          style={{
            flex: 1,
            borderRadius: 0,
            padding: '10px 0',
            fontSize: '11px',
            fontWeight: 600,
            borderBottom: activeTab === 'pages' ? '2px solid var(--accent)' : '2px solid transparent',
            color: activeTab === 'pages' ? 'var(--text-primary)' : 'var(--text-secondary)',
          }}
          onClick={() => setActiveTab('pages')}
        >
          <FileText size={13} />
          <span>Pages</span>
        </button>
      </div>

      {/* Tab: Layers */}
      {activeTab === 'layers' && (
        <div style={{ display: 'flex', flexDirection: 'column', flex: 1, overflow: 'hidden' }}>
          {/* Search & Filter Bar */}
          <div style={{ padding: '8px 10px', borderBottom: '1px solid var(--border-color)', display: 'flex', flexDirection: 'column', gap: '6px' }}>
            <div style={{ display: 'flex', alignItems: 'center', backgroundColor: '#141414', borderRadius: '4px', padding: '2px 6px' }}>
              <Search size={12} color="#888" style={{ marginRight: '4px' }} />
              <input
                type="text"
                placeholder="Search layers..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                style={{
                  background: 'transparent',
                  border: 'none',
                  outline: 'none',
                  color: '#FFF',
                  fontSize: '11px',
                  width: '100%',
                  padding: '4px 0',
                }}
              />
            </div>

            {/* Filter chips */}
            <div style={{ display: 'flex', gap: '4px' }}>
              {(['all', 'frames', 'shapes', 'text'] as const).map((t) => (
                <button
                  key={t}
                  className="figma-btn"
                  style={{
                    padding: '1px 6px',
                    fontSize: '10px',
                    borderRadius: '10px',
                    backgroundColor: filterType === t ? 'rgba(13, 153, 255, 0.2)' : 'transparent',
                    color: filterType === t ? '#0D99FF' : '#888',
                    border: filterType === t ? '1px solid rgba(13, 153, 255, 0.4)' : '1px solid transparent',
                  }}
                  onClick={() => setFilterType(t)}
                >
                  {t.charAt(0).toUpperCase() + t.slice(1)}
                </button>
              ))}
            </div>

            {/* Layer actions toolbar */}
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div style={{ display: 'flex', gap: '4px' }}>
                {onGroup && (
                  <button
                    className="figma-btn"
                    onClick={onGroup}
                    disabled={selectedIds.length === 0}
                    style={{ fontSize: '10px', padding: '2px 6px' }}
                    title="Group Selection (Ctrl+G)"
                  >
                    <FolderPlus size={11} /> Group
                  </button>
                )}
                {onUngroup && (
                  <button
                    className="figma-btn"
                    onClick={onUngroup}
                    disabled={selectedIds.length === 0}
                    style={{ fontSize: '10px', padding: '2px 6px' }}
                    title="Ungroup (Ctrl+Shift+G)"
                  >
                    Ungroup
                  </button>
                )}
              </div>

              {onOpenProperties && selectedIds.length === 1 && (
                <button
                  className="figma-btn"
                  onClick={onOpenProperties}
                  style={{ fontSize: '10px', padding: '2px 6px', color: '#0D99FF' }}
                  title="Element Properties (Alt+Enter)"
                >
                  <Sliders size={11} /> Properties
                </button>
              )}
            </div>
          </div>

          {/* Layer List */}
          <div style={{ flex: 1, overflowY: 'auto', padding: '4px 0' }}>
            {elements.length === 0 ? (
              <div
                style={{
                  padding: '30px 16px',
                  textAlign: 'center',
                  color: 'var(--text-secondary)',
                  fontSize: '11px',
                }}
              >
                No layers yet.
                <br />
                Draw shapes or add frames to get started!
              </div>
            ) : (
              displayRoots.map((elem) => renderLayerItem(elem, 0))
            )}
          </div>

          {/* Quick layer ordering actions footer */}
          {selectedIds.length === 1 && (
            <div
              style={{
                padding: '8px 12px',
                borderTop: '1px solid var(--border-color)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                fontSize: '11px',
                color: 'var(--text-secondary)',
                backgroundColor: 'var(--bg-header)',
              }}
            >
              <span>Layer Actions</span>
              <div style={{ display: 'flex', gap: '4px' }}>
                <button
                  className="figma-btn"
                  style={{ padding: '4px' }}
                  onClick={() => onReorderElement(selectedIds[0], 'up')}
                  title="Bring Forward"
                >
                  <ArrowUp size={12} />
                </button>
                <button
                  className="figma-btn"
                  style={{ padding: '4px' }}
                  onClick={() => onReorderElement(selectedIds[0], 'down')}
                  title="Send Backward"
                >
                  <ArrowDown size={12} />
                </button>
                <button
                  className="figma-btn"
                  style={{ padding: '4px' }}
                  onClick={() => onDuplicateElement(selectedIds[0])}
                  title="Duplicate (Ctrl+D)"
                >
                  <Copy size={12} />
                </button>
                <button
                  className="figma-btn"
                  style={{ padding: '4px', color: 'var(--danger)' }}
                  onClick={() => onDeleteElement(selectedIds[0])}
                  title="Delete"
                >
                  <Trash2 size={12} />
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Tab: Assets Library */}
      {activeTab === 'assets' && (
        <div style={{ flex: 1, overflowY: 'auto', padding: '12px' }}>
          <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '10px' }}>
            UI COMPONENTS KIT
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
            <div
              className="figma-btn"
              style={{
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'flex-start',
                padding: '10px',
                backgroundColor: 'var(--bg-header)',
                border: '1px solid var(--border-color)',
                borderRadius: '6px',
                cursor: 'pointer',
              }}
              onClick={() => onInsertAsset('primary-button')}
            >
              <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-primary)' }}>Primary Button</div>
              <div style={{ fontSize: '10px', color: 'var(--text-secondary)' }}>Accent interactive CTA</div>
            </div>

            <div
              className="figma-btn"
              style={{
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'flex-start',
                padding: '10px',
                backgroundColor: 'var(--bg-header)',
                border: '1px solid var(--border-color)',
                borderRadius: '6px',
                cursor: 'pointer',
              }}
              onClick={() => onInsertAsset('glass-card')}
            >
              <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-primary)' }}>Glass Card Frame</div>
              <div style={{ fontSize: '10px', color: 'var(--text-secondary)' }}>Translucent backdrop card</div>
            </div>

            <div
              className="figma-btn"
              style={{
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'flex-start',
                padding: '10px',
                backgroundColor: 'var(--bg-header)',
                border: '1px solid var(--border-color)',
                borderRadius: '6px',
                cursor: 'pointer',
              }}
              onClick={() => onInsertAsset('search-bar')}
            >
              <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-primary)' }}>Search Input</div>
              <div style={{ fontSize: '10px', color: 'var(--text-secondary)' }}>With icon and rounded pill</div>
            </div>

            <div
              className="figma-btn"
              style={{
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'flex-start',
                padding: '10px',
                backgroundColor: 'var(--bg-header)',
                border: '1px solid var(--border-color)',
                borderRadius: '6px',
                cursor: 'pointer',
              }}
              onClick={() => onInsertAsset('badge')}
            >
              <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-primary)' }}>Status Badge</div>
              <div style={{ fontSize: '10px', color: 'var(--text-secondary)' }}>Pill indicator</div>
            </div>
          </div>
        </div>
      )}

      {/* Tab: Pages */}
      {activeTab === 'pages' && (
        <div style={{ flex: 1, overflowY: 'auto', padding: '12px' }}>
          <div
            style={{
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              marginBottom: '10px',
            }}
          >
            <span style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)' }}>PAGES</span>
            <button
              className="figma-btn"
              style={{ padding: '2px 6px', fontSize: '11px' }}
              onClick={onAddPage}
            >
              <Plus size={12} />
              <span>Page</span>
            </button>
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
            {pages.map((p) => {
              const isCurrent = p.id === activePageId;
              return (
                <div
                  key={p.id}
                  style={{
                    padding: '8px 10px',
                    borderRadius: '4px',
                    backgroundColor: isCurrent ? 'var(--accent-light)' : 'transparent',
                    borderLeft: isCurrent ? '2px solid var(--accent)' : '2px solid transparent',
                    cursor: 'pointer',
                    fontSize: '12px',
                    color: isCurrent ? '#FFF' : 'var(--text-primary)',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px',
                  }}
                  onClick={() => onSelectPage(p.id)}
                >
                  <FileText size={13} color={isCurrent ? 'var(--accent)' : '#888'} />
                  <span>{p.name}</span>
                </div>
              );
            })}
          </div>
        </div>
      )}
    </aside>
  );
};
