# Kerf Website Project Report

**Snapshot:** 2026-11-14  
**Purpose:** Define the public Kerf download and documentation website, its content, implementation approach, integration APIs, and the current `.kfx` package contract.

## 1. Project Summary

Build a public website that helps people find, download, learn, and integrate Kerf. The site should serve three related products clearly:

1. **Kerf Studio**, the native Windows design and animation application.
2. **`kerf-react`**, the published React runtime for playing `.kfx` packages in browser applications.
3. **`.kfx`**, the ZIP-based runtime package exported by Kerf Studio.

The website should distinguish released capabilities from future platform plans. Current repository state supports a Windows x64 desktop application and a browser-based React runtime. It does **not** contain supported iOS, Android, macOS, Linux, or React Native player SDKs.

There is no website source, deployment configuration, canonical download URL, release feed, or domain configuration in the current repository. Treat the site stack and release integration below as proposed work, not as existing infrastructure.

## 2. Current Product and Release Facts

| Product | Current repository state | Website treatment |
|---|---|---|
| Kerf Studio | `ProductInfo` reports **Kerf 3.1 Beta**. A native Windows WPF application uses a retained Direct3D renderer and a Rust geometry library. | Label the desktop download as Beta. Do not imply a stable release or multi-OS desktop support. |
| Windows installer | `native-windows/build.bat` builds `Kerf.exe`, `KerfSetup.exe`, and `kerf_core.dll`. The installer embeds the app and native core. A local `build-output/release/` currently contains build artifacts; it is not a public release feed. | Link to a published release asset, not to a workspace path. Display version, channel, file size, release date, checksum, and system requirements from release metadata. |
| React runtime | `packages/kerf-react/package.json` reports **`kerf-react` 1.1.0**, MIT licensed, with React and React DOM peer dependencies `>=16.8.0` and `fflate` as a dependency. | Provide npm/pnpm/yarn install commands and link to the npm package and source repository. |
| `.kfx` | The exporter writes a ZIP package with JSON entries and format version `1.0.0`; manifest runtime compatibility is `>=1.0.0`. | Document format version separately from Studio version and npm version. Do not imply those version numbers move together. |
| Other native runtimes | A Rust `cdylib`/`rlib` and C ABI are used internally by the Windows app. The code does not provide a complete public `.kfx` loader, renderer, header, or packaged SDK for other native platforms. | Show Windows as available. Mark other native SDKs as unavailable or roadmap only. Do not publish the old in-app sample declarations as a real SDK API. |

### Supported integration matrix

| Target | Availability | Notes |
|---|---|---|
| React in browser | Available | Use `Kerf` or `useKerf` from `kerf-react`. The player renders to an HTML canvas. |
| Vite, Create React App, Remix | React integration path | These are browser React apps; use the same package and public asset URL pattern. Verify each framework in examples before claiming official certification. |
| Next.js | React integration path, client side | Canvas/player setup uses browser APIs. Put the player in a client component; document client-only loading for SSR-sensitive setups. |
| Plain JavaScript | Runtime bundle is built | `dist/kerf-runtime.iife.js` is generated and embedded by the Windows build, but `package.json` does not expose a documented `./runtime` subpath. Treat direct script-tag consumption as experimental until a stable URL and API contract are published. |
| Windows desktop | Available | WPF/Direct3D 11 application with a Windows x64 installer. |
| iOS, Android, macOS, Linux, React Native | Not currently shipped | The `.kfx` data format is platform-neutral, but that does not mean a player SDK exists for these targets. |

## 3. Website Goals and Audiences

### Goals

- Make the Windows download and its Beta status obvious.
- Explain what Kerf can do today with concrete screenshots, short clips, and working examples.
- Provide accurate React install, component, hook, and runtime API documentation.
- Publish a versioned `.kfx` format guide based on the exporter and runtime parser.
- Explain native availability accurately and set expectations for other platforms.
- Let visitors find install help, release notes, troubleshooting, license information, and source links quickly.
- Keep documentation synchronized with package types and exporter code.

### Audiences

- **Designers and animators:** learn the editor workflow, animation features, and export formats.
- **React developers:** install the runtime, load `.kfx`, control state machine inputs, and handle events.
- **Native developers:** understand what can be used now and what is not yet a distributable SDK.
- **Maintainers:** publish a desktop release, package version, changelog, and matching documentation.

## 4. Recommended Information Architecture

Use a shared header with **Product**, **Download**, **Documentation**, **Examples**, and **GitHub** links. Keep the primary “Download for Windows” action visible on the homepage and download page.

### Public routes

```text
/
/download
/changelog
/docs/
/docs/getting-started
/docs/studio/installation
/docs/studio/interface-and-tools
/docs/studio/layers-and-layout
/docs/studio/animation-and-state-machines
/docs/studio/exporting
/docs/react/quick-start
/docs/react/nextjs
/docs/react/api
/docs/react/troubleshooting
/docs/kfx/overview
/docs/kfx/container-and-schema
/docs/kfx/compatibility
/docs/native-platforms
/docs/faq
/license
```

### Homepage content

1. **Hero:** concise description of vector design and interactive animation; primary Windows download CTA and secondary React runtime/docs CTA.
2. **Product status:** Kerf Studio 3.1 Beta, Windows x64; `kerf-react` package version; `.kfx` as the runtime package format.
3. **Feature overview:** vector tools and Bézier editing, layers and groups, auto layout, components, animation timeline/state machines, export formats.
4. **Interactive demo:** use an approved `.kfx` sample with the existing `kerf-react` player; show play/pause and one state-machine input.
5. **Developer entry points:** React quick start, API reference, `.kfx` format, and platform support matrix.
6. **Footer:** GitHub, npm, license, release notes, documentation, and support/issue link.

Avoid competitor claims and performance figures unless they have current evidence and an approved comparison. The current product is Beta; the website should say so plainly.

### Download page content

- Current channel and version, release date, Windows x64 architecture, installer name, file size, and SHA-256 checksum.
- One primary installer link and a separate source/build link for developers.
- System requirements: the repository README currently lists Windows 10/11 64-bit and .NET Framework 4.8. Confirm these against a clean installation before publishing the final requirements.
- Installation steps, SmartScreen/signing status if applicable, uninstall path, and troubleshooting.
- Release notes and known issues for the exact version.
- A link to npm for React developers so the Windows application download is not confused with the runtime package.

The build script produces `KerfSetup.exe` as the distributable installer. The site should use a public release asset URL, supplied by release configuration, rather than hardcoding the local `build-output/release/` path. No public download URL or checksum automation is present in this checkout.

## 5. Website Implementation Proposal

### Stack

Use **Astro with Starlight** for the static, documentation-led site and add Astro’s React integration for the live `.kfx` demo. Starlight is built for Markdown/MDX documentation and includes navigation and full-text search; Astro can hydrate React components as client-side islands. See the [Starlight documentation](https://starlight.astro.build/) and [Astro React integration guide](https://docs.astro.build/en/guides/integrations-guide/react/).

Recommended implementation shape:

- **Static-first pages:** homepage, download page, changelog, format guide, and most docs are generated HTML; no account system or application backend is required for the initial site.
- **React island:** hydrate the player demo only where needed, using the released `kerf-react` dependency and an approved sample package.
- **Content:** Markdown/MDX files for tutorials, platform matrix, release notes, and `.kfx` specification. Keep code examples adjacent to the docs they describe.
- **API reference:** generate or validate the React API pages from `packages/kerf-react/dist/*.d.ts` or source declarations. Do not manually copy method signatures into two places without a verification step.
- **Theme:** derive colors and logos from the existing `assets/` brand files. Meet keyboard navigation, color contrast, reduced-motion, responsive layout, and semantic heading requirements.
- **Search:** keep docs search enabled and index API names, file extensions, common errors, and keyboard shortcuts.
- **Deployment:** choose and configure the project’s static host and canonical domain before launch. The repository currently has no site deployment workflow.

### Release data

Add a small machine-readable release manifest consumed by the download page, for example:

```json
{
  "channel": "beta",
  "version": "3.1 Beta",
  "platform": "Windows x64",
  "assetName": "KerfSetup.exe",
  "downloadUrl": "<published release asset URL>",
  "sizeBytes": 0,
  "sha256": "<generated checksum>",
  "releasedAt": "<UTC timestamp>",
  "releaseNotesUrl": "<release notes URL>"
}
```

The placeholders must be replaced from the actual publishing process. Prefer generating the manifest and checksum in the release workflow so the web page cannot silently point at an old or missing installer. Keep separate version fields for the Studio release, npm package, and `.kfx` format.

### Interactive demo

- Use `testdata/Test.kfx` or `assets/KerfBanner.kfx` only after confirming the sample is approved for public display.
- Place the sample under the site’s public/static directory and load it using `<Kerf src="/demos/sample.kfx" />`.
- Set an explicit responsive container height; the component canvas fills its parent.
- Include a fallback message/link to download the sample and a static poster or message if JavaScript is disabled or package loading fails.
- Keep the sample small; the current React loader fetches the entire ZIP before rendering.

## 6. React Integration Documentation

The current published package is `kerf-react`, not `@kerf/runtime-react`. The repository’s in-app `RuntimeGuideDialog.cs` contains examples using the old package name and old component names (`KerfPlayer`, `useKerfPlayer`); do not copy those examples into the website. Use the package source and generated declarations as the API source of truth.

### Quick start

```bash
npm install kerf-react
# or: pnpm add kerf-react
# or: yarn add kerf-react
```

Place an exported `.kfx` file in a location served by the app, such as `public/animations/hero.kfx`:

```tsx
import { Kerf } from 'kerf-react';

export function Hero() {
  return (
    <div style={{ width: '100%', height: 420 }}>
      <Kerf src="/animations/hero.kfx" autoplay loop />
    </div>
  );
}
```

`src` may be a URL. Remote package hosting must allow browser fetches from the embedding site (CORS). `Kerf` renders an HTML canvas and uses the browser’s 2D canvas context and `requestAnimationFrame`.

### Component API (`KerfProps`)

| Prop | Type | Default | Meaning |
|---|---|---|---|
| `src` | `string` | Required | URL/path to the `.kfx` file. |
| `autoplay` | `boolean` | `true` | Start playback after load. |
| `loop` | `boolean` | Package setting | Override loop behavior as looping or once. Authored ping-pong behavior is represented in the package format; the prop is not a three-mode enum. |
| `inputs` | `Record<string, boolean \| number>` | `{}` | Initial/update values for Boolean and Number state-machine inputs. |
| `style` | `React.CSSProperties` | Canvas defaults | Canvas style; default width/height are `100%`. Parent must have a measurable height. |
| `className` | `string` | — | Canvas class. |
| `onLoad` | `(pkg: KfxPackage) => void` | — | Called after package loading and parsing. |
| `onStateChange` | `(stateName: string) => void` | — | Called on state changes. |
| `onEvent` | `(eventName: string) => void` | — | Called when a runtime listener emits a named event. |
| `onTextInputChange` | `(name: string, value: string) => void` | — | Called when a listener changes a text view-model property. |

There is currently no `onError` prop. Load failures are written to the browser console. Website troubleshooting should cover wrong paths, HTTP failures, CORS, malformed ZIP/JSON, and a zero-height canvas parent.

### Ref API (`KerfRef`)

```tsx
import { useCallback, useRef } from 'react';
import { Kerf, type KerfRef } from 'kerf-react';

export function InteractiveCard() {
  const player = useRef<KerfRef>(null);
  const handleState = useCallback((state: string) => {
    console.log('State:', state);
  }, []);

  return (
    <div
      onPointerEnter={() => player.current?.setBool('isHovering', true)}
      onPointerLeave={() => player.current?.setBool('isHovering', false)}
      style={{ width: 500, height: 350 }}
    >
      <Kerf
        ref={player}
        src="/animations/card.kfx"
        onStateChange={handleState}
      />
      <button onClick={() => player.current?.fireTrigger('Activate')}>
        Activate
      </button>
    </div>
  );
}
```

| Ref method | Signature | Effect |
|---|---|---|
| `setBool` | `(name, value) => void` | Set a Boolean state-machine input. |
| `setNumber` | `(name, value) => void` | Set a Number state-machine input. |
| `fireTrigger` | `(name) => void` | Fire a Trigger input. |
| `play` | `() => void` | Resume playback. |
| `pause` | `() => void` | Pause playback. |
| `reset` | `() => void` | Set playback time to zero. |
| `getPlayer` | `() => KerfRuntimePlayer \| null` | Access the underlying player instance. |

### Hook API (`useKerf`)

`useKerf(options)` returns `KerfComponent`, `canvasRef`, `player`, `isLoaded`, `activeState`, `setBool`, `setNumber`, `fireTrigger`, `play`, `pause`, and `reset`. The hook is appropriate when the application needs the underlying player or a separately placed canvas; `Kerf` is the simpler default.

Current source-level caveats to address before presenting every hook option as supported:

- `UseKerfOptions.stateMachine` is declared in the TypeScript interface but is not consumed by the current hook implementation.
- Hook `inputs` are applied on load; later changes should be sent with the returned setters. The current hook effect does not watch `options.inputs` for changes.
- The `Kerf` component reload effect includes callback props in its dependency list. Keep callbacks stable with `useCallback` to avoid unnecessary reloads when a parent rerenders.
- Neither API currently exposes an `onError` callback.

These gaps should either be fixed in code and covered by examples or clearly marked as limitations. The website API must reflect the released package, not only the declared interface.

### Next.js and server rendering

Document a client component boundary (`'use client'`) for the player and, where needed, load the demo with `next/dynamic` and `ssr: false`. The runtime fetches `.kfx` data and renders through browser APIs; it is not a server-side renderer. Include a Next.js example that is built against the exact package version supported by the docs.

### Runtime exports

The package root currently exports:

- `Kerf`, `KerfProps`, and `KerfRef`.
- `useKerf`, `UseKerfOptions`, and `UseKerfResult`.
- `loadKfxFromUrl`, `loadKfxFromArrayBuffer`, and `KerfRuntimePlayer`.
- `cubicBezier`, `evaluateTrack`, `evaluateGeometryTrack`, and `evaluateGeometryNodeTrack`.
- `KfxPackage`, manifest/scene/layer/geometry, animation, state-machine, listener, and pose TypeScript interfaces.

`KerfRuntimePlayer` provides `advance`, `computePose`, `renderToCanvas`, `getArtboardCoords`, state-machine input setters, and pointer/keyboard/text input handlers. Document direct player use as an advanced browser API and give one tested example. It is a browser canvas runtime, not a native rendering surface API.

## 7. Native Platform Documentation

The current native application is a Windows x64 desktop program. It uses C# WPF for the app/UI, a Direct3D 11 scene renderer, and the `kerf_core.dll` Rust library through C# P/Invoke. The build script can compile the Rust core when Cargo is installed and can fall back to the checked-in DLL.

The Rust crate exports C ABI functions for Bézier bounds/flattening, path hit testing and measurement, convex hull, liquid glass rendering, animation sampling, and state-machine primitives. These are internal engine functions. The codebase does not ship a public C header or a complete cross-platform `.kfx` player API, renderer bindings, or platform binaries for iOS/Android/macOS/Linux.

For a native API reference, the website may inventory the current **internal** exports from `native-windows/crates/kerf_core/src/lib.rs`:

| API group | Current exported symbol examples | Public SDK status |
|---|---|---|
| Engine diagnostics | `kerf_engine_init`, `kerf_engine_version`, `kerf_engine_simd_desc`, `kerf_engine_free_string` | Internal C ABI; no published header/package. |
| Geometry | `kerf_path_compute_bounds`, `kerf_bezier_adaptive_flatten`, `kerf_path_hit_test`, `kerf_path_length`, `kerf_simplify_points`, `kerf_path_signed_area`, `kerf_path_contains_point`, `kerf_convex_hull` | Internal geometry primitives; not a complete animation player. |
| Animation | `kerf_anim_context_create/destroy`, `kerf_anim_clip_create`, `kerf_anim_clip_add_track`, `kerf_anim_track_add_keyframe`, `kerf_anim_track_sample` | Internal low-level animation context API. |
| State machine | `kerf_sm_create`, `kerf_sm_add_input`, `kerf_sm_set_bool`, `kerf_sm_set_number`, `kerf_sm_fire_trigger`, `kerf_sm_add_layer/state/transition`, `kerf_sm_transition_add_condition`, `kerf_sm_advance`, `kerf_sm_get_property_value`, `kerf_sm_get_current_state` | Internal state-machine primitives; does not load `.kfx` or draw a platform surface. |
| Pixel rendering | `kerf_render_liquid_glass_v2`, `kerf_render_liquid_glass` | Internal image-effect routines, not a general scene renderer. |

The C# `RustEngineInterop` layer calls these exports from `kerf_core.dll` using P/Invoke and has managed fallbacks for supported operations. A future public native SDK needs published headers/bindings, lifecycle/error ownership rules, `.kfx` loading, a renderer/surface contract, input/event APIs, platform binaries, and tested sample projects. Until then, present the table as an engineering inventory, not as a supported app integration API.

Native documentation should therefore contain:

1. Windows download and installation steps.
2. Windows system requirements and the current Beta channel.
3. Build-from-source instructions pointing to `native-windows/build.bat` and Rust/Cargo requirements.
4. A clear support table separating the Windows app from future native runtimes.
5. A “not available yet” statement for React Native, iOS, Android, macOS, and Linux SDKs.

Before advertising any non-Windows native SDK, implement and publish a stable package for that platform, a public header/bindings, runtime lifecycle/load/render/input APIs, example app, supported OS/architecture matrix, and compatibility policy. The old in-app guide includes illustrative C declarations and Metal/Vulkan/WASM renderer claims that do not match shipped code; treat those as stale proposal text, not product capability.

## 8. `.kfx` Format Documentation

### Container and current entries

`.kfx` is a ZIP archive containing JSON payloads. The current exporter (`KfxPackage.ExportPackage`) writes:

```text
animation.kfx
├── manifest.json        Required by the React loader
├── scene.json           Required by the React loader
├── animations.json      Required by the React loader
├── state-machines.json  Written by the exporter; optional in the React loader
└── metadata.json        Optional; included by default by the desktop exporter
```

The current exporter does not emit the `assets/` tree described in the old in-app guide. It serializes scene/layer data and optional editor metadata; it does not currently package a folder of image/font/audio binaries. Documentation must not promise embedded binary assets until that behavior exists.

### Manifest

The desktop exporter currently writes these fields:

```json
{
  "FormatVersion": "1.0.0",
  "Generator": "Kerf 3.1 Beta",
  "CreatedAt": "<ISO-8601 UTC timestamp>",
  "RuntimeCompatibility": ">=1.0.0",
  "Title": "<document name>",
  "HasMetadata": true,
  "EntryPoints": {
    "defaultClipId": 0,
    "defaultStateMachine": "StateMachine"
  }
}
```

`defaultClipId` and state-machine values depend on the document. Format the example as illustrative; do not promise fixed IDs. JSON property names are currently PascalCase in the exported model.

### Payload overview

- **`scene.json`:** active page ID/name, logical width/height, background color, and hierarchical layer list. The exporter selects the first positive-size root frame for dimensions when present; otherwise it uses 1920×1080 defaults.
- **`animations.json`:** an array of clips. A clip has ID/name/duration, optional work area/FPS/loop mode, and tracks. Tracks contain target/property identifiers and ordered keyframes.
- **Keyframes:** `Time`, `Value`, `Interp`, optional cubic Bézier controls (`X1`, `Y1`, `X2`, `Y2`), spring parameters, and optional geometry or geometry-node values. Runtime interpolation numbers are 0 Hold, 1 Linear, 2 Cubic, and 3 Spring. Loop modes are 0 Once, 1 Loop, and 2 PingPong.
- **`state-machines.json`:** the current state-machine definition: inputs, state-machine layers, states, transitions, condition groups, and listeners/actions. Inputs include Boolean, Number, and Trigger kinds. Runtime package interface currently exposes one `stateMachine` object.
- **`metadata.json`:** optional editor data including document ID/name, active page, timestamps, schema version, asset-library map, and pages. It is included by default in the desktop exporter and can be omitted with `includeEditorMetadata: false` in the C# API.

Layer, property, listener, and action enum values are serialized numerically. A public schema page should publish mapping tables generated/checked against `ElementType`, `PropKey`, animation types, and state-machine enums in source; do not infer enum numbers from labels in the old desktop guide.

### Load and compatibility behavior

- `loadKfxFromUrl(url)` fetches the complete response as an `ArrayBuffer` and then unzips it with `fflate`.
- `loadKfxFromArrayBuffer(buffer)` parses ZIP entries into a `KfxPackage` with `manifest`, `scene`, `animations`, and optional `stateMachine`.
- The web loader throws for missing required entries or malformed JSON/ZIP data. `state-machines.json` is optional to the React loader.
- The native importer can read optional metadata, rebuild a document when metadata is absent, and runs canonical document migration/reference validation before returning.
- On Windows import, `KfxSecurityValidator` rejects traversal-style ZIP entry names and caps total uncompressed package data at 250 MB. The React loader currently uses `unzipSync` and does not apply that same explicit validation/limit. Do not describe the web loader as having identical validation; consider adding consistent size/entry checks before exposing it to untrusted uploads.
- The file contains declarative JSON, not a plugin or script extension point. The website can say the current schema is parsed as data, but should not substitute that for a broader security audit.

### C# package API

The native source exposes `KfxPackage.ExportPackage(KerfDocument doc, string targetFilePath, bool includeEditorMetadata = true)` and `KfxPackage.ImportPackage(string packageFilePath)` (plus an overload returning a `MigrationReport`). These are application source APIs, not currently a published .NET package. Do not label them a supported public SDK until a distributable library and compatibility contract exist.

## 9. Desktop Product Documentation

Create short, task-based guides rather than duplicating a long feature list on every page:

- Install and first launch; create/open/save `.kerf` projects.
- Canvas, pages, zoom/pan, selection, marquee, and keyboard shortcuts.
- Vector shapes, pen/path editing, fills/strokes, text, transforms, and alignment.
- Layer hierarchy, multi-selection, grouping, components, and auto layout.
- Prototyping, animation clips, keyframes, easing, state machines, inputs, and pointer listeners.
- Exporting `.kfx`, SVG, PNG/JPEG, PDF, MP4, and GIF. Verify each format is present in the release UI before marking it supported.
- Importing `.kfx` and migration/compatibility behavior.
- Troubleshooting render initialization, missing Rust DLL fallback, file recovery/autosave, and failed exports.

Root `README.md` and `PROGRESS.md` are useful overview sources, but the website should validate feature claims against the current UI/engine and the release build. The desktop `RuntimeGuideDialog.cs` is not a reliable website source for package names, hooks, native API declarations, assets, or non-Windows support.

## 10. Content and Maintenance Rules

- Keep a visible “Beta” label until the release channel changes.
- Version the Studio manual, React package docs, and `.kfx` format independently.
- Include “last verified against” metadata on API and format pages.
- Derive API signatures from TypeScript declarations and validate example snippets in a small sample app during release work.
- Reconcile the `kerf-react` README and in-app guide with the implementation before using either as canonical web documentation.
- Use exact exported package names and APIs (`kerf-react`, `Kerf`, `useKerf`).
- Store release notes and download metadata alongside the release process; fail the build or show a clear unavailable state if the manifest has no valid asset URL.
- Explain browser hosting requirements (public URL, fetchable MIME/URL, CORS for another origin, and a sized parent element).
- Keep the project/license and attribution language consistent with the published package and desktop distribution terms.

## 11. Implementation Phases and Acceptance Criteria

### Phase A — Site foundation

- Create the Astro/Starlight project, global brand theme, navigation, footer, mobile layout, and accessible page templates.
- Add homepage and product/platform status cards.
- Add search and source/npm links.

**Accept when:** routes render on mobile and desktop, navigation works with keyboard, metadata/SEO is present, and Beta/platform status is visible without scrolling to the footer.

### Phase B — Download and release content

- Add release manifest and download page.
- Add release notes and install/uninstall guide.
- Add release workflow generation of SHA-256 and asset metadata, or define a documented manual publishing procedure.

**Accept when:** the site link resolves to a published `KerfSetup.exe`, displayed version matches artifact, checksum verifies, and unavailable releases cannot display a stale link as current.

### Phase C — React docs and examples

- Add installation, quick start, ref, hook, API reference, Next.js/client-only guide, and troubleshooting.
- Create a small example project using the exact npm package version and a public `.kfx` sample.
- Correct/document current hook and callback caveats or fix them in the package first.

**Accept when:** code examples type-check/build against the package, input/state callbacks work, and package loading errors are understandable.

### Phase D — `.kfx` and native docs

- Publish the ZIP entry layout, manifest, schema, enums, compatibility, security limits, and sample file.
- Publish the platform matrix and Windows build-from-source instructions.
- Remove claims copied from stale in-app native-guide text.

**Accept when:** the schema page matches a generated sample and `KfxPackage.cs`; every platform row is labeled shipped, experimental, planned, or unsupported.

### Phase E — Launch readiness

- Configure production domain/hosting, redirects, 404 page, release workflow, link checking, and documentation review owners.
- Review download/license terms and package attribution.

**Accept when:** production URLs, downloads, npm links, docs search, release metadata, and support links have been checked on the deployed site.

## 12. Open Decisions Before Launch

1. Production domain and static hosting provider.
2. Canonical public release location (GitHub Releases or another approved host) and release automation owner.
3. Whether Beta installers are code-signed and how Windows SmartScreen guidance should be worded.
4. Confirmed minimum Windows version and .NET runtime requirement against a clean machine.
5. Desktop license/distribution terms and final copyright/support contact.
6. Whether to expose the standalone IIFE runtime as a supported public entry point.
7. Whether to fix `useKerf`'s unused `stateMachine` option, reactive input handling, callback effect behavior, and add `onError` before documenting those behaviors.
8. Whether to add a real native SDK roadmap; do not advertise one until owners and supported targets are approved.

## 13. Code and Content Sources

- [Root product overview and build instructions](README.md)
- [Current development record](PROGRESS.md)
- [`kerf-react` package metadata](packages/kerf-react/package.json)
- [`kerf-react` package README](packages/kerf-react/README.md)
- [`Kerf` component and props](packages/kerf-react/src/Kerf.tsx)
- [`useKerf` hook](packages/kerf-react/src/useKerf.tsx)
- [Runtime and `.kfx` TypeScript interfaces](packages/kerf-react/src/runtime.ts)
- [Desktop package exporter/importer](native-windows/src/IO/KfxPackage.cs)
- [Desktop `.kfx` security validation](native-windows/src/IO/KfxSecurityValidator.cs)
- [Windows build and installer assembly](native-windows/build.bat)
- [Windows installer source](native-windows/KerfInstaller.cs)
- [Product version constants](native-windows/src/ProductInfo.cs)
- [Existing in-app runtime guide (contains stale claims; audit before reuse)](native-windows/src/UI/Dialogs/RuntimeGuideDialog.cs)
- [React example app](testdata/react-app)
- [Example `.kfx` packages](testdata/Test.kfx) and [Kerf banner package](assets/KerfBanner.kfx)
