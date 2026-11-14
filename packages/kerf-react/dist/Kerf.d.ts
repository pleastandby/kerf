import React from 'react';
import { KerfRuntimePlayer, KfxPackage } from './runtime';
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
export declare const Kerf: React.ForwardRefExoticComponent<KerfProps & React.RefAttributes<KerfRef>>;
