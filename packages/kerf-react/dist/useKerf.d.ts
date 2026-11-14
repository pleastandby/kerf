import React from 'react';
import { KerfRuntimePlayer, KfxPackage } from './runtime';
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
    KerfComponent: (props?: {
        style?: React.CSSProperties;
        className?: string;
    }) => React.ReactElement;
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
export declare function useKerf(options: UseKerfOptions): UseKerfResult;
