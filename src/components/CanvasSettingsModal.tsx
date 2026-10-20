import React from 'react';
import {
  X,
  Palette,
  Grid,
  Maximize2,
  Check,
  Compass,
} from 'lucide-react';
import type { CanvasSettings } from '../types';

interface CanvasSettingsModalProps {
  isOpen: boolean;
  onClose: () => void;
  settings: CanvasSettings;
  onUpdateSettings: (newSettings: Partial<CanvasSettings>) => void;
  onClearGuides?: () => void;
}

const BG_PALETTES = [
  { name: 'Figma Dark', hex: '#1E1E1E', label: 'Dark Default' },
  { name: 'OLED Black', hex: '#0A0A0C', label: 'True Black' },
  { name: 'Midnight Navy', hex: '#111726', label: 'Deep Blue' },
  { name: 'Studio Slate', hex: '#262933', label: 'Cool Gray' },
  { name: 'Clean White', hex: '#F8F9FA', label: 'Light Mode' },
  { name: 'Warm Cream', hex: '#F5F2EB', label: 'Warm Paper' },
];

const ARTBOARD_PRESETS = [
  { id: 'infinite', name: 'Infinite Canvas', desc: 'No boundaries (Standard Figma)' },
  { id: 'desktop', name: 'Desktop Display', desc: '1440 × 900 px (MacBook Pro)' },
  { id: 'mobile', name: 'Mobile Phone', desc: '393 × 852 px (iPhone 16 Pro)' },
  { id: 'tablet', name: 'Tablet Display', desc: '820 × 1180 px (iPad Air)' },
] as const;

export const CanvasSettingsModal: React.FC<CanvasSettingsModalProps> = ({
  isOpen,
  onClose,
  settings,
  onUpdateSettings,
  onClearGuides,
}) => {
  if (!isOpen) return null;

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
          width: '540px',
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
            <Palette size={18} color="#0D99FF" />
            <span style={{ fontSize: '14px', fontWeight: 600 }}>Custom Canvas & Grid Settings</span>
          </div>
          <button onClick={onClose} className="figma-btn" style={{ padding: '4px', borderRadius: '4px', color: '#888' }}>
            <X size={16} />
          </button>
        </div>

        {/* Body */}
        <div style={{ padding: '20px', overflowY: 'auto', flex: 1, display: 'flex', flexDirection: 'column', gap: '20px' }}>
          {/* Canvas Background Color */}
          <div>
            <div style={{ fontSize: '12px', fontWeight: 600, color: '#AAA', marginBottom: '10px', display: 'flex', alignItems: 'center', gap: '6px' }}>
              <Palette size={14} color="#0D99FF" />
              <span>Canvas Background Theme</span>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '10px' }}>
              {BG_PALETTES.map((palette) => {
                const isSelected = settings.backgroundColor.toLowerCase() === palette.hex.toLowerCase();
                return (
                  <div
                    key={palette.hex}
                    onClick={() => onUpdateSettings({ backgroundColor: palette.hex })}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: '10px',
                      padding: '8px 12px',
                      backgroundColor: '#252525',
                      border: isSelected ? '1.5px solid #0D99FF' : '1px solid #333',
                      borderRadius: '8px',
                      cursor: 'pointer',
                      transition: 'border-color 0.15s ease',
                    }}
                  >
                    <div
                      style={{
                        width: '24px',
                        height: '24px',
                        borderRadius: '6px',
                        backgroundColor: palette.hex,
                        border: '1px solid rgba(255, 255, 255, 0.15)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                      }}
                    >
                      {isSelected && <Check size={14} color={palette.hex === '#F8F9FA' || palette.hex === '#F5F2EB' ? '#000' : '#FFF'} />}
                    </div>
                    <div>
                      <div style={{ fontSize: '12px', fontWeight: 500, color: '#FFF' }}>{palette.name}</div>
                      <div style={{ fontSize: '10px', color: '#888' }}>{palette.hex}</div>
                    </div>
                  </div>
                );
              })}
            </div>

            {/* Custom Hex input */}
            <div style={{ display: 'flex', gap: '8px', alignItems: 'center', marginTop: '10px' }}>
              <span style={{ fontSize: '11px', color: '#888' }}>Custom Color:</span>
              <input
                type="color"
                value={settings.backgroundColor.startsWith('#') ? settings.backgroundColor : '#1E1E1E'}
                onChange={(e) => onUpdateSettings({ backgroundColor: e.target.value })}
                style={{ width: '28px', height: '24px', border: 'none', borderRadius: '4px', cursor: 'pointer' }}
              />
              <input
                type="text"
                className="figma-input"
                value={settings.backgroundColor}
                onChange={(e) => onUpdateSettings({ backgroundColor: e.target.value })}
                style={{ width: '100px', fontSize: '11px' }}
              />
            </div>
          </div>

          {/* Grid Type & Density */}
          <div>
            <div style={{ fontSize: '12px', fontWeight: 600, color: '#AAA', marginBottom: '10px', display: 'flex', alignItems: 'center', gap: '6px' }}>
              <Grid size={14} color="#10B981" />
              <span>Grid Style & Density</span>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '8px' }}>
              {(['dots', 'lines', 'isometric', 'none'] as const).map((type) => {
                const isSelected = settings.gridType === type;
                return (
                  <button
                    key={type}
                    className="figma-btn"
                    style={{
                      padding: '8px',
                      backgroundColor: isSelected ? 'rgba(13, 153, 255, 0.15)' : '#252525',
                      border: isSelected ? '1.5px solid #0D99FF' : '1px solid #333',
                      color: isSelected ? '#FFF' : '#AAA',
                      borderRadius: '6px',
                      fontSize: '11px',
                      fontWeight: 600,
                      textTransform: 'capitalize',
                    }}
                    onClick={() => onUpdateSettings({ gridType: type })}
                  >
                    {type}
                  </button>
                );
              })}
            </div>

            {/* Grid Size Slider */}
            {settings.gridType !== 'none' && (
              <div style={{ marginTop: '12px', display: 'flex', alignItems: 'center', gap: '12px' }}>
                <span style={{ fontSize: '11px', color: '#888', minWidth: '90px' }}>
                  Grid Size ({settings.gridSize}px):
                </span>
                <input
                  type="range"
                  min="8"
                  max="48"
                  step="4"
                  value={settings.gridSize}
                  onChange={(e) => onUpdateSettings({ gridSize: Number(e.target.value) })}
                  style={{ flex: 1 }}
                />
                <span style={{ fontSize: '11px', color: '#0D99FF', fontWeight: 600, width: '35px' }}>
                  {settings.gridSize}px
                </span>
              </div>
            )}
          </div>

          {/* Rulers and Guides */}
          <div>
            <div style={{ fontSize: '12px', fontWeight: 600, color: '#AAA', marginBottom: '10px', display: 'flex', alignItems: 'center', gap: '6px' }}>
              <Compass size={14} color="#F59E0B" />
              <span>Pixel Rulers & Alignment Guides</span>
            </div>

            <div style={{ display: 'flex', gap: '12px' }}>
              <label
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: '8px',
                  backgroundColor: '#252525',
                  padding: '10px 14px',
                  borderRadius: '6px',
                  border: '1px solid #333',
                  cursor: 'pointer',
                  flex: 1,
                }}
              >
                <input
                  type="checkbox"
                  checked={settings.showRulers}
                  onChange={(e) => onUpdateSettings({ showRulers: e.target.checked })}
                  style={{ accentColor: '#0D99FF' }}
                />
                <div>
                  <div style={{ fontSize: '12px', color: '#FFF' }}>Show Pixel Rulers</div>
                  <div style={{ fontSize: '10px', color: '#888' }}>Toggle with Shift+R</div>
                </div>
              </label>

              <label
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: '8px',
                  backgroundColor: '#252525',
                  padding: '10px 14px',
                  borderRadius: '6px',
                  border: '1px solid #333',
                  cursor: 'pointer',
                  flex: 1,
                }}
              >
                <input
                  type="checkbox"
                  checked={settings.showGuides}
                  onChange={(e) => onUpdateSettings({ showGuides: e.target.checked })}
                  style={{ accentColor: '#0D99FF' }}
                />
                <div>
                  <div style={{ fontSize: '12px', color: '#FFF' }}>Show Guide Lines</div>
                  <div style={{ fontSize: '10px', color: '#888' }}>Drag from rulers</div>
                </div>
              </label>
            </div>

            {onClearGuides && (
              <button
                className="figma-btn"
                onClick={onClearGuides}
                style={{ marginTop: '10px', fontSize: '11px', color: '#EF4444' }}
              >
                Clear all active guides
              </button>
            )}
          </div>

          {/* Canvas Artboard Boundaries */}
          <div>
            <div style={{ fontSize: '12px', fontWeight: 600, color: '#AAA', marginBottom: '10px', display: 'flex', alignItems: 'center', gap: '6px' }}>
              <Maximize2 size={14} color="#A855F7" />
              <span>Canvas Boundary Mode</span>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
              {ARTBOARD_PRESETS.map((preset) => {
                const isSelected = settings.preset === preset.id;
                return (
                  <div
                    key={preset.id}
                    onClick={() => {
                      let w: number | undefined;
                      let h: number | undefined;
                      if (preset.id === 'desktop') {
                        w = 1440;
                        h = 900;
                      } else if (preset.id === 'mobile') {
                        w = 393;
                        h = 852;
                      } else if (preset.id === 'tablet') {
                        w = 820;
                        h = 1180;
                      }
                      onUpdateSettings({ preset: preset.id, fixedWidth: w, fixedHeight: h });
                    }}
                    style={{
                      padding: '10px 14px',
                      backgroundColor: isSelected ? 'rgba(168, 85, 247, 0.15)' : '#252525',
                      border: isSelected ? '1.5px solid #A855F7' : '1px solid #333',
                      borderRadius: '8px',
                      cursor: 'pointer',
                      transition: 'all 0.15s ease',
                    }}
                  >
                    <div style={{ fontSize: '12px', fontWeight: 600, color: isSelected ? '#A855F7' : '#FFF' }}>
                      {preset.name}
                    </div>
                    <div style={{ fontSize: '10px', color: '#888' }}>{preset.desc}</div>
                  </div>
                );
              })}
            </div>
          </div>
        </div>

        {/* Footer */}
        <div
          style={{
            padding: '14px 20px',
            borderTop: '1px solid #2C2C2C',
            display: 'flex',
            justifyContent: 'flex-end',
            backgroundColor: '#252525',
          }}
        >
          <button className="figma-btn figma-btn-primary" onClick={onClose} style={{ padding: '6px 16px' }}>
            Done
          </button>
        </div>
      </div>
    </div>
  );
};
