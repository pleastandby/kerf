import React, { useState } from 'react';
import {
  AlignLeft,
  AlignCenter,
  AlignRight,
  Maximize2,
  Minimize2,
  Copy,
  Check,
  Link,
  Type,
  Plus,
  Trash2,
  Eye,
  EyeOff,
  MoveHorizontal,
  LayoutGrid,
  ArrowRight,
  ArrowDown,
  WrapText,
  Grid,
  MoveLeft,
  MoveRight,
} from 'lucide-react';
import type { FigmaElement, Effect } from '../types';
import { generateCSS, generateTailwind, generateSVGCode } from '../utils/codeGenerator';

interface RightSidebarProps {
  selectedElements: FigmaElement[];
  allFrames: FigmaElement[];
  allElements?: FigmaElement[];
  onUpdateElement: (id: string, updates: Partial<FigmaElement>, recordHistory?: boolean) => void;
  onAlign: (alignment: 'left' | 'center' | 'right' | 'top' | 'middle' | 'bottom') => void;
  onDistribute: (axis: 'horizontal' | 'vertical') => void;
  onAutoLayout?: () => void;
  onRemoveAutoLayout?: () => void;
  onReorderChild?: (childId: string, direction: 'prev' | 'next') => void;
}

export const RightSidebar: React.FC<RightSidebarProps> = ({
  selectedElements,
  allFrames,
  allElements = [],
  onUpdateElement,
  onAlign,
  onDistribute,
  onAutoLayout,
  onRemoveAutoLayout,
  onReorderChild,
}) => {
  const [activeTab, setActiveTab] = useState<'design' | 'prototype' | 'inspect'>('design');
  const [inspectMode, setInspectMode] = useState<'css' | 'tailwind' | 'svg'>('css');
  const [copiedCode, setCopiedCode] = useState(false);

  const primaryElement = selectedElements[0] || null;
  const parentFrame = primaryElement?.parentId
    ? allElements.find((e) => e.id === primaryElement.parentId)
    : null;
  const isChildOfAutoLayout = parentFrame?.isAutoLayout === true;

  const handleCopyCode = (text: string) => {
    navigator.clipboard.writeText(text);
    setCopiedCode(true);
    setTimeout(() => setCopiedCode(false), 2000);
  };

  const handleAddShadow = (elem: FigmaElement) => {
    const newEffect: Effect = {
      id: `eff-${Date.now()}`,
      type: 'drop-shadow',
      x: 0,
      y: 10,
      blur: 20,
      spread: 0,
      color: 'rgba(0, 0, 0, 0.4)',
      enabled: true,
    };
    onUpdateElement(elem.id, {
      effects: [...(elem.effects || []), newEffect],
    });
  };

  const handleRemoveEffect = (elem: FigmaElement, effId: string) => {
    onUpdateElement(elem.id, {
      effects: elem.effects.filter((e) => e.id !== effId),
    });
  };

  return (
    <aside
      style={{
        width: '280px',
        backgroundColor: 'var(--bg-panel)',
        borderLeft: '1px solid var(--border-color)',
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        zIndex: 50,
      }}
    >
      {/* Right Sidebar Tabs */}
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
            borderBottom: activeTab === 'design' ? '2px solid var(--accent)' : '2px solid transparent',
            color: activeTab === 'design' ? 'var(--text-primary)' : 'var(--text-secondary)',
          }}
          onClick={() => setActiveTab('design')}
        >
          Design
        </button>

        <button
          className="figma-btn"
          style={{
            flex: 1,
            borderRadius: 0,
            padding: '10px 0',
            fontSize: '11px',
            fontWeight: 600,
            borderBottom: activeTab === 'prototype' ? '2px solid var(--accent)' : '2px solid transparent',
            color: activeTab === 'prototype' ? 'var(--text-primary)' : 'var(--text-secondary)',
          }}
          onClick={() => setActiveTab('prototype')}
        >
          Prototype
        </button>

        <button
          className="figma-btn"
          style={{
            flex: 1,
            borderRadius: 0,
            padding: '10px 0',
            fontSize: '11px',
            fontWeight: 600,
            borderBottom: activeTab === 'inspect' ? '2px solid var(--accent)' : '2px solid transparent',
            color: activeTab === 'inspect' ? 'var(--text-primary)' : 'var(--text-secondary)',
          }}
          onClick={() => setActiveTab('inspect')}
        >
          Inspect
        </button>
      </div>

      {/* Tab: Design */}
      {activeTab === 'design' && (
        <div style={{ flex: 1, overflowY: 'auto', padding: '12px' }}>
          {selectedElements.length === 0 ? (
            <div
              style={{
                padding: '40px 10px',
                textAlign: 'center',
                color: 'var(--text-secondary)',
                fontSize: '11px',
              }}
            >
              Select an element or frame on canvas to inspect and modify properties.
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              {/* Alignment Bar */}
              <div>
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    background: 'var(--bg-input)',
                    padding: '4px',
                    borderRadius: '6px',
                    border: '1px solid var(--border-color)',
                  }}
                >
                  <button
                    className="figma-btn"
                    style={{ padding: '5px' }}
                    onClick={() => onAlign('left')}
                    title="Align Left"
                  >
                    <AlignLeft size={13} />
                  </button>
                  <button
                    className="figma-btn"
                    style={{ padding: '5px' }}
                    onClick={() => onAlign('center')}
                    title="Align Horizontal Center"
                  >
                    <AlignCenter size={13} />
                  </button>
                  <button
                    className="figma-btn"
                    style={{ padding: '5px' }}
                    onClick={() => onAlign('right')}
                    title="Align Right"
                  >
                    <AlignRight size={13} />
                  </button>
                  <div style={{ width: '1px', height: '14px', background: 'var(--border-color)' }} />
                  <button
                    className="figma-btn"
                    style={{ padding: '5px' }}
                    onClick={() => onAlign('top')}
                    title="Align Top"
                  >
                    <Maximize2 size={13} style={{ transform: 'rotate(45deg)' }} />
                  </button>
                  <button
                    className="figma-btn"
                    style={{ padding: '5px' }}
                    onClick={() => onAlign('middle')}
                    title="Align Vertical Center"
                  >
                    <Minimize2 size={13} style={{ transform: 'rotate(45deg)' }} />
                  </button>
                  <button
                    className="figma-btn"
                    style={{ padding: '5px' }}
                    onClick={() => onAlign('bottom')}
                    title="Align Bottom"
                  >
                    <Maximize2 size={13} style={{ transform: 'rotate(-135deg)' }} />
                  </button>
                  <button
                    className="figma-btn"
                    style={{ padding: '5px' }}
                    onClick={() => onDistribute('horizontal')}
                    title="Distribute Horizontal Spacing"
                  >
                    <MoveHorizontal size={13} />
                  </button>
                  {onAutoLayout && (
                    <>
                      <div style={{ width: '1px', height: '14px', background: 'var(--border-color)' }} />
                      <button
                        className="figma-btn"
                        style={{
                          padding: '5px',
                          color: primaryElement?.isAutoLayout ? 'var(--accent)' : 'var(--text-secondary)',
                        }}
                        onClick={onAutoLayout}
                        title="Auto Layout (Shift+A)"
                      >
                        <LayoutGrid size={13} />
                      </button>
                    </>
                  )}
                </div>
              </div>

              {/* Transform (X, Y, W, H, Rotation, Corner Radius) */}
              {primaryElement && (
                <div style={{ borderBottom: '1px solid var(--border-color)', paddingBottom: '14px' }}>
                  <div
                    style={{
                      fontSize: '11px',
                      fontWeight: 600,
                      color: 'var(--text-secondary)',
                      marginBottom: '8px',
                    }}
                  >
                    TRANSFORM
                  </div>

                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px' }}>
                    {/* X */}
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                      <span style={{ fontSize: '11px', color: 'var(--text-secondary)', width: '12px' }}>
                        X
                      </span>
                      <input
                        type="number"
                        className="figma-input"
                        value={Math.round(primaryElement.x)}
                        onChange={(e) =>
                          onUpdateElement(primaryElement.id, { x: parseFloat(e.target.value) || 0 })
                        }
                      />
                    </div>

                    {/* Y */}
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                      <span style={{ fontSize: '11px', color: 'var(--text-secondary)', width: '12px' }}>
                        Y
                      </span>
                      <input
                        type="number"
                        className="figma-input"
                        value={Math.round(primaryElement.y)}
                        onChange={(e) =>
                          onUpdateElement(primaryElement.id, { y: parseFloat(e.target.value) || 0 })
                        }
                      />
                    </div>

                    {/* W */}
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                      <span style={{ fontSize: '11px', color: 'var(--text-secondary)', width: '12px' }}>
                        W
                      </span>
                      <input
                        type="number"
                        className="figma-input"
                        value={Math.round(primaryElement.width)}
                        onChange={(e) =>
                          onUpdateElement(primaryElement.id, {
                            width: Math.max(1, parseFloat(e.target.value) || 1),
                          })
                        }
                      />
                    </div>

                    {/* H */}
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                      <span style={{ fontSize: '11px', color: 'var(--text-secondary)', width: '12px' }}>
                        H
                      </span>
                      <input
                        type="number"
                        className="figma-input"
                        value={Math.round(primaryElement.height)}
                        onChange={(e) =>
                          onUpdateElement(primaryElement.id, {
                            height: Math.max(1, parseFloat(e.target.value) || 1),
                          })
                        }
                      />
                    </div>

                    {/* Rotation */}
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                      <span style={{ fontSize: '11px', color: 'var(--text-secondary)', width: '12px' }}>
                        ∠
                      </span>
                      <input
                        type="number"
                        className="figma-input"
                        value={Math.round(primaryElement.rotation)}
                        onChange={(e) =>
                          onUpdateElement(primaryElement.id, {
                            rotation: parseFloat(e.target.value) || 0,
                          })
                        }
                      />
                    </div>

                    {/* Corner Radius */}
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                      <span
                        style={{
                          fontSize: '11px',
                          color: 'var(--text-secondary)',
                          width: '12px',
                          cursor: 'ew-resize',
                          userSelect: 'none',
                        }}
                        title="Click and drag to adjust corner radius"
                        onMouseDown={(e) => {
                          e.preventDefault();
                          const startX = e.clientX;
                          const initialRadius = primaryElement.cornerRadius || 0;
                          const onMouseMove = (moveEvent: MouseEvent) => {
                            const delta = moveEvent.clientX - startX;
                            const newRadius = Math.max(0, Math.round(initialRadius + delta));
                            onUpdateElement(primaryElement.id, { cornerRadius: newRadius });
                          };
                          const onMouseUp = () => {
                            window.removeEventListener('mousemove', onMouseMove);
                            window.removeEventListener('mouseup', onMouseUp);
                          };
                          window.addEventListener('mousemove', onMouseMove);
                          window.addEventListener('mouseup', onMouseUp);
                        }}
                      >
                        ◜
                      </span>
                      <input
                        type="number"
                        className="figma-input"
                        value={primaryElement.cornerRadius || 0}
                        onChange={(e) =>
                          onUpdateElement(primaryElement.id, {
                            cornerRadius: Math.max(0, parseFloat(e.target.value) || 0),
                          })
                        }
                      />
                    </div>
                  </div>
                </div>
              )}

              {/* Quick Auto Layout Creator button for multiple elements or non-frames */}
              {onAutoLayout && !primaryElement?.isAutoLayout && (
                <button
                  className="figma-btn"
                  style={{
                    width: '100%',
                    padding: '8px 12px',
                    background: 'rgba(13, 153, 255, 0.12)',
                    border: '1px solid var(--accent)',
                    color: '#0D99FF',
                    fontWeight: 600,
                    fontSize: '11px',
                    gap: '6px',
                    justifyContent: 'center',
                    borderRadius: '6px',
                  }}
                  onClick={onAutoLayout}
                  title="Wrap in Auto Layout frame (Shift+A)"
                >
                  <LayoutGrid size={14} />
                  <span>Add Auto Layout Frame (Shift+A)</span>
                </button>
              )}

              {/* Auto Layout Section for Frames */}
              {primaryElement && (primaryElement.isFrame || primaryElement.type === 'frame') && (
                <div style={{ borderBottom: '1px solid var(--border-color)', paddingBottom: '14px' }}>
                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      marginBottom: '10px',
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                      <LayoutGrid size={13} color="var(--accent)" />
                      <span style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)' }}>
                        AUTO LAYOUT
                      </span>
                    </div>
                    {primaryElement.isAutoLayout ? (
                      <button
                        className="figma-btn"
                        style={{ padding: '2px 6px', fontSize: '10px', color: '#EF4444' }}
                        onClick={() =>
                          onRemoveAutoLayout
                            ? onRemoveAutoLayout()
                            : onUpdateElement(primaryElement.id, { isAutoLayout: false }, true)
                        }
                        title="Remove Auto Layout (Alt+Shift+A)"
                      >
                        <Trash2 size={11} />
                      </button>
                    ) : (
                      <button
                        className="figma-btn"
                        style={{
                          padding: '3px 8px',
                          fontSize: '10px',
                          color: '#0D99FF',
                          background: 'rgba(13, 153, 255, 0.1)',
                        }}
                        onClick={() =>
                          onAutoLayout
                            ? onAutoLayout()
                            : onUpdateElement(
                                primaryElement.id,
                                {
                                  isAutoLayout: true,
                                  layoutMode: 'horizontal',
                                  itemSpacing: 12,
                                  paddingTop: 16,
                                  paddingRight: 16,
                                  paddingBottom: 16,
                                  paddingLeft: 16,
                                  alignItems: 'start',
                                  justifyContent: 'start',
                                  layoutSizingHorizontal: 'hug',
                                  layoutSizingVertical: 'hug',
                                },
                                true
                              )
                        }
                        title="Enable Auto Layout (Shift+A)"
                      >
                        <Plus size={11} /> Add
                      </button>
                    )}
                  </div>

                  {primaryElement.isAutoLayout && (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                      {/* Direction Segmented Control */}
                      <div>
                        <span
                          style={{
                            fontSize: '10px',
                            color: 'var(--text-secondary)',
                            display: 'block',
                            marginBottom: '4px',
                          }}
                        >
                          DIRECTION / LAYOUT MODE
                        </span>
                        <div
                          style={{
                            display: 'grid',
                            gridTemplateColumns: 'repeat(4, 1fr)',
                            gap: '4px',
                            background: 'var(--bg-input)',
                            padding: '3px',
                            borderRadius: '6px',
                          }}
                        >
                          <button
                            className="figma-btn"
                            style={{
                              padding: '6px 0',
                              display: 'flex',
                              flexDirection: 'column',
                              alignItems: 'center',
                              gap: '2px',
                              fontSize: '10px',
                              background:
                                (primaryElement.layoutMode || 'horizontal') === 'horizontal'
                                  ? 'var(--accent)'
                                  : 'transparent',
                              color:
                                (primaryElement.layoutMode || 'horizontal') === 'horizontal'
                                  ? '#fff'
                                  : 'var(--text-secondary)',
                              borderRadius: '4px',
                            }}
                            onClick={() => onUpdateElement(primaryElement.id, { layoutMode: 'horizontal' }, true)}
                            title="Horizontal Row (flex-direction: row)"
                          >
                            <ArrowRight size={13} />
                            <span>Row</span>
                          </button>
                          <button
                            className="figma-btn"
                            style={{
                              padding: '6px 0',
                              display: 'flex',
                              flexDirection: 'column',
                              alignItems: 'center',
                              gap: '2px',
                              fontSize: '10px',
                              background:
                                primaryElement.layoutMode === 'vertical' ? 'var(--accent)' : 'transparent',
                              color:
                                primaryElement.layoutMode === 'vertical' ? '#fff' : 'var(--text-secondary)',
                              borderRadius: '4px',
                            }}
                            onClick={() => onUpdateElement(primaryElement.id, { layoutMode: 'vertical' }, true)}
                            title="Vertical Column (flex-direction: column)"
                          >
                            <ArrowDown size={13} />
                            <span>Col</span>
                          </button>
                          <button
                            className="figma-btn"
                            style={{
                              padding: '6px 0',
                              display: 'flex',
                              flexDirection: 'column',
                              alignItems: 'center',
                              gap: '2px',
                              fontSize: '10px',
                              background: primaryElement.layoutMode === 'wrap' ? 'var(--accent)' : 'transparent',
                              color: primaryElement.layoutMode === 'wrap' ? '#fff' : 'var(--text-secondary)',
                              borderRadius: '4px',
                            }}
                            onClick={() => onUpdateElement(primaryElement.id, { layoutMode: 'wrap' }, true)}
                            title="Wrap (flex-wrap: wrap)"
                          >
                            <WrapText size={13} />
                            <span>Wrap</span>
                          </button>
                          <button
                            className="figma-btn"
                            style={{
                              padding: '6px 0',
                              display: 'flex',
                              flexDirection: 'column',
                              alignItems: 'center',
                              gap: '2px',
                              fontSize: '10px',
                              background: primaryElement.layoutMode === 'grid' ? 'var(--accent)' : 'transparent',
                              color: primaryElement.layoutMode === 'grid' ? '#fff' : 'var(--text-secondary)',
                              borderRadius: '4px',
                            }}
                            onClick={() => onUpdateElement(primaryElement.id, { layoutMode: 'grid' }, true)}
                            title="Grid Matrix Layout"
                          >
                            <Grid size={13} />
                            <span>Grid</span>
                          </button>
                        </div>
                      </div>

                      {/* 3x3 Alignment Matrix (Figma signature widget) */}
                      <div>
                        <div
                          style={{
                            display: 'flex',
                            justifyContent: 'space-between',
                            alignItems: 'center',
                            marginBottom: '6px',
                          }}
                        >
                          <span style={{ fontSize: '10px', color: 'var(--text-secondary)' }}>
                            ALIGNMENT & DISTRIBUTION
                          </span>
                          <span style={{ fontSize: '10px', color: 'var(--accent)' }}>
                            {primaryElement.justifyContent === 'space-between' ? 'Space between' : 'Packed'}
                          </span>
                        </div>

                        <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
                          {/* 3x3 Matrix */}
                          <div
                            style={{
                              width: '74px',
                              height: '74px',
                              background: 'var(--bg-input)',
                              borderRadius: '6px',
                              border: '1px solid var(--border-color)',
                              display: 'grid',
                              gridTemplateColumns: 'repeat(3, 1fr)',
                              gridTemplateRows: 'repeat(3, 1fr)',
                              padding: '3px',
                              gap: '2px',
                            }}
                            title="Interactive Alignment Matrix"
                          >
                            {(['start', 'center', 'end'] as const).map((vAlign) =>
                              (['start', 'center', 'end'] as const).map((hAlign) => {
                                const isCurrent =
                                  primaryElement.alignItems ===
                                    (primaryElement.layoutMode === 'vertical' ? hAlign : vAlign) &&
                                  (primaryElement.justifyContent === 'space-between'
                                    ? false
                                    : primaryElement.justifyContent ===
                                      (primaryElement.layoutMode === 'vertical' ? vAlign : hAlign));

                                return (
                                  <div
                                    key={`${vAlign}-${hAlign}`}
                                    onClick={() => {
                                      const newAlign =
                                        primaryElement.layoutMode === 'vertical' ? hAlign : vAlign;
                                      const newJustify =
                                        primaryElement.layoutMode === 'vertical' ? vAlign : hAlign;
                                      onUpdateElement(
                                        primaryElement.id,
                                        { alignItems: newAlign, justifyContent: newJustify },
                                        true
                                      );
                                    }}
                                    style={{
                                      borderRadius: '3px',
                                      background: isCurrent ? 'var(--accent)' : 'transparent',
                                      display: 'flex',
                                      alignItems: 'center',
                                      justifyContent: 'center',
                                      cursor: 'pointer',
                                      transition: 'background 0.15s ease',
                                    }}
                                  >
                                    <div
                                      style={{
                                        width: isCurrent ? '7px' : '4px',
                                        height: isCurrent ? '7px' : '4px',
                                        borderRadius: '50%',
                                        background: isCurrent ? '#fff' : 'var(--text-muted)',
                                      }}
                                    />
                                  </div>
                                );
                              })
                            )}
                          </div>

                          {/* Quick toggles */}
                          <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: '6px' }}>
                            <button
                              className="figma-btn"
                              style={{
                                padding: '5px 8px',
                                fontSize: '10px',
                                justifyContent: 'space-between',
                                background:
                                  primaryElement.justifyContent === 'space-between'
                                    ? 'rgba(13, 153, 255, 0.2)'
                                    : 'var(--bg-input)',
                                border:
                                  primaryElement.justifyContent === 'space-between'
                                    ? '1px solid var(--accent)'
                                    : '1px solid var(--border-color)',
                                color:
                                  primaryElement.justifyContent === 'space-between'
                                    ? '#0D99FF'
                                    : 'var(--text-primary)',
                              }}
                              onClick={() =>
                                onUpdateElement(
                                  primaryElement.id,
                                  {
                                    justifyContent:
                                      primaryElement.justifyContent === 'space-between'
                                        ? 'start'
                                        : 'space-between',
                                  },
                                  true
                                )
                              }
                            >
                              <span>Space between</span>
                              <span style={{ fontSize: '9px', opacity: 0.7 }}>Auto</span>
                            </button>

                            <button
                              className="figma-btn"
                              style={{
                                padding: '5px 8px',
                                fontSize: '10px',
                                justifyContent: 'space-between',
                                background:
                                  primaryElement.justifyContent === 'center'
                                    ? 'rgba(13, 153, 255, 0.2)'
                                    : 'var(--bg-input)',
                                border:
                                  primaryElement.justifyContent === 'center'
                                    ? '1px solid var(--accent)'
                                    : '1px solid var(--border-color)',
                                color:
                                  primaryElement.justifyContent === 'center'
                                    ? '#0D99FF'
                                    : 'var(--text-primary)',
                              }}
                              onClick={() =>
                                onUpdateElement(
                                  primaryElement.id,
                                  {
                                    justifyContent: 'center',
                                    alignItems: 'center',
                                  },
                                  true
                                )
                              }
                            >
                              <span>Center Both</span>
                              <span>•</span>
                            </button>
                          </div>
                        </div>
                      </div>

                      {/* Item Spacing (Gap) */}
                      <div
                        style={{
                          display: 'grid',
                          gridTemplateColumns: '1fr 1fr',
                          gap: '8px',
                          alignItems: 'center',
                        }}
                      >
                        <div>
                          <span
                            style={{
                              fontSize: '10px',
                              color: 'var(--text-secondary)',
                              display: 'block',
                              marginBottom: '4px',
                            }}
                          >
                            ITEM GAP (SPACING)
                          </span>
                          <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                            <input
                              type="number"
                              min="0"
                              className="figma-input"
                              value={primaryElement.itemSpacing ?? 12}
                              onChange={(e) =>
                                onUpdateElement(
                                  primaryElement.id,
                                  { itemSpacing: Math.max(0, parseInt(e.target.value) || 0) },
                                  true
                                )
                              }
                            />
                            <span style={{ fontSize: '10px', color: 'var(--text-muted)' }}>px</span>
                          </div>
                        </div>

                        <div>
                          <span
                            style={{
                              fontSize: '10px',
                              color: 'var(--text-secondary)',
                              display: 'block',
                              marginBottom: '4px',
                            }}
                          >
                            PRESET GAPS
                          </span>
                          <div style={{ display: 'flex', gap: '3px' }}>
                            {[8, 12, 16, 24].map((gapVal) => (
                              <button
                                key={gapVal}
                                className="figma-btn"
                                style={{
                                  flex: 1,
                                  padding: '3px 0',
                                  fontSize: '10px',
                                  background:
                                    primaryElement.itemSpacing === gapVal
                                      ? 'var(--accent)'
                                      : 'var(--bg-input)',
                                  color: primaryElement.itemSpacing === gapVal ? '#fff' : 'var(--text-primary)',
                                }}
                                onClick={() => onUpdateElement(primaryElement.id, { itemSpacing: gapVal }, true)}
                              >
                                {gapVal}
                              </button>
                            ))}
                          </div>
                        </div>
                      </div>

                      {/* Padding Section */}
                      <div>
                        <span
                          style={{
                            fontSize: '10px',
                            color: 'var(--text-secondary)',
                            display: 'block',
                            marginBottom: '6px',
                          }}
                        >
                          PADDING (T, R, B, L)
                        </span>

                        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '6px' }}>
                          <div>
                            <span style={{ fontSize: '9px', color: 'var(--text-muted)', display: 'block', textAlign: 'center' }}>Top</span>
                            <input
                              type="number"
                              min="0"
                              className="figma-input"
                              style={{ textAlign: 'center' }}
                              value={primaryElement.paddingTop ?? 16}
                              onChange={(e) =>
                                onUpdateElement(
                                  primaryElement.id,
                                  { paddingTop: Math.max(0, parseInt(e.target.value) || 0) },
                                  true
                                )
                              }
                            />
                          </div>
                          <div>
                            <span style={{ fontSize: '9px', color: 'var(--text-muted)', display: 'block', textAlign: 'center' }}>Right</span>
                            <input
                              type="number"
                              min="0"
                              className="figma-input"
                              style={{ textAlign: 'center' }}
                              value={primaryElement.paddingRight ?? 16}
                              onChange={(e) =>
                                onUpdateElement(
                                  primaryElement.id,
                                  { paddingRight: Math.max(0, parseInt(e.target.value) || 0) },
                                  true
                                )
                              }
                            />
                          </div>
                          <div>
                            <span style={{ fontSize: '9px', color: 'var(--text-muted)', display: 'block', textAlign: 'center' }}>Bottom</span>
                            <input
                              type="number"
                              min="0"
                              className="figma-input"
                              style={{ textAlign: 'center' }}
                              value={primaryElement.paddingBottom ?? 16}
                              onChange={(e) =>
                                onUpdateElement(
                                  primaryElement.id,
                                  { paddingBottom: Math.max(0, parseInt(e.target.value) || 0) },
                                  true
                                )
                              }
                            />
                          </div>
                          <div>
                            <span style={{ fontSize: '9px', color: 'var(--text-muted)', display: 'block', textAlign: 'center' }}>Left</span>
                            <input
                              type="number"
                              min="0"
                              className="figma-input"
                              style={{ textAlign: 'center' }}
                              value={primaryElement.paddingLeft ?? 16}
                              onChange={(e) =>
                                onUpdateElement(
                                  primaryElement.id,
                                  { paddingLeft: Math.max(0, parseInt(e.target.value) || 0) },
                                  true
                                )
                              }
                            />
                          </div>
                        </div>
                      </div>

                      {/* Resizing Behavior */}
                      <div>
                        <span
                          style={{
                            fontSize: '10px',
                            color: 'var(--text-secondary)',
                            display: 'block',
                            marginBottom: '6px',
                          }}
                        >
                          FRAME SIZING
                        </span>
                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px' }}>
                          <div>
                            <span style={{ fontSize: '9px', color: 'var(--text-muted)' }}>Width</span>
                            <select
                              className="figma-input"
                              value={primaryElement.layoutSizingHorizontal || 'hug'}
                              onChange={(e) =>
                                onUpdateElement(
                                  primaryElement.id,
                                  { layoutSizingHorizontal: e.target.value as any },
                                  true
                                )
                              }
                              style={{ width: '100%', fontSize: '11px', height: '26px' }}
                            >
                              <option value="hug">Hug contents</option>
                              <option value="fixed">Fixed width</option>
                            </select>
                          </div>
                          <div>
                            <span style={{ fontSize: '9px', color: 'var(--text-muted)' }}>Height</span>
                            <select
                              className="figma-input"
                              value={primaryElement.layoutSizingVertical || 'hug'}
                              onChange={(e) =>
                                onUpdateElement(
                                  primaryElement.id,
                                  { layoutSizingVertical: e.target.value as any },
                                  true
                                )
                              }
                              style={{ width: '100%', fontSize: '11px', height: '26px' }}
                            >
                              <option value="hug">Hug contents</option>
                              <option value="fixed">Fixed height</option>
                            </select>
                          </div>
                        </div>
                      </div>
                    </div>
                  )}
                </div>
              )}

              {/* Child of Auto Layout Frame */}
              {isChildOfAutoLayout && primaryElement && (
                <div style={{ borderBottom: '1px solid var(--border-color)', paddingBottom: '14px' }}>
                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      marginBottom: '8px',
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                      <LayoutGrid size={13} color="#10B981" />
                      <span style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)' }}>
                        AUTO LAYOUT CHILD
                      </span>
                    </div>
                    <span style={{ fontSize: '10px', color: '#10B981' }}>Inside Frame</span>
                  </div>

                  <div style={{ display: 'flex', gap: '6px' }}>
                    <button
                      className="figma-btn"
                      style={{ flex: 1, padding: '6px', fontSize: '11px', gap: '4px' }}
                      onClick={() => onReorderChild && onReorderChild(primaryElement.id, 'prev')}
                      title="Move Earlier in Layout Order"
                    >
                      <MoveLeft size={13} />
                      <span>Move Earlier</span>
                    </button>
                    <button
                      className="figma-btn"
                      style={{ flex: 1, padding: '6px', fontSize: '11px', gap: '4px' }}
                      onClick={() => onReorderChild && onReorderChild(primaryElement.id, 'next')}
                      title="Move Later in Layout Order"
                    >
                      <span>Move Later</span>
                      <MoveRight size={13} />
                    </button>
                  </div>
                </div>
              )}

              {/* Typography Section (if Text Element) */}
              {primaryElement && primaryElement.type === 'text' && (
                <div style={{ borderBottom: '1px solid var(--border-color)', paddingBottom: '14px' }}>
                  <div
                    style={{
                      fontSize: '11px',
                      fontWeight: 600,
                      color: 'var(--text-secondary)',
                      marginBottom: '8px',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '6px',
                    }}
                  >
                    <Type size={12} />
                    <span>TYPOGRAPHY</span>
                  </div>

                  {/* Text string edit */}
                  <div style={{ marginBottom: '8px' }}>
                    <textarea
                      className="figma-input"
                      rows={2}
                      value={primaryElement.text || ''}
                      onChange={(e) =>
                        onUpdateElement(primaryElement.id, { text: e.target.value })
                      }
                      style={{ resize: 'vertical', fontSize: '12px' }}
                    />
                  </div>

                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px' }}>
                    {/* Font Family */}
                    <select
                      className="figma-input"
                      value={primaryElement.fontFamily || 'Inter'}
                      onChange={(e) =>
                        onUpdateElement(primaryElement.id, { fontFamily: e.target.value })
                      }
                    >
                      <option value="Inter">Inter</option>
                      <option value="Roboto">Roboto</option>
                      <option value="Fira Code">Fira Code</option>
                      <option value="Outfit">Outfit</option>
                      <option value="serif">Playfair (Serif)</option>
                    </select>

                    {/* Font Weight */}
                    <select
                      className="figma-input"
                      value={primaryElement.fontWeight || 400}
                      onChange={(e) =>
                        onUpdateElement(primaryElement.id, {
                          fontWeight: parseInt(e.target.value) || 400,
                        })
                      }
                    >
                      <option value={300}>Light (300)</option>
                      <option value={400}>Regular (400)</option>
                      <option value={500}>Medium (500)</option>
                      <option value={600}>SemiBold (600)</option>
                      <option value={700}>Bold (700)</option>
                      <option value={800}>ExtraBold (800)</option>
                    </select>

                    {/* Font Size */}
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                      <span style={{ fontSize: '10px', color: 'var(--text-secondary)' }}>Size</span>
                      <input
                        type="number"
                        className="figma-input"
                        value={primaryElement.fontSize || 16}
                        onChange={(e) =>
                          onUpdateElement(primaryElement.id, {
                            fontSize: Math.max(6, parseFloat(e.target.value) || 16),
                          })
                        }
                      />
                    </div>

                    {/* Text Align */}
                    <div
                      style={{
                        display: 'flex',
                        background: 'var(--bg-input)',
                        borderRadius: '5px',
                        border: '1px solid var(--border-color)',
                      }}
                    >
                      <button
                        className={`figma-btn ${primaryElement.textAlign === 'left' ? 'active' : ''}`}
                        style={{ flex: 1, padding: '3px 0' }}
                        onClick={() => onUpdateElement(primaryElement.id, { textAlign: 'left' })}
                      >
                        <AlignLeft size={11} />
                      </button>
                      <button
                        className={`figma-btn ${primaryElement.textAlign === 'center' ? 'active' : ''}`}
                        style={{ flex: 1, padding: '3px 0' }}
                        onClick={() => onUpdateElement(primaryElement.id, { textAlign: 'center' })}
                      >
                        <AlignCenter size={11} />
                      </button>
                      <button
                        className={`figma-btn ${primaryElement.textAlign === 'right' ? 'active' : ''}`}
                        style={{ flex: 1, padding: '3px 0' }}
                        onClick={() => onUpdateElement(primaryElement.id, { textAlign: 'right' })}
                      >
                        <AlignRight size={11} />
                      </button>
                    </div>
                  </div>
                </div>
              )}

              {/* Fill Section */}
              {primaryElement && (
                <div style={{ borderBottom: '1px solid var(--border-color)', paddingBottom: '14px' }}>
                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      marginBottom: '8px',
                    }}
                  >
                    <span style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)' }}>
                      FILL
                    </span>

                    {/* Fill mode toggle */}
                    <select
                      className="figma-input"
                      value={primaryElement.fillType || 'solid'}
                      onChange={(e) =>
                        onUpdateElement(primaryElement.id, {
                          fillType: e.target.value as 'solid' | 'linear-gradient',
                          fillGradient:
                            e.target.value === 'linear-gradient'
                              ? { start: primaryElement.fill || '#0D99FF', end: '#7C3AED', angle: 90 }
                              : undefined,
                        })
                      }
                      style={{ width: '110px', height: '22px', fontSize: '10px' }}
                    >
                      <option value="solid">Solid Color</option>
                      <option value="linear-gradient">Gradient</option>
                    </select>
                  </div>

                  {primaryElement.fillType === 'linear-gradient' && primaryElement.fillGradient ? (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                        <input
                          type="color"
                          value={primaryElement.fillGradient.start}
                          onChange={(e) =>
                            onUpdateElement(primaryElement.id, {
                              fillGradient: {
                                ...primaryElement.fillGradient!,
                                start: e.target.value,
                              },
                            })
                          }
                          style={{
                            width: '26px',
                            height: '24px',
                            border: 'none',
                            borderRadius: '4px',
                            cursor: 'pointer',
                            background: 'transparent',
                          }}
                        />
                        <input
                          type="text"
                          className="figma-input"
                          value={primaryElement.fillGradient.start}
                          onChange={(e) =>
                            onUpdateElement(primaryElement.id, {
                              fillGradient: {
                                ...primaryElement.fillGradient!,
                                start: e.target.value,
                              },
                            })
                          }
                        />
                      </div>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                        <input
                          type="color"
                          value={primaryElement.fillGradient.end}
                          onChange={(e) =>
                            onUpdateElement(primaryElement.id, {
                              fillGradient: {
                                ...primaryElement.fillGradient!,
                                end: e.target.value,
                              },
                            })
                          }
                          style={{
                            width: '26px',
                            height: '24px',
                            border: 'none',
                            borderRadius: '4px',
                            cursor: 'pointer',
                            background: 'transparent',
                          }}
                        />
                        <input
                          type="text"
                          className="figma-input"
                          value={primaryElement.fillGradient.end}
                          onChange={(e) =>
                            onUpdateElement(primaryElement.id, {
                              fillGradient: {
                                ...primaryElement.fillGradient!,
                                end: e.target.value,
                              },
                            })
                          }
                        />
                      </div>
                    </div>
                  ) : (
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                      <input
                        type="color"
                        value={primaryElement.fill.startsWith('#') ? primaryElement.fill : '#0D99FF'}
                        onChange={(e) =>
                          onUpdateElement(primaryElement.id, { fill: e.target.value })
                        }
                        style={{
                          width: '28px',
                          height: '26px',
                          border: 'none',
                          borderRadius: '4px',
                          cursor: 'pointer',
                          background: 'transparent',
                        }}
                      />
                      <input
                        type="text"
                        className="figma-input"
                        value={primaryElement.fill}
                        onChange={(e) =>
                          onUpdateElement(primaryElement.id, { fill: e.target.value })
                        }
                        style={{ flex: 1 }}
                      />
                      <div style={{ width: '50px' }}>
                        <input
                          type="number"
                          className="figma-input"
                          min="0"
                          max="100"
                          value={Math.round(primaryElement.opacity * 100)}
                          onChange={(e) =>
                            onUpdateElement(primaryElement.id, {
                              opacity: Math.max(0, Math.min(1, (parseFloat(e.target.value) || 100) / 100)),
                            })
                          }
                        />
                      </div>
                    </div>
                  )}
                </div>
              )}

              {/* Stroke Section */}
              {primaryElement && (
                <div style={{ borderBottom: '1px solid var(--border-color)', paddingBottom: '14px' }}>
                  <div
                    style={{
                      fontSize: '11px',
                      fontWeight: 600,
                      color: 'var(--text-secondary)',
                      marginBottom: '8px',
                    }}
                  >
                    STROKE
                  </div>

                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <input
                      type="color"
                      value={
                        primaryElement.stroke && primaryElement.stroke.startsWith('#')
                          ? primaryElement.stroke
                          : '#282C37'
                      }
                      onChange={(e) =>
                        onUpdateElement(primaryElement.id, { stroke: e.target.value })
                      }
                      style={{
                        width: '26px',
                        height: '24px',
                        border: 'none',
                        borderRadius: '4px',
                        cursor: 'pointer',
                        background: 'transparent',
                      }}
                    />
                    <input
                      type="text"
                      className="figma-input"
                      value={primaryElement.stroke}
                      onChange={(e) =>
                        onUpdateElement(primaryElement.id, { stroke: e.target.value })
                      }
                      style={{ flex: 1 }}
                    />
                    <div style={{ width: '45px' }}>
                      <input
                        type="number"
                        className="figma-input"
                        min="0"
                        value={primaryElement.strokeWidth}
                        onChange={(e) =>
                          onUpdateElement(primaryElement.id, {
                            strokeWidth: Math.max(0, parseFloat(e.target.value) || 0),
                          })
                        }
                        title="Stroke Width"
                      />
                    </div>
                  </div>
                </div>
              )}

              {/* Effects Section */}
              {primaryElement && (
                <div style={{ borderBottom: '1px solid var(--border-color)', paddingBottom: '14px' }}>
                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      marginBottom: '8px',
                    }}
                  >
                    <span style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)' }}>
                      EFFECTS
                    </span>
                    <button
                      className="figma-btn"
                      style={{ padding: '2px 6px', fontSize: '10px' }}
                      onClick={() => handleAddShadow(primaryElement)}
                      title="Add Drop Shadow"
                    >
                      <Plus size={11} /> Shadow
                    </button>
                  </div>

                  {primaryElement.effects && primaryElement.effects.length > 0 ? (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
                      {primaryElement.effects.map((eff) => (
                        <div
                          key={eff.id}
                          style={{
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'space-between',
                            background: 'var(--bg-input)',
                            padding: '4px 8px',
                            borderRadius: '5px',
                            fontSize: '11px',
                          }}
                        >
                          <span>{eff.type === 'drop-shadow' ? 'Drop Shadow' : 'Blur'}</span>
                          <div style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                            <button
                              className="figma-btn"
                              style={{ padding: '2px' }}
                              onClick={() => {
                                const updated = primaryElement.effects.map((e) =>
                                  e.id === eff.id ? { ...e, enabled: !e.enabled } : e
                                );
                                onUpdateElement(primaryElement.id, { effects: updated });
                              }}
                            >
                              {eff.enabled ? <Eye size={11} /> : <EyeOff size={11} color="var(--danger)" />}
                            </button>
                            <button
                              className="figma-btn"
                              style={{ padding: '2px', color: 'var(--danger)' }}
                              onClick={() => handleRemoveEffect(primaryElement, eff.id)}
                            >
                              <Trash2 size={11} />
                            </button>
                          </div>
                        </div>
                      ))}
                    </div>
                  ) : (
                    <div style={{ fontSize: '10px', color: 'var(--text-secondary)' }}>
                      No effects applied.
                    </div>
                  )}
                </div>
              )}
            </div>
          )}
        </div>
      )}

      {/* Tab: Prototype */}
      {activeTab === 'prototype' && (
        <div style={{ flex: 1, overflowY: 'auto', padding: '14px' }}>
          {primaryElement ? (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <Link size={16} color="var(--accent)" />
                <span style={{ fontSize: '12px', fontWeight: 600 }}>Interaction Node</span>
              </div>

              <div>
                <span style={{ fontSize: '10px', color: 'var(--text-secondary)' }}>TRIGGER</span>
                <div
                  style={{
                    padding: '8px',
                    background: 'var(--bg-input)',
                    borderRadius: '6px',
                    fontSize: '12px',
                    marginTop: '4px',
                  }}
                >
                  On Click / Tap
                </div>
              </div>

              <div>
                <span style={{ fontSize: '10px', color: 'var(--text-secondary)' }}>
                  NAVIGATE TO FRAME
                </span>
                <select
                  className="figma-input"
                  value={primaryElement.prototypeTargetId || ''}
                  onChange={(e) =>
                    onUpdateElement(primaryElement.id, {
                      prototypeTargetId: e.target.value || undefined,
                    })
                  }
                  style={{ marginTop: '4px' }}
                >
                  <option value="">None (No interaction)</option>
                  {allFrames.map((frame) => (
                    <option key={frame.id} value={frame.id}>
                      {frame.name}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <span style={{ fontSize: '10px', color: 'var(--text-secondary)' }}>
                  ANIMATION TRANSITION
                </span>
                <select
                  className="figma-input"
                  value={primaryElement.prototypeTransition || 'slide-left'}
                  onChange={(e) =>
                    onUpdateElement(primaryElement.id, {
                      prototypeTransition: e.target.value as any,
                    })
                  }
                  style={{ marginTop: '4px' }}
                >
                  <option value="slide-left">Slide Left</option>
                  <option value="dissolve">Dissolve</option>
                  <option value="instant">Instant</option>
                </select>
              </div>

              <div
                style={{
                  background: 'var(--accent-light)',
                  border: '1px solid rgba(13, 153, 255, 0.3)',
                  padding: '10px',
                  borderRadius: '6px',
                  fontSize: '11px',
                  lineHeight: '1.5',
                  color: 'var(--text-primary)',
                }}
              >
                💡 Click "Present" (▶) in the top bar to run this interactive flow prototype!
              </div>
            </div>
          ) : (
            <div style={{ textAlign: 'center', color: 'var(--text-secondary)', padding: '30px 10px' }}>
              Select an element to create prototype click links.
            </div>
          )}
        </div>
      )}

      {/* Tab: Inspect / Code */}
      {activeTab === 'inspect' && (
        <div style={{ flex: 1, overflowY: 'auto', padding: '14px', display: 'flex', flexDirection: 'column' }}>
          {primaryElement ? (
            <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
              {/* Mode Switcher */}
              <div
                style={{
                  display: 'flex',
                  background: 'var(--bg-input)',
                  borderRadius: '6px',
                  border: '1px solid var(--border-color)',
                  marginBottom: '12px',
                  padding: '2px',
                }}
              >
                <button
                  className={`figma-btn ${inspectMode === 'css' ? 'active' : ''}`}
                  style={{ flex: 1, padding: '4px 0', fontSize: '11px' }}
                  onClick={() => setInspectMode('css')}
                >
                  CSS
                </button>
                <button
                  className={`figma-btn ${inspectMode === 'tailwind' ? 'active' : ''}`}
                  style={{ flex: 1, padding: '4px 0', fontSize: '11px' }}
                  onClick={() => setInspectMode('tailwind')}
                >
                  Tailwind
                </button>
                <button
                  className={`figma-btn ${inspectMode === 'svg' ? 'active' : ''}`}
                  style={{ flex: 1, padding: '4px 0', fontSize: '11px' }}
                  onClick={() => setInspectMode('svg')}
                >
                  SVG
                </button>
              </div>

              {/* Code Box */}
              <div style={{ position: 'relative', flex: 1 }}>
                <pre
                  style={{
                    backgroundColor: 'var(--bg-input)',
                    border: '1px solid var(--border-color)',
                    borderRadius: '8px',
                    padding: '12px',
                    fontSize: '11px',
                    fontFamily: 'var(--font-mono)',
                    color: '#60A5FA',
                    overflowX: 'auto',
                    whiteSpace: 'pre-wrap',
                    height: '100%',
                    maxHeight: '380px',
                    userSelect: 'text',
                  }}
                >
                  {inspectMode === 'css'
                    ? generateCSS(primaryElement)
                    : inspectMode === 'tailwind'
                    ? generateTailwind(primaryElement)
                    : generateSVGCode([primaryElement], primaryElement.width, primaryElement.height)}
                </pre>

                {/* Copy Button */}
                <button
                  className="figma-btn figma-btn-primary"
                  style={{
                    position: 'absolute',
                    top: '8px',
                    right: '8px',
                    padding: '4px 8px',
                    fontSize: '10px',
                  }}
                  onClick={() => {
                    const text =
                      inspectMode === 'css'
                        ? generateCSS(primaryElement)
                        : inspectMode === 'tailwind'
                        ? generateTailwind(primaryElement)
                        : generateSVGCode([primaryElement]);
                    handleCopyCode(text);
                  }}
                >
                  {copiedCode ? <Check size={12} /> : <Copy size={12} />}
                  <span>{copiedCode ? 'Copied!' : 'Copy Code'}</span>
                </button>
              </div>
            </div>
          ) : (
            <div style={{ textAlign: 'center', color: 'var(--text-secondary)', padding: '30px 10px' }}>
              Select an element to inspect CSS, Tailwind, or SVG markup.
            </div>
          )}
        </div>
      )}
    </aside>
  );
};
