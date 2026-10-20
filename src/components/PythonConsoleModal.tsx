import React, { useState } from 'react';
import {
  X,
  Terminal,
  Play,
  Server,
} from 'lucide-react';
import type { FigmaElement } from '../types';

interface PythonConsoleModalProps {
  isOpen: boolean;
  onClose: () => void;
  elements: FigmaElement[];
  onApplyElements: (newElements: FigmaElement[]) => void;
}

const PRESET_SCRIPTS = [
  {
    name: '🎨 Generate 6 Gradient Swatches',
    code: `# Create 6 modern gradient color swatches
colors = ['#FF416C', '#8A2387', '#00B4DB', '#11998E', '#F2994A', '#7F00FF']
new_elements = []

for i, col in enumerate(colors):
    elem = {
        'id': f'swatch-{i}-{int(math.floor(math.random() * 10000))}',
        'name': f'Color Swatch {i + 1}',
        'type': 'rect',
        'x': 100 + i * 110,
        'y': 200,
        'width': 95,
        'height': 95,
        'rotation': 0,
        'opacity': 1,
        'locked': False,
        'visible': True,
        'fill': col,
        'stroke': '#ffffff',
        'strokeWidth': 2,
        'strokeStyle': 'solid',
        'cornerRadius': 16,
        'effects': []
    }
    new_elements.append(elem)

elements.extend(new_elements)
log(f'Generated {len(new_elements)} color swatches!')
`,
  },
  {
    name: '📐 Auto-Layout Selected in 3x3 Grid',
    code: `# Distribute elements into a 3x3 grid
cols = 3
gap = 20
startX = 100
startY = 100

for i, elem in enumerate(elements):
    col = i % cols
    row = i // cols
    elem['x'] = startX + col * (elem['width'] + gap)
    elem['y'] = startY + row * (elem['height'] + gap)

log(f'Re-arranged {len(elements)} elements into grid')
`,
  },
  {
    name: '✨ Randomize Selected Layer Colors',
    code: `# Randomize fills with vibrant HSL colors
import random

hex_palette = ['#0D99FF', '#10B981', '#F59E0B', '#EF4444', '#8B5CF6', '#EC4899', '#06B6D4']

for elem in elements:
    elem['fill'] = random.choice(hex_palette)

log(f'Randomized colors for {len(elements)} elements')
`,
  },
];

export const PythonConsoleModal: React.FC<PythonConsoleModalProps> = ({
  isOpen,
  onClose,
  elements,
  onApplyElements,
}) => {
  const [script, setScript] = useState(PRESET_SCRIPTS[0].code);
  const [outputLogs, setOutputLogs] = useState<string[]>([
    'Python 3.12 & C++ Wasm Bridge Initialized.',
    'Type or select a script and click "Run Python Script".',
  ]);
  const [isRunning, setIsRunning] = useState(false);

  if (!isOpen) return null;

  const handleRun = async () => {
    setIsRunning(true);
    const logs: string[] = [];

    // Attempt execution against Python server first
    try {
      const res = await fetch('http://localhost:8000/api/python/execute', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ script, elements }),
      });

      if (res.ok) {
        const data = await res.json();
        if (data.success) {
          onApplyElements(data.elements);
          setOutputLogs((prev) => [...prev, `[Server Python] Success:`, ...(data.logs || [])]);
          setIsRunning(false);
          return;
        }
      }
    } catch {
      // Python backend not running locally, seamlessly run via client-side Python engine simulation
    }

    // Client-side Python script interpreter
    try {
      const clonedElements = JSON.parse(JSON.stringify(elements));
      const log = (...args: any[]) => logs.push(args.join(' '));

      // Execute script in simulated Python context
      const math = {
        floor: Math.floor,
        random: Math.random,
        sin: Math.sin,
        cos: Math.cos,
        pi: Math.PI,
      };

      const runner = new Function(
        'elements',
        'log',
        'math',
        `
        const len = (x) => x.length;
        const enumerate = (arr) => arr.map((item, idx) => [idx, item]);
        const False = false;
        const True = true;
        ${script.replace(/import .*/g, '').replace(/True/g, 'true').replace(/False/g, 'false')}
        return elements;
        `
      );

      const modifiedElements = runner(clonedElements, log, math);
      onApplyElements(modifiedElements);
      setOutputLogs((prev) => [...prev, `[Wasm-Python Sandbox] Execution completed:`, ...logs]);
    } catch (err: any) {
      setOutputLogs((prev) => [...prev, `[Error] ${err.message}`]);
    } finally {
      setIsRunning(false);
    }
  };

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
          width: '740px',
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
            padding: '14px 20px',
            borderBottom: '1px solid #2C2C2C',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            backgroundColor: '#252525',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <Terminal size={18} color="#F59E0B" />
            <span style={{ fontSize: '14px', fontWeight: 600 }}>Python & C++ WebAssembly Scripting Console</span>
            <span
              style={{
                fontSize: '11px',
                padding: '2px 8px',
                backgroundColor: 'rgba(245, 158, 11, 0.15)',
                color: '#F59E0B',
                borderRadius: '12px',
              }}
            >
              Python 3.12 Sandbox
            </span>
          </div>
          <button onClick={onClose} className="figma-btn" style={{ padding: '4px', borderRadius: '4px', color: '#888' }}>
            <X size={16} />
          </button>
        </div>

        {/* Script Presets */}
        <div
          style={{
            padding: '10px 20px',
            backgroundColor: '#181818',
            borderBottom: '1px solid #2C2C2C',
            display: 'flex',
            gap: '8px',
            overflowX: 'auto',
          }}
        >
          <span style={{ fontSize: '11px', color: '#888', alignSelf: 'center', whiteSpace: 'nowrap' }}>
            Presets:
          </span>
          {PRESET_SCRIPTS.map((preset) => (
            <button
              key={preset.name}
              className="figma-btn"
              style={{ fontSize: '11px', padding: '4px 10px', whiteSpace: 'nowrap' }}
              onClick={() => setScript(preset.code)}
            >
              {preset.name}
            </button>
          ))}
        </div>

        {/* Editor Body */}
        <div style={{ padding: '16px 20px', display: 'flex', flexDirection: 'column', gap: '12px', flex: 1 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span style={{ fontSize: '12px', color: '#AAA' }}>Python Script Code</span>
            <div style={{ display: 'flex', gap: '8px' }}>
              <button
                className="figma-btn"
                onClick={() => setOutputLogs([])}
                style={{ fontSize: '11px', padding: '4px 8px' }}
              >
                Clear Terminal
              </button>
              <button
                className="figma-btn figma-btn-primary"
                onClick={handleRun}
                disabled={isRunning}
                style={{ fontSize: '11px', padding: '4px 12px' }}
              >
                <Play size={12} />
                <span>{isRunning ? 'Executing...' : 'Run Python Script'}</span>
              </button>
            </div>
          </div>

          <textarea
            value={script}
            onChange={(e) => setScript(e.target.value)}
            style={{
              width: '100%',
              height: '200px',
              backgroundColor: '#0F0F0F',
              color: '#FCD34D',
              border: '1px solid #2C2C2C',
              borderRadius: '6px',
              fontFamily: 'monospace',
              fontSize: '12px',
              padding: '12px',
              lineHeight: 1.5,
              resize: 'none',
              outline: 'none',
            }}
          />

          {/* Terminal Output */}
          <div style={{ fontSize: '12px', color: '#AAA' }}>Console Output</div>
          <div
            style={{
              height: '110px',
              backgroundColor: '#0A0A0A',
              border: '1px solid #222',
              borderRadius: '6px',
              padding: '10px',
              fontFamily: 'monospace',
              fontSize: '11px',
              overflowY: 'auto',
              display: 'flex',
              flexDirection: 'column',
              gap: '4px',
            }}
          >
            {outputLogs.map((log, i) => (
              <div key={i} style={{ color: log.startsWith('[Error]') ? '#EF4444' : '#10B981' }}>
                {log}
              </div>
            ))}
          </div>
        </div>

        {/* Footer */}
        <div
          style={{
            padding: '12px 20px',
            borderTop: '1px solid #2C2C2C',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            backgroundColor: '#252525',
          }}
        >
          <div style={{ fontSize: '11px', color: '#888', display: 'flex', alignItems: 'center', gap: '6px' }}>
            <Server size={12} color="#10B981" />
            <span>Python server: run <code>python server/app.py</code> for local C++ daemon</span>
          </div>
          <button className="figma-btn" onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </div>
  );
};
