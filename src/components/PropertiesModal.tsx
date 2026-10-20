import React, { useState } from 'react';
import {
  X,
  Sliders,
  Cpu,
  Code,
  Layers,
  Check,
  Copy,
} from 'lucide-react';
import type { FigmaElement } from '../types';
import { wasmEngine } from '../engine/wasmEngine';
import { generateCSS } from '../utils/codeGenerator';

interface PropertiesModalProps {
  element: FigmaElement | null;
  isOpen: boolean;
  onClose: () => void;
  onUpdateElement: (id: string, updates: Partial<FigmaElement>) => void;
}

export const PropertiesModal: React.FC<PropertiesModalProps> = ({
  element,
  isOpen,
  onClose,
  onUpdateElement,
}) => {
  const [activeTab, setActiveTab] = useState<'properties' | 'wasm' | 'code'>('properties');
  const [copied, setCopied] = useState(false);

  if (!isOpen || !element) return null;

  const handleCopy = (text: string) => {
    navigator.clipboard.writeText(text);
    setCopied(true);
    setTimeout(() => setCopied(false), 1800);
  };

  const cssCode = generateCSS(element);
  const jsonCode = JSON.stringify(element, null, 2);
  const wasmInfo = wasmEngine.tessellateElement(element, 0);

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        backgroundColor: 'rgba(0, 0, 0, 0.65)',
        backdropFilter: 'blur(8px)',
        zIndex: 10000,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
      }}
      onClick={onClose}
    >
      <div
        style={{
          width: '560px',
          maxHeight: '85vh',
          backgroundColor: '#1E1E1E',
          border: '1px solid #333333',
          borderRadius: '12px',
          boxShadow: '0 24px 64px rgba(0, 0, 0, 0.8)',
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
          color: '#E0E0E0',
        }}
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div
          style={{
            padding: '14px 18px',
            borderBottom: '1px solid #2C2C2C',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            backgroundColor: '#252525',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <Sliders size={16} color="#0D99FF" />
            <span style={{ fontSize: '13px', fontWeight: 600 }}>Element Properties & Diagnostics</span>
            <span
              style={{
                fontSize: '11px',
                padding: '2px 6px',
                backgroundColor: 'rgba(13, 153, 255, 0.15)',
                color: '#0D99FF',
                borderRadius: '4px',
              }}
            >
              {element.type.toUpperCase()}
            </span>
          </div>
          <button
            onClick={onClose}
            className="figma-btn"
            style={{ padding: '4px', borderRadius: '4px', color: '#888' }}
          >
            <X size={16} />
          </button>
        </div>

        {/* Tab switcher */}
        <div
          style={{
            display: 'flex',
            borderBottom: '1px solid #2C2C2C',
            backgroundColor: '#1E1E1E',
            padding: '0 12px',
          }}
        >
          <button
            className="figma-btn"
            style={{
              padding: '10px 14px',
              fontSize: '12px',
              fontWeight: 500,
              borderRadius: 0,
              borderBottom: activeTab === 'properties' ? '2px solid #0D99FF' : '2px solid transparent',
              color: activeTab === 'properties' ? '#FFF' : '#888',
            }}
            onClick={() => setActiveTab('properties')}
          >
            <Layers size={13} />
            <span>Attributes</span>
          </button>
          <button
            className="figma-btn"
            style={{
              padding: '10px 14px',
              fontSize: '12px',
              fontWeight: 500,
              borderRadius: 0,
              borderBottom: activeTab === 'wasm' ? '2px solid #10B981' : '2px solid transparent',
              color: activeTab === 'wasm' ? '#FFF' : '#888',
            }}
            onClick={() => setActiveTab('wasm')}
          >
            <Cpu size={13} color="#10B981" />
            <span>WebGL & Wasm Memory</span>
          </button>
          <button
            className="figma-btn"
            style={{
              padding: '10px 14px',
              fontSize: '12px',
              fontWeight: 500,
              borderRadius: 0,
              borderBottom: activeTab === 'code' ? '2px solid #F59E0B' : '2px solid transparent',
              color: activeTab === 'code' ? '#FFF' : '#888',
            }}
            onClick={() => setActiveTab('code')}
          >
            <Code size={13} color="#F59E0B" />
            <span>JSON & CSS Code</span>
          </button>
        </div>

        {/* Tab Body */}
        <div style={{ padding: '18px', overflowY: 'auto', flex: 1, maxHeight: '60vh' }}>
          {activeTab === 'properties' && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              {/* Name & ID */}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '11px', color: '#888', display: 'block', marginBottom: '4px' }}>
                    Layer Name
                  </label>
                  <input
                    type="text"
                    className="figma-input"
                    value={element.name}
                    onChange={(e) => onUpdateElement(element.id, { name: e.target.value })}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '11px', color: '#888', display: 'block', marginBottom: '4px' }}>
                    Node ID
                  </label>
                  <input
                    type="text"
                    className="figma-input"
                    value={element.id}
                    disabled
                    style={{ opacity: 0.6 }}
                  />
                </div>
              </div>

              {/* Geometry Grid */}
              <div style={{ fontSize: '12px', fontWeight: 600, color: '#AAA', marginTop: '4px' }}>
                Geometry & Layout
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '10px' }}>
                <div>
                  <label style={{ fontSize: '11px', color: '#888', display: 'block', marginBottom: '4px' }}>X</label>
                  <input
                    type="number"
                    className="figma-input"
                    value={Math.round(element.x)}
                    onChange={(e) => onUpdateElement(element.id, { x: Number(e.target.value) })}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '11px', color: '#888', display: 'block', marginBottom: '4px' }}>Y</label>
                  <input
                    type="number"
                    className="figma-input"
                    value={Math.round(element.y)}
                    onChange={(e) => onUpdateElement(element.id, { y: Number(e.target.value) })}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '11px', color: '#888', display: 'block', marginBottom: '4px' }}>Width</label>
                  <input
                    type="number"
                    className="figma-input"
                    value={Math.round(element.width)}
                    onChange={(e) => onUpdateElement(element.id, { width: Math.max(1, Number(e.target.value)) })}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '11px', color: '#888', display: 'block', marginBottom: '4px' }}>Height</label>
                  <input
                    type="number"
                    className="figma-input"
                    value={Math.round(element.height)}
                    onChange={(e) => onUpdateElement(element.id, { height: Math.max(1, Number(e.target.value)) })}
                  />
                </div>
              </div>

              {/* Rotation & Corner Radius */}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '11px', color: '#888', display: 'block', marginBottom: '4px' }}>
                    Rotation (deg)
                  </label>
                  <input
                    type="number"
                    className="figma-input"
                    value={Math.round(element.rotation || 0)}
                    onChange={(e) => onUpdateElement(element.id, { rotation: Number(e.target.value) })}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '11px', color: '#888', display: 'block', marginBottom: '4px' }}>
                    Corner Radius (px)
                  </label>
                  <input
                    type="number"
                    className="figma-input"
                    value={element.cornerRadius || 0}
                    onChange={(e) => onUpdateElement(element.id, { cornerRadius: Math.max(0, Number(e.target.value)) })}
                  />
                </div>
              </div>

              {/* Fill & Opacity */}
              <div style={{ fontSize: '12px', fontWeight: 600, color: '#AAA', marginTop: '4px' }}>
                Fill & Appearance
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '11px', color: '#888', display: 'block', marginBottom: '4px' }}>
                    Fill Color
                  </label>
                  <div style={{ display: 'flex', gap: '6px', alignItems: 'center' }}>
                    <input
                      type="color"
                      value={element.fill.startsWith('#') ? element.fill : '#3b82f6'}
                      onChange={(e) => onUpdateElement(element.id, { fill: e.target.value })}
                      style={{
                        width: '32px',
                        height: '28px',
                        padding: 0,
                        border: 'none',
                        cursor: 'pointer',
                        borderRadius: '4px',
                      }}
                    />
                    <input
                      type="text"
                      className="figma-input"
                      value={element.fill}
                      onChange={(e) => onUpdateElement(element.id, { fill: e.target.value })}
                      style={{ flex: 1 }}
                    />
                  </div>
                </div>
                <div>
                  <label style={{ fontSize: '11px', color: '#888', display: 'block', marginBottom: '4px' }}>
                    Opacity ({Math.round((element.opacity ?? 1) * 100)}%)
                  </label>
                  <input
                    type="range"
                    min="0"
                    max="1"
                    step="0.05"
                    value={element.opacity ?? 1}
                    onChange={(e) => onUpdateElement(element.id, { opacity: parseFloat(e.target.value) })}
                    style={{ width: '100%', marginTop: '6px' }}
                  />
                </div>
              </div>

              {/* Stroke */}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '11px', color: '#888', display: 'block', marginBottom: '4px' }}>
                    Stroke Color
                  </label>
                  <div style={{ display: 'flex', gap: '6px', alignItems: 'center' }}>
                    <input
                      type="color"
                      value={element.stroke.startsWith('#') ? element.stroke : '#ffffff'}
                      onChange={(e) => onUpdateElement(element.id, { stroke: e.target.value })}
                      style={{
                        width: '32px',
                        height: '28px',
                        padding: 0,
                        border: 'none',
                        cursor: 'pointer',
                        borderRadius: '4px',
                      }}
                    />
                    <input
                      type="text"
                      className="figma-input"
                      value={element.stroke}
                      onChange={(e) => onUpdateElement(element.id, { stroke: e.target.value })}
                      style={{ flex: 1 }}
                    />
                  </div>
                </div>
                <div>
                  <label style={{ fontSize: '11px', color: '#888', display: 'block', marginBottom: '4px' }}>
                    Stroke Width (px)
                  </label>
                  <input
                    type="number"
                    className="figma-input"
                    value={element.strokeWidth || 0}
                    onChange={(e) => onUpdateElement(element.id, { strokeWidth: Math.max(0, Number(e.target.value)) })}
                  />
                </div>
              </div>
            </div>
          )}

          {activeTab === 'wasm' && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              <div
                style={{
                  backgroundColor: '#141414',
                  borderRadius: '8px',
                  padding: '14px',
                  border: '1px solid #282828',
                }}
              >
                <div style={{ fontSize: '12px', fontWeight: 600, color: '#10B981', marginBottom: '8px' }}>
                  ⚡ C++ WebAssembly Linear Heap Allocation
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px', fontSize: '12px' }}>
                  <div>
                    <span style={{ color: '#888' }}>Wasm Pointer: </span>
                    <span style={{ fontFamily: 'monospace', color: '#60A5FA' }}>
                      0x{wasmInfo.memoryOffset.toString(16).padStart(8, '0')}
                    </span>
                  </div>
                  <div>
                    <span style={{ color: '#888' }}>Vertex Buffer Size: </span>
                    <span style={{ fontFamily: 'monospace' }}>48 floats (192 bytes)</span>
                  </div>
                  <div>
                    <span style={{ color: '#888' }}>WebGL Primitives: </span>
                    <span>2 Triangles (6 vertices)</span>
                  </div>
                  <div>
                    <span style={{ color: '#888' }}>SIMD Mat3 Affine: </span>
                    <span style={{ color: '#34D399' }}>Active (12 uops)</span>
                  </div>
                </div>
              </div>

              {/* Raw Vertex Dump */}
              <div style={{ fontSize: '11px', color: '#888' }}>Raw WebGL Vertex Data (x, y, u, v, r, g, b, a):</div>
              <pre
                style={{
                  backgroundColor: '#0F0F0F',
                  padding: '10px',
                  borderRadius: '6px',
                  fontSize: '11px',
                  fontFamily: 'monospace',
                  overflowX: 'auto',
                  border: '1px solid #222',
                  color: '#93C5FD',
                }}
              >
                {Array.from(wasmInfo.vertices)
                  .map((val, idx) => (idx % 8 === 0 ? `\nVertex ${Math.floor(idx / 8)}: ` : '') + val.toFixed(2))
                  .join(' ')}
              </pre>
            </div>
          )}

          {activeTab === 'code' && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span style={{ fontSize: '11px', color: '#888' }}>Generated CSS</span>
                <button className="figma-btn" onClick={() => handleCopy(cssCode)} style={{ fontSize: '11px' }}>
                  {copied ? <Check size={12} color="#10B981" /> : <Copy size={12} />}
                  <span>Copy CSS</span>
                </button>
              </div>
              <pre
                style={{
                  backgroundColor: '#0F0F0F',
                  padding: '12px',
                  borderRadius: '6px',
                  fontSize: '11px',
                  fontFamily: 'monospace',
                  overflowX: 'auto',
                  border: '1px solid #222',
                  color: '#FCD34D',
                }}
              >
                {cssCode}
              </pre>

              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '6px' }}>
                <span style={{ fontSize: '11px', color: '#888' }}>Figma Schema JSON</span>
                <button className="figma-btn" onClick={() => handleCopy(jsonCode)} style={{ fontSize: '11px' }}>
                  {copied ? <Check size={12} color="#10B981" /> : <Copy size={12} />}
                  <span>Copy JSON</span>
                </button>
              </div>
              <pre
                style={{
                  backgroundColor: '#0F0F0F',
                  padding: '12px',
                  borderRadius: '6px',
                  fontSize: '11px',
                  fontFamily: 'monospace',
                  overflowX: 'auto',
                  border: '1px solid #222',
                  color: '#A7F3D0',
                  maxHeight: '180px',
                }}
              >
                {jsonCode}
              </pre>
            </div>
          )}
        </div>

        {/* Footer */}
        <div
          style={{
            padding: '12px 18px',
            borderTop: '1px solid #2C2C2C',
            display: 'flex',
            justifyContent: 'flex-end',
            gap: '8px',
            backgroundColor: '#252525',
          }}
        >
          <button className="figma-btn figma-btn-primary" onClick={onClose}>
            Done
          </button>
        </div>
      </div>
    </div>
  );
};
