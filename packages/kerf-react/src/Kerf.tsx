import React, { useEffect, useRef, forwardRef, useImperativeHandle } from 'react';
import { loadKfxFromUrl, KerfRuntimePlayer, KfxPackage } from './runtime';

export interface KerfProps {
  /** Path or URL to the .kfx package (e.g. "/hero.kfx" or "https://cdn.example.com/anim.kfx") */
  src: string;
  /** Whether animation starts playing automatically. Defaults to true. */
  autoplay?: boolean;
  /** Whether animation loops. Defaults to true. */
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
  inputs,
  style,
  className,
  onLoad,
  onStateChange
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
        if (onStateChange) player.onStateChange = onStateChange;

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
        const loop = (now: number) => {
          const dt = (now - lastTime) / 1000.0;
          lastTime = now;

          if (playerRef.current && canvasRef.current) {
            playerRef.current.advance(dt);
            playerRef.current.renderToCanvas(canvasRef.current);
          }

          animId = requestAnimationFrame(loop);
        };

        animId = requestAnimationFrame(loop);
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
  }, [src, autoplay, onStateChange, onLoad]);

  // Pointer interaction
  const handlePointerMove = (e: React.PointerEvent<HTMLCanvasElement>) => {
    const player = playerRef.current;
    const canvas = canvasRef.current;
    if (player && canvas) {
      const coords = player.getArtboardCoords(canvas, e.clientX, e.clientY);
      player.handlePointerMove(coords.x, coords.y);
    }
  };

  const handlePointerLeave = () => {
    playerRef.current?.handlePointerLeave();
  };

  return (
    <canvas
      ref={canvasRef}
      className={className}
      style={{
        width: '100%',
        height: '100%',
        display: 'block',
        ...style
      }}
      onPointerMove={handlePointerMove}
      onPointerLeave={handlePointerLeave}
    />
  );
});
Kerf.displayName = 'Kerf';

