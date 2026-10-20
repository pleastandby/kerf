import React, { useState, useEffect } from 'react';
import { X, RotateCcw } from 'lucide-react';
import type { FigmaElement } from '../types';

interface PrototypeModalProps {
  isOpen: boolean;
  onClose: () => void;
  elements: FigmaElement[];
  initialFrameId?: string;
}

export const PrototypeModal: React.FC<PrototypeModalProps> = ({
  isOpen,
  onClose,
  elements,
  initialFrameId,
}) => {
  const frames = elements.filter((el) => el.type === 'frame');
  const [currentFrameId, setCurrentFrameId] = useState<string>('');
  const [transitioning, setTransitioning] = useState(false);
  const [hotspotHint, setHotspotHint] = useState<{ x: number; y: number } | null>(null);

  useEffect(() => {
    if (isOpen) {
      if (initialFrameId && frames.some((f) => f.id === initialFrameId)) {
        setCurrentFrameId(initialFrameId);
      } else if (frames.length > 0) {
        setCurrentFrameId(frames[0].id);
      }
    }
  }, [isOpen, initialFrameId, frames]);

  if (!isOpen) return null;

  const currentFrame = frames.find((f) => f.id === currentFrameId) || frames[0];
  if (!currentFrame) {
    return (
      <div
        style={{
          position: 'fixed',
          inset: 0,
          background: 'rgba(0,0,0,0.85)',
          zIndex: 9999,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        <div style={{ background: '#252526', padding: '24px', borderRadius: '12px', textAlign: 'center' }}>
          <p style={{ color: '#fff', marginBottom: '16px' }}>No Frames found in the current page to present.</p>
          <button className="figma-btn figma-btn-primary" onClick={onClose}>
            Back to Canvas
          </button>
        </div>
      </div>
    );
  }

  // Find all elements belonging to current frame
  const frameChildren = elements.filter(
    (el) =>
      el.parentId === currentFrame.id ||
      (el.id !== currentFrame.id &&
        el.x >= currentFrame.x &&
        el.x + el.width <= currentFrame.x + currentFrame.width + 10 &&
        el.y >= currentFrame.y &&
        el.y + el.height <= currentFrame.y + currentFrame.height + 10)
  );

  const handleElementClick = (elem: FigmaElement, e: React.MouseEvent) => {
    e.stopPropagation();
    if (elem.prototypeTargetId) {
      setTransitioning(true);
      setTimeout(() => {
        setCurrentFrameId(elem.prototypeTargetId!);
        setTransitioning(false);
      }, 150);
    }
  };

  const handleCanvasClick = (e: React.MouseEvent<HTMLDivElement>) => {
    // Show blue hotspot hints on interactive elements
    const rect = e.currentTarget.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const y = e.clientY - rect.top;
    setHotspotHint({ x, y });
    setTimeout(() => setHotspotHint(null), 800);
  };

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        backgroundColor: '#0a0a0c',
        zIndex: 9999,
        display: 'flex',
        flexDirection: 'column',
      }}
    >
      {/* Top Prototype Navigation Bar */}
      <div
        style={{
          height: '52px',
          backgroundColor: '#16181e',
          borderBottom: '1px solid #282c37',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '0 20px',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <span style={{ fontWeight: 600, color: '#FFFFFF', fontSize: '14px' }}>
            {currentFrame.name}
          </span>
          <span className="figma-badge">Interactive Prototype</span>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <button
            className="figma-btn"
            style={{ padding: '6px 12px', background: '#252936' }}
            onClick={() => setCurrentFrameId(frames[0].id)}
            title="Restart Prototype"
          >
            <RotateCcw size={14} />
            <span>Restart (R)</span>
          </button>

          <button
            className="figma-btn"
            style={{ padding: '6px 10px', background: '#252936' }}
            onClick={onClose}
            title="Close Preview"
          >
            <X size={16} />
          </button>
        </div>
      </div>

      {/* Main Prototype Viewport */}
      <div
        style={{
          flex: 1,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          overflow: 'auto',
          padding: '20px',
        }}
      >
        {/* Device Frame */}
        <div
          onClick={handleCanvasClick}
          style={{
            width: `${currentFrame.width}px`,
            height: `${currentFrame.height}px`,
            backgroundColor: currentFrame.fill || '#141720',
            borderRadius: `${currentFrame.cornerRadius || 0}px`,
            position: 'relative',
            overflow: 'hidden',
            boxShadow: '0 25px 60px rgba(0,0,0,0.8), 0 0 0 1px #282c37',
            transition: 'opacity 0.15s ease',
            opacity: transitioning ? 0.3 : 1,
            cursor: 'default',
          }}
        >
          {/* Render Elements Relative to Frame Coordinates */}
          {frameChildren.map((elem) => {
            if (!elem.visible) return null;
            const relX = elem.x - currentFrame.x;
            const relY = elem.y - currentFrame.y;

            const isInteractive = Boolean(elem.prototypeTargetId);

            return (
              <div
                key={elem.id}
                onClick={(e) => handleElementClick(elem, e)}
                style={{
                  position: 'absolute',
                  left: `${relX}px`,
                  top: `${relY}px`,
                  width: `${elem.width}px`,
                  height: `${elem.height}px`,
                  transform: elem.rotation ? `rotate(${elem.rotation}deg)` : undefined,
                  opacity: elem.opacity,
                  backgroundColor:
                    elem.fillType === 'linear-gradient' && elem.fillGradient
                      ? undefined
                      : elem.fill,
                  backgroundImage:
                    elem.fillType === 'linear-gradient' && elem.fillGradient
                      ? `linear-gradient(${elem.fillGradient.angle}deg, ${elem.fillGradient.start}, ${elem.fillGradient.end})`
                      : undefined,
                  border:
                    elem.strokeWidth > 0
                      ? `${elem.strokeWidth}px ${elem.strokeStyle} ${elem.stroke}`
                      : undefined,
                  borderRadius: `${elem.cornerRadius || 0}px`,
                  display: 'flex',
                  alignItems: elem.type === 'text' ? 'flex-start' : 'center',
                  justifyContent:
                    elem.textAlign === 'center'
                      ? 'center'
                      : elem.textAlign === 'right'
                      ? 'flex-end'
                      : 'flex-start',
                  color: elem.fill,
                  fontSize: `${elem.fontSize || 16}px`,
                  fontFamily: elem.fontFamily || 'Inter',
                  fontWeight: elem.fontWeight || 400,
                  cursor: isInteractive ? 'pointer' : 'default',
                  transition: 'transform 0.1s ease',
                  overflow: 'hidden',
                }}
              >
                {elem.type === 'text' && elem.text}

                {/* Hotspot Pulse when non-interactive area clicked */}
                {hotspotHint && isInteractive && (
                  <div
                    style={{
                      position: 'absolute',
                      inset: 0,
                      backgroundColor: 'rgba(13, 153, 255, 0.3)',
                      border: '2px solid #0D99FF',
                      borderRadius: `${elem.cornerRadius || 0}px`,
                      pointerEvents: 'none',
                    }}
                  />
                )}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};
