# kerf-react

> Official plug-and-play React runtime for Kerf (`.kfx`) interactive vector animations and state machines.

[![npm version](https://img.shields.io/npm/v/kerf-react.svg?logo=npm&color=CB3837)](https://www.npmjs.com/package/kerf-react)
[![license](https://img.shields.io/github/license/pleastandby/kerf)](https://github.com/pleastandby/kerf/blob/master/LICENSE)
[![GitHub Repository](https://img.shields.io/badge/GitHub-pleastandby%2Fkerf%2Fpackages-blue?logo=github)](https://github.com/pleastandby/kerf/packages)

Embed interactive, resolution-independent vector animations exported from **Kerf Studio** in any React application with **zero configuration** — just like Rive and Lottie.

---

## 🚀 Installation

```bash
npm install kerf-react
# or using pnpm
pnpm add kerf-react
# or using yarn
yarn add kerf-react
```

---

## ⚡ Quick Start

### 1. Place your `.kfx` animation
Export from Kerf Studio (`Export > Kerf Animation (.kfx)`) and place it in your `public/` directory (e.g., `public/hero.kfx`).

### 2. Embed with `<Kerf />`
```tsx
import { Kerf } from 'kerf-react';

export function HeroBanner() {
  return (
    <div style={{ width: '100%', height: 400 }}>
      <Kerf src="/hero.kfx" autoplay loop />
    </div>
  );
}
```

---

## ✏️ Path Geometry Keyframes

In Kerf Studio's **Animate** mode, select a vector path and use **Keyframe Path** in the path inspector to record its geometry at the current playhead time. Record another key after editing the path to animate its shape. The React runtime and `.kfx` exports interpolate paths when both keys have the same Bézier node layout. Paths with different node layouts or SVG-only geometry hold the earlier shape until the next key.

---

## 🎮 Interactive State Machines

Kerf state machines support interactive transitions driven by pointer events and code inputs (Booleans, Numbers, and Triggers).

### Controlling Inputs via Props:
```tsx
import { useState } from 'react';
import { Kerf } from 'kerf-react';

export function InteractiveCard() {
  const [isHovered, setIsHovered] = useState(false);

  return (
    <div
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
      style={{ width: 500, height: 350 }}
    >
      <Kerf
        src="/card-animation.kfx"
        inputs={{ isHovering: isHovered }}
      />
    </div>
  );
}
```

### Controlling Inputs via Ref:
```tsx
import { useRef } from 'react';
import { Kerf, KerfRef } from 'kerf-react';

export function ActionButton() {
  const animRef = useRef<KerfRef>(null);

  const handleClick = () => {
    // Fire a trigger impulse
    animRef.current?.fireTrigger('ClickBurst');
    // Or set a boolean/number
    animRef.current?.setBool('isActive', true);
  };

  return (
    <div>
      <Kerf ref={animRef} src="/button.kfx" />
      <button onClick={handleClick}>Trigger Animation</button>
    </div>
  );
}
```

---

## 🪝 Hook API (`useKerf`)

For fine-grained control over the canvas, use the `useKerf()` hook (analogous to Rive's `useRive()`):

```tsx
import { useKerf } from 'kerf-react';

export function Gauge({ value }: { value: number }) {
  const { KerfComponent, setNumber, isLoaded, activeState } = useKerf({
    src: '/gauge.kfx',
    autoplay: true,
    inputs: { level: value }
  });

  return (
    <div>
      <KerfComponent style={{ width: 400, height: 400 }} />
      <span>Status: {isLoaded ? activeState : 'Loading...'}</span>
    </div>
  );
}
```

---

## 📖 Component Props

| Prop | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `src` | `string` | **(Required)** | Path or URL to the `.kfx` package |
| `autoplay` | `boolean` | `true` | Starts animation automatically on load |
| `loop` | `boolean` | package setting | Override the loop mode authored in the `.kfx` file |
| `inputs` | `Record<string, boolean \| number>` | `{}` | Map of initial state machine input values |
| `onLoad` | `(pkg: KfxPackage) => void` | `undefined` | Callback fired when `.kfx` is parsed and ready |
| `onStateChange` | `(stateName: string) => void` | `undefined` | Callback fired on state machine transitions |
| `style` | `React.CSSProperties` | `{}` | Inline CSS styles applied to the canvas |
| `className` | `string` | `undefined` | CSS class name applied to the canvas |

---

## 📄 License & Repository

- **Repository**: [https://github.com/pleastandby/kerf/packages](https://github.com/pleastandby/kerf/packages)
- **License**: MIT © [pleastandby](https://github.com/pleastandby/kerf)

