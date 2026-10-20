# Kerf — Next-Gen Vector Design & Prototyping Studio

![React](https://img.shields.io/badge/React-19.2-blue?logo=react)
![TypeScript](https://img.shields.io/badge/TypeScript-5.x-blue?logo=typescript)
![Vite](https://img.shields.io/badge/Vite-8.x-646CFF?logo=vite)
![WebGL](https://img.shields.io/badge/WebGL-Hardware%20Acceleration-green)
![License](https://img.shields.io/badge/license-MIT-green)

**Kerf** is a state-of-the-art web-based vector design and prototyping studio inspired by Figma. Built with modern React 19, TypeScript, and Vite, Kerf features an infinite canvas, hardware-accelerated rendering, Figma-grade **Auto Layout**, vector path drawing, component hierarchy, interactive prototyping, and multi-format code exports.

---

## ✨ Features

- **🏠 Workspace Dashboard & Home Hub**:
  - Multi-workspace organization (Personal, Team, Enterprise).
  - Project management (create, duplicate, rename, export, import, delete).
  - Built-in UI templates (Mobile Banking App, SaaS Landing Page, Design System).
- **📐 Figma-Grade Auto Layout (`Shift + A`)**:
  - Wrap any selection or element into an auto-align frame instantly with <kbd>Shift</kbd> + <kbd>A</kbd>.
  - Toggle or remove Auto Layout with <kbd>Alt</kbd> + <kbd>Shift</kbd> + <kbd>A</kbd>.
  - **4 Direction / Layout Modes**:
    - **Horizontal Row (`->`)**: Flex row with primary and counter-axis alignment.
    - **Vertical Column (`↓`)**: Vertical stacking with flexible spacing.
    - **Wrap (`↩`)**: Flowing flex-wrap layout across rows.
    - **Grid (`⊞`)**: Multi-column responsive matrix layout.
  - **3x3 Interactive Alignment Matrix**: Figma-signature 9-point alignment box.
  - **Distribution**: Packed vs. Space Between (`auto` gap).
  - **Gap & 4-Side Padding**: Individual Top, Right, Bottom, Left controls with quick presets.
  - **Frame Sizing**: Hug Contents vs. Fixed Width / Height.
  - **Child Ordering**: Reorder children within auto-layout frames with a single click.
- **🖊️ Smooth Pen & Pencil Vector Tools**:
  - Full Bézier curve path drawing with anchor handles.
  - Relative coordinate vector engine: drawn paths move, scale, and align seamlessly with selection, grouping, auto-layout, and arrow-key nudges.
- **🎨 Infinite Canvas & Modern Design System**:
  - Smooth pan (<kbd>Space</kbd> + Drag or Middle Mouse) and pinch/scroll exponential zoom up to 3200%.
  - Smart snapping guides, rulers, custom pixel grids (isometric, dot, line).
  - Shapes: Rectangle, Ellipse, Star, Polygon / Triangle, Line, Arrow, Typography, Image.
  - Fills: Solid colors, linear gradients, opacity.
  - Strokes: Width, solid, dashed, dotted.
  - Effects: Real-time Drop Shadows, Inner Shadows, Layer Blurs.
- **⚡ Hardware Engine & WebGL Telemetry**:
  - Hybrid SVG/Canvas DOM rendering coupled with WebGL affine transform acceleration.
  - Real-time telemetry: FPS, frame time, draw calls, and memory monitoring.
- **🚀 Production Code & Asset Export**:
  - One-click CSS, Tailwind CSS, and SVG code generation with auto-layout flex rules.
  - Pixel-perfect PNG / JPG raster export at custom scale (1x, 2x, 3x) and SVG vector export.
  - Native `.kerf` JSON file backup and restore.
- **🐍 Optional Python Design Engine**:
  - Built-in Python console and lightweight Python backend for procedural design scripts and server-side project persistence.

---

## 🛠️ Tech Stack

- **Frontend**: [React 19](https://react.dev/), [TypeScript](https://www.typescriptlang.org/), [Vite 8](https://vitejs.dev/)
- **Icons**: [Lucide React](https://lucide.dev/)
- **Visuals & Canvas**: Vanilla HTML5 SVG / Canvas API + WebGL Shaders
- **Effects**: [canvas-confetti](https://www.npmjs.com/package/canvas-confetti)
- **Local Storage**: IndexedDB (custom client-side storage) + LocalStorage backup
- **Backend (Optional)**: Python 3 standard library `http.server` or FastAPI / Uvicorn

---

## 📋 Prerequisites

Before running the project locally or deploying, ensure you have:

- **Node.js**: `v18.0.0` or higher (Node.js 20+ LTS recommended)
- **npm**: `v9.0.0` or higher (or `pnpm` / `yarn`)
- **Python** *(optional, for Python backend)*: `v3.9` or higher

---

## 🚀 Running Locally

### 1. Clone the repository
```bash
git clone https://github.com/your-username/figma-kerf-studio.git
cd figma-kerf-studio
```

### 2. Install dependencies
```bash
npm install
```

### 3. Start the frontend development server
```bash
npm run dev
```

The Vite dev server will start at:
```
➜  Local:   http://localhost:5173/
➜  Network: use --host to expose
```
Open [http://localhost:5173/](http://localhost:5173/) in your web browser. The Home Workspace dashboard will load first!

---

### 4. (Optional) Start the Python Backend Server

The web application functions 100% standalone in the browser using client-side IndexedDB storage. If you would like to run the procedural Python script execution engine and server-side project saving:

```bash
# In a separate terminal:
cd server

# Run using Python's built-in standard library (no external packages required):
python app.py
```

The server will listen on `http://localhost:8000`.

*(Optional)* To install extended dependencies for FastAPI and headless rasterization:
```bash
pip install -r server/requirements.txt
```

---

## ⌨️ Essential Keyboard Shortcuts

| Shortcut | Description |
| :--- | :--- |
| <kbd>Shift</kbd> + <kbd>A</kbd> | **Add Auto Layout Frame** (auto-aligns selection into frame) |
| <kbd>Alt</kbd> + <kbd>Shift</kbd> + <kbd>A</kbd> | **Remove Auto Layout** |
| <kbd>V</kbd> | Select / Move tool |
| <kbd>H</kbd> | Hand / Pan tool |
| <kbd>Space</kbd> + **Drag** | Temporary quick pan |
| <kbd>P</kbd> | Pen vector path tool (<kbd>Enter</kbd> to finish path) |
| <kbd>R</kbd> | Rectangle tool |
| <kbd>O</kbd> | Ellipse / Circle tool |
| <kbd>T</kbd> | Text tool |
| <kbd>F</kbd> | Frame tool |
| <kbd>L</kbd> | Line tool |
| <kbd>Shift</kbd> + <kbd>L</kbd> | Arrow tool |
| <kbd>Ctrl</kbd> / <kbd>Cmd</kbd> + <kbd>G</kbd> | Group selected elements |
| <kbd>Ctrl</kbd> / <kbd>Cmd</kbd> + <kbd>Shift</kbd> + <kbd>G</kbd> | Ungroup selection |
| <kbd>Ctrl</kbd> / <kbd>Cmd</kbd> + <kbd>D</kbd> | Duplicate selection |
| <kbd>Ctrl</kbd> / <kbd>Cmd</kbd> + <kbd>Z</kbd> | Undo |
| <kbd>Ctrl</kbd> / <kbd>Cmd</kbd> + <kbd>Y</kbd> / <kbd>Shift</kbd> + <kbd>Z</kbd> | Redo |
| <kbd>Arrow Keys</kbd> | Nudge selected element 1px (hold <kbd>Shift</kbd> for 10px) |
| <kbd>Ctrl</kbd> + **Wheel** | Smooth zoom toward cursor |
| <kbd>?</kbd> | Open Keyboard Shortcuts Modal |

---

## 🏗️ Production Build

To validate TypeScript types and generate the optimized production bundle:

```bash
npm run build
```

The compiled output will be generated in the `dist/` directory:
- `dist/index.html`
- `dist/assets/index-*.js`
- `dist/assets/index-*.css`

To preview the production build locally:
```bash
npm run preview
```

---

## 🌐 Deployment Instructions

Kerf is built as a static Single Page Application (SPA), which makes it fast, reliable, and capable of being deployed anywhere at near-zero cost.

### Option 1: Deploy to Vercel (Recommended)

#### Via Vercel CLI:
```bash
npm install -g vercel
vercel
```

#### Via GitHub / Vercel Web Dashboard:
1. Push your repository to GitHub / GitLab / Bitbucket.
2. Go to [Vercel](https://vercel.com/) and click **Add New Project**.
3. Import your repository.
4. Framework Preset will be automatically detected as **Vite**.
   - **Build Command**: `npm run build`
   - **Output Directory**: `dist`
   - **Install Command**: `npm install`
5. Click **Deploy**.

---

### Option 2: Deploy to Netlify

#### Via Netlify CLI:
```bash
npm install -g netlify-cli
netlify deploy --prod --dir=dist
```

#### Via Netlify Dashboard:
1. Connect your repository on [Netlify](https://www.netlify.com/).
2. Set build settings:
   - **Base directory**: `.`
   - **Build command**: `npm run build`
   - **Publish directory**: `dist`
3. If using client-side routing, create a `public/_redirects` file with:
   ```
   /*    /index.html   200
   ```
4. Click **Deploy Site**.

---

### Option 3: Deploy with Docker

A production-ready `Dockerfile` using multi-stage builds with Nginx:

```dockerfile
# Stage 1: Build the React application
FROM node:20-alpine AS builder
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

# Stage 2: Serve via Nginx
FROM nginx:alpine
COPY --from=builder /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

Example `nginx.conf`:
```nginx
server {
    listen 80;
    server_name localhost;

    location / {
        root /usr/share/nginx/html;
        index index.html;
        try_files $uri $uri/ /index.html;
    }

    gzip on;
    gzip_types text/plain text/css application/json application/javascript text/xml application/xml text/javascript;
}
```

Build and run the Docker image:
```bash
docker build -t kerf-studio .
docker run -p 8080:80 kerf-studio
```
Then visit `http://localhost:8080`.

---

### Option 4: Deploy to GitHub Pages

1. In `vite.config.ts`, set the `base` path to your repository name:
   ```typescript
   export default defineConfig({
     base: '/<repository-name>/',
     plugins: [react()],
   });
   ```
2. Build the project:
   ```bash
   npm run build
   ```
3. Deploy the `dist` folder using the `gh-pages` package:
   ```bash
   npx gh-pages -d dist
   ```

---

### Option 5: Full-Stack Deployment (Frontend + Python Server)

If you wish to deploy the Python backend as well:

- **Frontend**: Deploy `dist/` to Vercel, Netlify, or Cloudflare Pages.
- **Python Backend**: Deploy `server/` to [Render](https://render.com/), [Railway](https://railway.app/), or [Fly.io](https://fly.io/).
  - Set the start command:
    ```bash
    python server/app.py
    ```
    or with Uvicorn:
    ```bash
    uvicorn server.app:app --host 0.0.0.0 --port $PORT
    ```

---

## 📁 Project Structure

```
├── public/                 # Static assets & icons
├── server/                 # Python backend
│   ├── app.py              # Lightweight HTTP & procedural script server
│   ├── requirements.txt    # Optional Python dependencies
│   └── data/               # Persistent server JSON project files
├── src/
│   ├── assets/             # Brand logos & graphics
│   ├── components/         # React UI Components
│   │   ├── Canvas.tsx              # Interactive SVG/Canvas workspace
│   │   ├── HomePage.tsx            # Home dashboard & workspace manager
│   │   ├── TopNav.tsx              # Toolbar, breadcrumbs & actions
│   │   ├── LeftSidebar.tsx         # Layers tree & assets inspector
│   │   ├── RightSidebar.tsx        # Properties & Auto Layout panel
│   │   ├── ContextMenu.tsx         # Right-click context menu
│   │   ├── ShortcutsModal.tsx      # Keyboard shortcuts cheat sheet
│   │   ├── PropertiesModal.tsx     # Fullscreen property inspector
│   │   ├── PrototypeModal.tsx      # Interactive prototyping player
│   │   ├── EngineModal.tsx         # WebGL telemetry & hardware stats
│   │   ├── PythonConsoleModal.tsx  # Procedural design script runner
│   │   └── CanvasSettingsModal.tsx # Grids, rulers & background setup
│   ├── engine/             # WebGL rendering engine & shaders
│   ├── storage/            # IndexedDB device storage adapter
│   ├── utils/
│   │   ├── autoLayout.ts    # Figma Auto Layout calculation engine
│   │   ├── vectorUtils.ts   # Bézier & relative SVG path utilities
│   │   ├── export.ts        # SVG, PNG, JPG & JSON export engine
│   │   └── codeGenerator.ts # CSS & Tailwind code generation
│   ├── templates.ts        # Built-in sample designs (Banking, SaaS)
│   ├── types.ts            # TypeScript interfaces & types
│   ├── App.tsx             # Main application state & shortcut manager
│   ├── main.tsx            # React application entry point
│   └── index.css           # Global design system & theme variables
├── package.json            # Project dependencies & build scripts
├── tsconfig.json           # TypeScript configuration
├── vite.config.ts          # Vite build configuration
└── README.md               # Documentation
```

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
