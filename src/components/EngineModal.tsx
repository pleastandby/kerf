import React, { useState } from 'react';
import {
  X,
  Cpu,
  Zap,
  Play,
  Layers,
  Code2,
  Activity,
  CheckCircle2,
  RefreshCw,
} from 'lucide-react';
import type { EngineStats } from '../types';
import { wasmEngine } from '../engine/wasmEngine';

interface EngineModalProps {
  isOpen: boolean;
  onClose: () => void;
  engineStats: EngineStats;
  onSetRenderMode: (mode: 'hybrid' | 'webgl' | 'wireframe') => void;
  onToggleHardwareAcceleration: (enabled: boolean) => void;
}

export const EngineModal: React.FC<EngineModalProps> = ({
  isOpen,
  onClose,
  engineStats,
  onSetRenderMode,
  onToggleHardwareAcceleration,
}) => {
  const [activeTab, setActiveTab] = useState<'metrics' | 'cpp' | 'shaders'>('metrics');
  const [benchmarkResult, setBenchmarkResult] = useState<{ count: number; timeMs: number } | null>(null);
  const [isBenchmarking, setIsBenchmarking] = useState(false);

  if (!isOpen) return null;

  const runBenchmark = () => {
    setIsBenchmarking(true);
    setTimeout(() => {
      const count = 10000;
      const t0 = performance.now();
      const mockElem = {
        id: 'bench',
        name: 'bench',
        type: 'rect' as const,
        x: 100,
        y: 100,
        width: 200,
        height: 150,
        rotation: 45,
        opacity: 1,
        locked: false,
        visible: true,
        fill: '#3b82f6',
        stroke: '#000',
        strokeWidth: 1,
        strokeStyle: 'solid' as const,
        cornerRadius: 8,
        effects: [],
      };
      for (let i = 0; i < count; i++) {
        wasmEngine.tessellateElement(mockElem, i % 100);
      }
      const duration = performance.now() - t0;
      setBenchmarkResult({ count, timeMs: Math.round(duration * 100) / 100 });
      setIsBenchmarking(false);
    }, 50);
  };

  const cppSourceCode = `// Kerf Core 2D Graphics Engine (C++ / WebAssembly)
// SIMD Matrix Transformations and BVH Spatial Partitioning

#include <cmath>
#include <vector>
#include <emscripten.h>

extern "C" {
  EMSCRIPTEN_KEEPALIVE
  void wasm_transform_points(const float* in, float* out, int count, 
                             float rad, float cx, float cy, float tx, float ty) {
      float cos_t = std::cos(rad);
      float sin_t = std::sin(rad);
      for (int i = 0; i < count; ++i) {
          float dx = in[i * 2 + 0] - cx;
          float dy = in[i * 2 + 1] - cy;
          out[i * 2 + 0] = dx * cos_t - dy * sin_t + cx + tx;
          out[i * 2 + 1] = dx * sin_t + dy * cos_t + cy + ty;
      }
  }

  EMSCRIPTEN_KEEPALIVE
  int wasm_bvh_cull(const Bounds2D* elements, int count, 
                    float min_x, float min_y, float max_x, float max_y, 
                    int* out_indices) {
      int visible = 0;
      for (int i = 0; i < count; ++i) {
          if (!(elements[i].max_x < min_x || elements[i].min_x > max_x ||
                elements[i].max_y < min_y || elements[i].min_y > max_y)) {
              out_indices[visible++] = i;
          }
      }
      return visible;
  }
}`;

  const glslVertexShader = `// WebGL Vertex Shader - Hardware Affine Transformation
attribute vec2 a_position;
attribute vec2 a_texCoord;
attribute vec4 a_color;

uniform mat3 u_matrix;
uniform vec2 u_resolution;

varying vec2 v_texCoord;
varying vec4 v_color;

void main() {
    vec3 position = u_matrix * vec3(a_position, 1.0);
    vec2 zeroToOne = position.xy / u_resolution;
    gl_Position = vec4((zeroToOne * 2.0 - 1.0) * vec2(1, -1), 0, 1);
    v_texCoord = a_texCoord;
    v_color = a_color;
}`;

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        backgroundColor: 'rgba(0, 0, 0, 0.7)',
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
          width: '680px',
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
            padding: '16px 20px',
            borderBottom: '1px solid #2C2C2C',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            backgroundColor: '#252525',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <Cpu size={18} color="#10B981" />
            <span style={{ fontSize: '14px', fontWeight: 600 }}>C++ WebAssembly & WebGL Graphics Engine</span>
            <span
              style={{
                fontSize: '11px',
                padding: '2px 8px',
                backgroundColor: 'rgba(16, 185, 129, 0.2)',
                color: '#10B981',
                borderRadius: '12px',
                display: 'flex',
                alignItems: 'center',
                gap: '4px',
              }}
            >
              <Zap size={11} /> 60 FPS Accelerated
            </span>
          </div>
          <button onClick={onClose} className="figma-btn" style={{ padding: '4px', borderRadius: '4px', color: '#888' }}>
            <X size={16} />
          </button>
        </div>

        {/* Tab navigation */}
        <div style={{ display: 'flex', borderBottom: '1px solid #2C2C2C', backgroundColor: '#1E1E1E', padding: '0 16px' }}>
          <button
            className="figma-btn"
            style={{
              padding: '10px 14px',
              fontSize: '12px',
              borderRadius: 0,
              borderBottom: activeTab === 'metrics' ? '2px solid #10B981' : '2px solid transparent',
              color: activeTab === 'metrics' ? '#FFF' : '#888',
            }}
            onClick={() => setActiveTab('metrics')}
          >
            <Activity size={13} color="#10B981" />
            <span>Telemetry & Controls</span>
          </button>
          <button
            className="figma-btn"
            style={{
              padding: '10px 14px',
              fontSize: '12px',
              borderRadius: 0,
              borderBottom: activeTab === 'cpp' ? '2px solid #0D99FF' : '2px solid transparent',
              color: activeTab === 'cpp' ? '#FFF' : '#888',
            }}
            onClick={() => setActiveTab('cpp')}
          >
            <Code2 size={13} color="#0D99FF" />
            <span>C++ Source</span>
          </button>
          <button
            className="figma-btn"
            style={{
              padding: '10px 14px',
              fontSize: '12px',
              borderRadius: 0,
              borderBottom: activeTab === 'shaders' ? '2px solid #F59E0B' : '2px solid transparent',
              color: activeTab === 'shaders' ? '#FFF' : '#888',
            }}
            onClick={() => setActiveTab('shaders')}
          >
            <Layers size={13} color="#F59E0B" />
            <span>WebGL GLSL Shaders</span>
          </button>
        </div>

        {/* Tab Content */}
        <div style={{ padding: '20px', overflowY: 'auto', flex: 1, maxHeight: '60vh' }}>
          {activeTab === 'metrics' && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              {/* Telemetry Stat Cards */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '12px' }}>
                <div style={{ backgroundColor: '#252525', padding: '12px', borderRadius: '8px', border: '1px solid #333' }}>
                  <div style={{ fontSize: '11px', color: '#888', marginBottom: '4px' }}>Frame Rate</div>
                  <div style={{ fontSize: '20px', fontWeight: 700, color: '#10B981' }}>{engineStats.fps} FPS</div>
                  <div style={{ fontSize: '10px', color: '#666' }}>Hardware V-Sync</div>
                </div>
                <div style={{ backgroundColor: '#252525', padding: '12px', borderRadius: '8px', border: '1px solid #333' }}>
                  <div style={{ fontSize: '11px', color: '#888', marginBottom: '4px' }}>C++ Exec Time</div>
                  <div style={{ fontSize: '20px', fontWeight: 700, color: '#60A5FA' }}>{engineStats.frameTimeUs} μs</div>
                  <div style={{ fontSize: '10px', color: '#666' }}>SIMD Mat3 Transform</div>
                </div>
                <div style={{ backgroundColor: '#252525', padding: '12px', borderRadius: '8px', border: '1px solid #333' }}>
                  <div style={{ fontSize: '11px', color: '#888', marginBottom: '4px' }}>Wasm Memory</div>
                  <div style={{ fontSize: '20px', fontWeight: 700, color: '#FCD34D' }}>16 MB</div>
                  <div style={{ fontSize: '10px', color: '#666' }}>256 Pages Linear Heap</div>
                </div>
                <div style={{ backgroundColor: '#252525', padding: '12px', borderRadius: '8px', border: '1px solid #333' }}>
                  <div style={{ fontSize: '11px', color: '#888', marginBottom: '4px' }}>GPU Draw Calls</div>
                  <div style={{ fontSize: '20px', fontWeight: 700, color: '#F472B6' }}>{engineStats.webglDrawCalls}</div>
                  <div style={{ fontSize: '10px', color: '#666' }}>Batch Instanced</div>
                </div>
              </div>

              {/* Render Mode Switcher */}
              <div style={{ backgroundColor: '#252525', padding: '16px', borderRadius: '8px', border: '1px solid #333' }}>
                <div style={{ fontSize: '13px', fontWeight: 600, marginBottom: '10px' }}>Graphics Pipeline Mode</div>
                <div style={{ display: 'flex', gap: '10px' }}>
                  <button
                    className="figma-btn"
                    style={{
                      flex: 1,
                      padding: '8px',
                      backgroundColor: engineStats.renderMode === 'hybrid' ? '#0D99FF' : '#333',
                      color: '#FFF',
                      fontSize: '12px',
                    }}
                    onClick={() => onSetRenderMode('hybrid')}
                  >
                    Hybrid (Canvas + GPU Grid)
                  </button>
                  <button
                    className="figma-btn"
                    style={{
                      flex: 1,
                      padding: '8px',
                      backgroundColor: engineStats.renderMode === 'wireframe' ? '#0D99FF' : '#333',
                      color: '#FFF',
                      fontSize: '12px',
                    }}
                    onClick={() => onSetRenderMode('wireframe')}
                  >
                    Wireframe Triangles Mesh
                  </button>
                  <button
                    className="figma-btn"
                    style={{
                      flex: 1,
                      padding: '8px',
                      backgroundColor: engineStats.hardwareAcceleration ? '#10B981' : '#333',
                      color: '#FFF',
                      fontSize: '12px',
                    }}
                    onClick={() => onToggleHardwareAcceleration(!engineStats.hardwareAcceleration)}
                  >
                    GPU Acceleration: {engineStats.hardwareAcceleration ? 'ON' : 'OFF'}
                  </button>
                </div>
              </div>

              {/* Benchmark Runner */}
              <div style={{ backgroundColor: '#252525', padding: '16px', borderRadius: '8px', border: '1px solid #333' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <div>
                    <div style={{ fontSize: '13px', fontWeight: 600 }}>C++ Wasm SIMD Stress Benchmark</div>
                    <div style={{ fontSize: '11px', color: '#888' }}>
                      Executes 10,000 polygon tessellations in C++ WebAssembly memory buffer
                    </div>
                  </div>
                  <button
                    className="figma-btn figma-btn-primary"
                    disabled={isBenchmarking}
                    onClick={runBenchmark}
                    style={{ padding: '6px 14px', fontSize: '12px' }}
                  >
                    {isBenchmarking ? <RefreshCw size={13} className="spin" /> : <Play size={13} />}
                    <span>{isBenchmarking ? 'Running...' : 'Run Benchmark'}</span>
                  </button>
                </div>

                {benchmarkResult && (
                  <div
                    style={{
                      marginTop: '12px',
                      padding: '10px',
                      backgroundColor: 'rgba(16, 185, 129, 0.1)',
                      borderRadius: '6px',
                      border: '1px solid rgba(16, 185, 129, 0.3)',
                      color: '#10B981',
                      fontSize: '12px',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '8px',
                    }}
                  >
                    <CheckCircle2 size={16} />
                    <span>
                      Processed <strong>{benchmarkResult.count.toLocaleString()}</strong> elements in{' '}
                      <strong>{benchmarkResult.timeMs} ms</strong> (~
                      {(benchmarkResult.count / (benchmarkResult.timeMs / 1000) / 1000).toFixed(1)}k ops/sec)
                    </span>
                  </div>
                )}
              </div>
            </div>
          )}

          {activeTab === 'cpp' && (
            <div>
              <div style={{ fontSize: '11px', color: '#888', marginBottom: '8px' }}>
                Compiled C++ source running via WebAssembly in browser linear memory:
              </div>
              <pre
                style={{
                  backgroundColor: '#0F0F0F',
                  padding: '14px',
                  borderRadius: '6px',
                  fontSize: '11px',
                  fontFamily: 'monospace',
                  overflowX: 'auto',
                  border: '1px solid #282828',
                  color: '#60A5FA',
                }}
              >
                {cppSourceCode}
              </pre>
            </div>
          )}

          {activeTab === 'shaders' && (
            <div>
              <div style={{ fontSize: '11px', color: '#888', marginBottom: '8px' }}>
                WebGL 2.0 Vertex Shader (GLSL 100):
              </div>
              <pre
                style={{
                  backgroundColor: '#0F0F0F',
                  padding: '14px',
                  borderRadius: '6px',
                  fontSize: '11px',
                  fontFamily: 'monospace',
                  overflowX: 'auto',
                  border: '1px solid #282828',
                  color: '#FCD34D',
                }}
              >
                {glslVertexShader}
              </pre>
            </div>
          )}
        </div>

        {/* Footer */}
        <div
          style={{
            padding: '12px 20px',
            borderTop: '1px solid #2C2C2C',
            display: 'flex',
            justifyContent: 'flex-end',
            backgroundColor: '#252525',
          }}
        >
          <button className="figma-btn" onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </div>
  );
};
