import React from 'react';
import { X, Command } from 'lucide-react';

interface ShortcutsModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export const ShortcutsModal: React.FC<ShortcutsModalProps> = ({ isOpen, onClose }) => {
  if (!isOpen) return null;

  const shortcuts = [
    { key: 'V', desc: 'Move / Selection tool' },
    { key: 'H', desc: 'Hand / Pan tool' },
    { key: 'Space + Drag', desc: 'Quick pan canvas' },
    { key: 'F', desc: 'Frame tool' },
    { key: 'R', desc: 'Rectangle tool' },
    { key: 'O', desc: 'Ellipse / Circle tool' },
    { key: 'L', desc: 'Line tool' },
    { key: 'Shift + L', desc: 'Arrow tool' },
    { key: 'T', desc: 'Text tool' },
    { key: 'P', desc: 'Pen vector path tool' },
    { key: 'Shift + A', desc: 'Add Auto Layout frame (autoalign)' },
    { key: 'Alt + Shift + A', desc: 'Remove Auto Layout' },
    { key: 'Ctrl / Cmd + Z', desc: 'Undo' },
    { key: 'Ctrl / Cmd + Y', desc: 'Redo' },
    { key: 'Ctrl / Cmd + D', desc: 'Duplicate selected element' },
    { key: 'Delete / Backspace', desc: 'Delete selected element' },
    { key: 'Arrow Keys', desc: 'Nudge element 1px (Shift for 10px)' },
    { key: 'Ctrl + Scroll', desc: 'Smooth zoom to cursor' },
    { key: '?', desc: 'Toggle keyboard shortcuts' },
  ];

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        backgroundColor: 'rgba(0, 0, 0, 0.75)',
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
          width: '540px',
          maxHeight: '80vh',
          backgroundColor: 'var(--bg-panel)',
          border: '1px solid var(--border-color)',
          borderRadius: '16px',
          boxShadow: 'var(--shadow-lg)',
          overflow: 'hidden',
          display: 'flex',
          flexDirection: 'column',
        }}
        onClick={(e) => e.stopPropagation()}
      >
        <div
          style={{
            padding: '16px 20px',
            borderBottom: '1px solid var(--border-color)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <Command size={18} color="var(--accent)" />
            <h3 style={{ fontSize: '15px', fontWeight: 600, color: '#fff' }}>
              Keyboard Shortcuts
            </h3>
          </div>
          <button className="figma-btn" onClick={onClose} style={{ padding: '4px' }}>
            <X size={16} />
          </button>
        </div>

        <div style={{ flex: 1, overflowY: 'auto', padding: '16px 20px' }}>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: '8px' }}>
            {shortcuts.map((sc) => (
              <div
                key={sc.key}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  padding: '8px 12px',
                  background: 'var(--bg-panel-secondary)',
                  borderRadius: '6px',
                  border: '1px solid var(--border-subtle)',
                }}
              >
                <span style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>
                  {sc.desc}
                </span>
                <span
                  style={{
                    fontFamily: 'var(--font-mono)',
                    fontSize: '11px',
                    fontWeight: 600,
                    padding: '3px 8px',
                    borderRadius: '4px',
                    background: 'var(--bg-input)',
                    border: '1px solid var(--border-color)',
                    color: 'var(--accent)',
                  }}
                >
                  {sc.key}
                </span>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
};
