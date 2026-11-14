import React, { useEffect, useRef, forwardRef, useImperativeHandle } from 'react';
import { loadKfxFromUrl, KerfRuntimePlayer, KfxPackage } from './runtime';

export interface KerfProps {
  /** Path or URL to the .kfx package (e.g. "/hero.kfx" or "https://cdn.example.com/anim.kfx") */
  src: string;
  /** Whether animation starts playing automatically. Defaults to true. */
  autoplay?: boolean;
  /** Override the package's authored loop mode. Defaults to the package setting. */
  loop?: boolean;
  /** Initial state machine inputs to bind, e.g. { isHovering: true, score: 42 } */
  inputs?: Record<string, boolean | number>;
  /** Custom canvas CSS styles */
  style?: React.CSSProperties;
  /** Custom CSS class name */
  className?: string;
  /** Callback invoked when the package is successfully loaded and parsed */
  onLoad?: (pkg: KfxPackage) => void;
  /** Callback invoked when state machine transitions */
  onStateChange?: (stateName: string) => void;
  /** Callback invoked when a listener reports a named event */
  onEvent?: (eventName: string) => void;
  /** Callback invoked when a listener changes a text view-model property */
  onTextInputChange?: (name: string, value: string) => void;
}

export interface KerfRef {
  /** Set state machine Boolean input */
  setBool: (name: string, value: boolean) => void;
  /** Set state machine Number input */
  setNumber: (name: string, value: number) => void;
  /** Fire state machine Trigger input */
  fireTrigger: (name: string) => void;
  /** Resume animation playback */
  play: () => void;
  /** Pause animation playback */
  pause: () => void;
  /** Reset playback time to 0 */
  reset: () => void;
  /** Access the underlying KerfRuntimePlayer instance */
  getPlayer: () => KerfRuntimePlayer | null;
}

/**
 * Official plug-and-play React component for Kerf (.kfx) interactive motion graphics.
 *
 * @example
 * ```tsx
 * import { Kerf } from 'kerf-react';
 *
 * export function Hero() {
 *   return <Kerf src="/animations/hero.kfx" autoplay loop />;
 * }
 * ```
 */
export const Kerf = forwardRef<KerfRef, KerfProps>(({
  src,
  autoplay = true,
  loop,
  inputs,
  style,
  className,
  onLoad,
  onStateChange,
  onEvent,
  onTextInputChange
}, ref) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const playerRef = useRef<KerfRuntimePlayer | null>(null);

  // Expose imperative API via ref
  useImperativeHandle(ref, () => ({
    setBool: (name: string, value: boolean) => playerRef.current?.setBool(name, value),
    setNumber: (name: string, value: number) => playerRef.current?.setNumber(name, value),
    fireTrigger: (name: string) => playerRef.current?.fireTrigger(name),
    play: () => {
      if (playerRef.current) playerRef.current.isPlaying = true;
    },
    pause: () => {
      if (playerRef.current) playerRef.current.isPlaying = false;
    },
    reset: () => {
      if (playerRef.current) playerRef.current.currentTime = 0;
    },
    getPlayer: () => playerRef.current
  }));

  // Sync inputs dynamically whenever props change
  useEffect(() => {
    if (!playerRef.current || !inputs) return;
    for (const [key, val] of Object.entries(inputs)) {
      if (typeof val === 'boolean') {
        playerRef.current.setBool(key, val);
      } else if (typeof val === 'number') {
        playerRef.current.setNumber(key, val);
      }
    }
  }, [inputs]);

  // Load and setup the .kfx package
  useEffect(() => {
    let cancelled = false;
    let animId: number;
    let lastTime = performance.now();

    async function init() {
      try {
        const pkg = await loadKfxFromUrl(src);
        if (cancelled) return;

        const player = new KerfRuntimePlayer(pkg);
        player.isPlaying = autoplay;
        player.loop = loop ?? null;
        if (onStateChange) player.onStateChange = onStateChange;
        if (onEvent) player.onEvent = onEvent;
        if (onTextInputChange) player.onTextInputChange = onTextInputChange;

        // Apply initial inputs if provided
        if (inputs) {
          for (const [key, val] of Object.entries(inputs)) {
            if (typeof val === 'boolean') player.setBool(key, val);
            else if (typeof val === 'number') player.setNumber(key, val);
          }
        }

        playerRef.current = player;
        onLoad?.(pkg);

        // Render loop
        const frameLoop = (now: number) => {
          const dt = (now - lastTime) / 1000.0;
          lastTime = now;

          if (playerRef.current && canvasRef.current) {
            playerRef.current.advance(dt);
            playerRef.current.renderToCanvas(canvasRef.current);
          }

          animId = requestAnimationFrame(frameLoop);
        };

        animId = requestAnimationFrame(frameLoop);
      } catch (err) {
        console.error(`[Kerf] Error loading "${src}":`, err);
      }
    }

    init();

    return () => {
      cancelled = true;
      cancelAnimationFrame(animId);
      playerRef.current = null;
    };
  }, [src, autoplay, loop, onStateChange, onLoad, onEvent, onTextInputChange]);

  // Pointer interaction
  const handlePointerMove = (e: React.PointerEvent<HTMLCanvasElement>) => {
    const player = playerRef.current;
    const canvas = canvasRef.current;
    if (player && canvas) {
      const coords = player.getArtboardCoords(canvas, e.clientX, e.clientY);
      player.handlePointerMove(coords.x, coords.y);
    }
  };

  const handlePointerDown = (e: React.PointerEvent<HTMLCanvasElement>) => {
    const player = playerRef.current;
    const canvas = canvasRef.current;
    if (player && canvas) {
      const coords = player.getArtboardCoords(canvas, e.clientX, e.clientY);
      player.handlePointerMove(coords.x, coords.y);
      player.handlePointerDown();
      e.currentTarget.setPointerCapture(e.pointerId);
    }
  };

  const handlePointerUp = (e: React.PointerEvent<HTMLCanvasElement>) => {
    const player = playerRef.current;
    const canvas = canvasRef.current;
    if (player && canvas) {
      const coords = player.getArtboardCoords(canvas, e.clientX, e.clientY);
      player.handlePointerMove(coords.x, coords.y);
      player.handlePointerUp();
      if (e.currentTarget.hasPointerCapture(e.pointerId)) e.currentTarget.releasePointerCapture(e.pointerId);
    }
  };

  const handlePointerCancel = (e: React.PointerEvent<HTMLCanvasElement>) => {
    playerRef.current?.handlePointerCancel();
    if (e.currentTarget.hasPointerCapture(e.pointerId)) e.currentTarget.releasePointerCapture(e.pointerId);
  };

  const handlePointerLeave = () => {
    playerRef.current?.handlePointerLeave();
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLCanvasElement>) => {
    playerRef.current?.handleKeyDown(e.key);
  };

  return (
    <canvas
      ref={canvasRef}
      tabIndex={0}
      className={className}
      style={{
        width: '100%',
        height: '100%',
        display: 'block',
        ...style
      }}
      onPointerMove={handlePointerMove}
      onPointerDown={handlePointerDown}
      onPointerUp={handlePointerUp}
      onPointerLeave={handlePointerLeave}
      onPointerCancel={handlePointerCancel}
      onKeyDown={handleKeyDown}
      onClick={() => playerRef.current?.handlePointerClick()}
    />
  );
});
Kerf.displayName = 'Kerf';

