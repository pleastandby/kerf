import React, { useRef, useState, useEffect, useCallback } from 'react';
import { loadKfxFromUrl, KerfRuntimePlayer, KfxPackage } from './runtime';

export interface UseKerfOptions {
  src: string;
  autoplay?: boolean;
  loop?: boolean;
  stateMachine?: string;
  inputs?: Record<string, boolean | number>;
  onLoad?: (pkg: KfxPackage) => void;
  onStateChange?: (stateName: string) => void;
  onEvent?: (eventName: string) => void;
  onTextInputChange?: (name: string, value: string) => void;
}

export interface UseKerfResult {
  /** The Canvas React element to render */
  KerfComponent: (props?: { style?: React.CSSProperties; className?: string }) => React.ReactElement;
  /** Ref to the HTMLCanvasElement */
  canvasRef: React.RefObject<HTMLCanvasElement | null>;
  /** Underlying KerfRuntimePlayer */
  player: KerfRuntimePlayer | null;
  /** Whether the package has finished loading */
  isLoaded: boolean;
  /** Active state name in the state machine */
  activeState: string;
  /** Set state machine Boolean input */
  setBool: (name: string, value: boolean) => void;
  /** Set state machine Number input */
  setNumber: (name: string, value: number) => void;
  /** Fire state machine Trigger input */
  fireTrigger: (name: string) => void;
  /** Play animation */
  play: () => void;
  /** Pause animation */
  pause: () => void;
  /** Reset playback */
  reset: () => void;
}

/**
 * Hook-based Kerf integration (analogous to Rive's `useRive()`).
 *
 * @example
 * ```tsx
 * const { KerfComponent, setBool } = useKerf({
 *   src: '/animations/hero.kfx',
 *   autoplay: true
 * });
 *
 * return (
 *   <div onMouseEnter={() => setBool('isHovering', true)}>
 *     <KerfComponent style={{ width: '100%', height: 400 }} />
 *   </div>
 * );
 * ```
 */
export function useKerf(options: UseKerfOptions): UseKerfResult {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const playerRef = useRef<KerfRuntimePlayer | null>(null);

  const [isLoaded, setIsLoaded] = useState(false);
  const [activeState, setActiveState] = useState('');

  const setBool = useCallback((name: string, val: boolean) => {
    playerRef.current?.setBool(name, val);
  }, []);

  const setNumber = useCallback((name: string, val: number) => {
    playerRef.current?.setNumber(name, val);
  }, []);

  const fireTrigger = useCallback((name: string) => {
    playerRef.current?.fireTrigger(name);
  }, []);

  const play = useCallback(() => {
    if (playerRef.current) playerRef.current.isPlaying = true;
  }, []);

  const pause = useCallback(() => {
    if (playerRef.current) playerRef.current.isPlaying = false;
  }, []);

  const reset = useCallback(() => {
    if (playerRef.current) playerRef.current.currentTime = 0;
  }, []);

  useEffect(() => {
    let cancelled = false;
    let animId: number;
    let lastTime = performance.now();

    async function init() {
      try {
        const pkg = await loadKfxFromUrl(options.src);
        if (cancelled) return;

        const player = new KerfRuntimePlayer(pkg);
        player.isPlaying = options.autoplay ?? true;
        player.loop = options.loop ?? null;

        player.onStateChange = (state) => {
          setActiveState(state);
          options.onStateChange?.(state);
        };
        player.onEvent = (eventName) => options.onEvent?.(eventName);
        player.onTextInputChange = (name, value) => options.onTextInputChange?.(name, value);

        if (options.inputs) {
          for (const [key, val] of Object.entries(options.inputs)) {
            if (typeof val === 'boolean') player.setBool(key, val);
            else if (typeof val === 'number') player.setNumber(key, val);
          }
        }

        playerRef.current = player;
        setIsLoaded(true);
        if (player.currentState?.Name) {
          setActiveState(player.currentState.Name);
        }
        options.onLoad?.(pkg);

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
        console.error('[useKerf] Error loading:', err);
      }
    }

    init();

    return () => {
      cancelled = true;
      cancelAnimationFrame(animId);
      playerRef.current = null;
    };
  }, [options.src, options.autoplay, options.loop]);

  const KerfComponent = useCallback((props?: { style?: React.CSSProperties; className?: string }) => {
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

    const handlePointerDown = (e: React.PointerEvent<HTMLCanvasElement>) => {
      const player = playerRef.current;
      const canvas = canvasRef.current;
      if (!player || !canvas) return;
      const coords = player.getArtboardCoords(canvas, e.clientX, e.clientY);
      player.handlePointerMove(coords.x, coords.y);
      player.handlePointerDown();
        e.currentTarget.setPointerCapture(e.pointerId);
    };

    const handlePointerUp = (e: React.PointerEvent<HTMLCanvasElement>) => {
      const player = playerRef.current;
      const canvas = canvasRef.current;
      if (!player || !canvas) return;
      const coords = player.getArtboardCoords(canvas, e.clientX, e.clientY);
      player.handlePointerMove(coords.x, coords.y);
      player.handlePointerUp();
        if (e.currentTarget.hasPointerCapture(e.pointerId)) e.currentTarget.releasePointerCapture(e.pointerId);
    };

      const handlePointerCancel = (e: React.PointerEvent<HTMLCanvasElement>) => {
        playerRef.current?.handlePointerCancel();
        if (e.currentTarget.hasPointerCapture(e.pointerId)) e.currentTarget.releasePointerCapture(e.pointerId);
      };

    return (
      <canvas
        ref={canvasRef}
        className={props?.className}
        style={{
          width: '100%',
          height: '100%',
          display: 'block',
          ...props?.style
        }}
        onPointerMove={handlePointerMove}
        onPointerDown={handlePointerDown}
        onPointerUp={handlePointerUp}
        onPointerLeave={handlePointerLeave}
        onPointerCancel={handlePointerCancel}
        onClick={() => playerRef.current?.handlePointerClick()}
      />
    );
  }, []);

  return {
    KerfComponent,
    canvasRef,
    player: playerRef.current,
    isLoaded,
    activeState,
    setBool,
    setNumber,
    fireTrigger,
    play,
    pause,
    reset
  };
}
