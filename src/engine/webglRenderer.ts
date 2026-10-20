import type { FigmaElement, Viewport, EngineStats } from '../types';
import { wasmEngine } from './wasmEngine';

const VERTEX_SHADER_SOURCE = `
  attribute vec2 a_position;
  attribute vec2 a_texCoord;
  attribute vec4 a_color;

  uniform mat3 u_matrix;
  uniform vec2 u_resolution;

  varying vec2 v_texCoord;
  varying vec4 v_color;

  void main() {
    // 2D affine transformation
    vec3 position = u_matrix * vec3(a_position, 1.0);
    
    // Convert to clip space [-1, 1]
    vec2 zeroToOne = position.xy / u_resolution;
    vec2 zeroToTwo = zeroToOne * 2.0;
    vec2 clipSpace = zeroToTwo - 1.0;

    gl_Position = vec4(clipSpace * vec2(1, -1), 0, 1);
    v_texCoord = a_texCoord;
    v_color = a_color;
  }
`;

const FRAGMENT_SHADER_SOURCE = `
  precision mediump float;

  varying vec2 v_texCoord;
  varying vec4 v_color;

  uniform int u_renderMode; // 0 = standard, 1 = wireframe, 2 = glowing grid

  void main() {
    if (u_renderMode == 1) {
      // Wireframe mode: edges highlighted
      float edge = min(min(v_texCoord.x, 1.0 - v_texCoord.x), min(v_texCoord.y, 1.0 - v_texCoord.y));
      if (edge < 0.05) {
        gl_FragColor = vec4(0.05, 0.6, 1.0, 1.0);
      } else {
        gl_FragColor = vec4(0.05, 0.6, 1.0, 0.15);
      }
      return;
    }

    // Hybrid mode (0): Completely transparent so SVG rounded corners and vector elements render with pristine curves without sharp quad bleed-through
    gl_FragColor = vec4(0.0, 0.0, 0.0, 0.0);
  }
`;

export class WebGLRenderer {
  private canvas: HTMLCanvasElement | null = null;
  private gl: WebGLRenderingContext | null = null;
  private program: WebGLProgram | null = null;

  // Shader attributes & uniforms
  private aPositionLocation = -1;
  private aTexCoordLocation = -1;
  private aColorLocation = -1;
  private uMatrixLocation: WebGLUniformLocation | null = null;
  private uResolutionLocation: WebGLUniformLocation | null = null;
  private uRenderModeLocation: WebGLUniformLocation | null = null;

  // Buffers
  private vertexBuffer: WebGLBuffer | null = null;

  // Metrics
  private frameCount = 0;
  private lastFpsTime = performance.now();
  private currentFps = 60;
  private drawCalls = 0;
  private vertexCount = 0;
  private culledCount = 0;
  private renderMode: 'hybrid' | 'webgl' | 'wireframe' = 'hybrid';
  private hardwareAcceleration = true;

  public init(canvas: HTMLCanvasElement): boolean {
    this.canvas = canvas;
    const gl = canvas.getContext('webgl', {
      alpha: true,
      antialias: true,
      premultipliedAlpha: false,
    });

    if (!gl) {
      console.warn('WebGL not supported on this browser');
      return false;
    }

    this.gl = gl;
    const program = this.createProgram(gl, VERTEX_SHADER_SOURCE, FRAGMENT_SHADER_SOURCE);
    if (!program) return false;

    this.program = program;
    this.aPositionLocation = gl.getAttribLocation(program, 'a_position');
    this.aTexCoordLocation = gl.getAttribLocation(program, 'a_texCoord');
    this.aColorLocation = gl.getAttribLocation(program, 'a_color');

    this.uMatrixLocation = gl.getUniformLocation(program, 'u_matrix');
    this.uResolutionLocation = gl.getUniformLocation(program, 'u_resolution');
    this.uRenderModeLocation = gl.getUniformLocation(program, 'u_renderMode');

    this.vertexBuffer = gl.createBuffer();

    // Enable standard alpha blending
    gl.enable(gl.BLEND);
    gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);

    return true;
  }

  private createProgram(
    gl: WebGLRenderingContext,
    vsSource: string,
    fsSource: string
  ): WebGLProgram | null {
    const vs = this.compileShader(gl, gl.VERTEX_SHADER, vsSource);
    const fs = this.compileShader(gl, gl.FRAGMENT_SHADER, fsSource);
    if (!vs || !fs) return null;

    const prog = gl.createProgram();
    if (!prog) return null;

    gl.attachShader(prog, vs);
    gl.attachShader(prog, fs);
    gl.linkProgram(prog);

    if (!gl.getProgramParameter(prog, gl.LINK_STATUS)) {
      console.warn('Program link error:', gl.getProgramInfoLog(prog));
      return null;
    }

    return prog;
  }

  private compileShader(gl: WebGLRenderingContext, type: number, source: string): WebGLShader | null {
    const shader = gl.createShader(type);
    if (!shader) return null;

    gl.shaderSource(shader, source);
    gl.compileShader(shader);

    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
      console.warn('Shader compile error:', gl.getShaderInfoLog(shader));
      gl.deleteShader(shader);
      return null;
    }

    return shader;
  }

  public render(
    elements: FigmaElement[],
    viewport: Viewport,
    width: number,
    height: number
  ) {
    if (!this.gl || !this.program || !this.canvas) return;
    const gl = this.gl;

    // Handle high DPI resizing
    const dpr = window.devicePixelRatio || 1;
    const displayWidth = Math.round(width * dpr);
    const displayHeight = Math.round(height * dpr);

    if (this.canvas.width !== displayWidth || this.canvas.height !== displayHeight) {
      this.canvas.width = displayWidth;
      this.canvas.height = displayHeight;
    }

    gl.viewport(0, 0, this.canvas.width, this.canvas.height);
    gl.clearColor(0, 0, 0, 0);
    gl.clear(gl.COLOR_BUFFER_BIT);

    gl.useProgram(this.program);

    // Uniform resolution
    gl.uniform2f(this.uResolutionLocation, width, height);

    // Render mode: 0 standard, 1 wireframe
    gl.uniform1i(this.uRenderModeLocation, this.renderMode === 'wireframe' ? 1 : 0);

    // Camera Viewport 3x3 matrix:
    // [ zoom,    0,    viewport.x ]
    // [    0, zoom,    viewport.y ]
    // [    0,    0,             1 ]
    const matrix = [
      viewport.zoom, 0, 0,
      0, viewport.zoom, 0,
      viewport.x, viewport.y, 1,
    ];
    gl.uniformMatrix3fv(this.uMatrixLocation, false, new Float32Array(matrix));

    // Spatial culling and vertex gathering via Wasm Engine
    const visibleElements: FigmaElement[] = [];
    let culled = 0;

    for (const el of elements) {
      if (!el.visible) continue;
      if (wasmEngine.isVisibleInViewport(el, viewport, width, height)) {
        visibleElements.push(el);
      } else {
        culled++;
      }
    }

    this.culledCount = culled;

    if (visibleElements.length === 0) {
      this.drawCalls = 0;
      this.vertexCount = 0;
      this.updateFps();
      return;
    }

    // Allocate batch vertex buffer (6 vertices * 8 floats per element = 48 floats)
    const totalFloats = visibleElements.length * 48;
    const batchData = new Float32Array(totalFloats);

    let offset = 0;
    visibleElements.forEach((el, index) => {
      const res = wasmEngine.tessellateElement(el, index);
      batchData.set(res.vertices, offset);
      offset += 48;
    });

    gl.bindBuffer(gl.ARRAY_BUFFER, this.vertexBuffer);
    gl.bufferData(gl.ARRAY_BUFFER, batchData, gl.DYNAMIC_DRAW);

    // Stride is 8 floats * 4 bytes = 32 bytes
    const STRIDE = 32;

    // Position (x, y) - 2 floats, offset 0
    gl.enableVertexAttribArray(this.aPositionLocation);
    gl.vertexAttribPointer(this.aPositionLocation, 2, gl.FLOAT, false, STRIDE, 0);

    // TexCoord (u, v) - 2 floats, offset 8
    gl.enableVertexAttribArray(this.aTexCoordLocation);
    gl.vertexAttribPointer(this.aTexCoordLocation, 2, gl.FLOAT, false, STRIDE, 8);

    // Color (r, g, b, a) - 4 floats, offset 16
    gl.enableVertexAttribArray(this.aColorLocation);
    gl.vertexAttribPointer(this.aColorLocation, 4, gl.FLOAT, false, STRIDE, 16);

    // Draw all elements in single GPU draw call!
    const verticesToDraw = visibleElements.length * 6;
    gl.drawArrays(gl.TRIANGLES, 0, verticesToDraw);

    this.drawCalls = 1;
    this.vertexCount = verticesToDraw;
    this.updateFps();
  }

  private updateFps() {
    this.frameCount++;
    const now = performance.now();
    if (now - this.lastFpsTime >= 500) {
      this.currentFps = Math.round((this.frameCount * 1000) / (now - this.lastFpsTime));
      this.frameCount = 0;
      this.lastFpsTime = now;
    }
  }

  public setRenderMode(mode: 'hybrid' | 'webgl' | 'wireframe') {
    this.renderMode = mode;
  }

  public getStats(): EngineStats {
    const wasmStats = wasmEngine.getTelemetry();
    return {
      fps: this.currentFps,
      frameTimeUs: wasmStats.lastExecutionUs,
      wasmInstructions: wasmStats.instructionCount,
      wasmMemoryBytes: wasmStats.heapBytes,
      webglDrawCalls: this.drawCalls,
      webglVertexCount: this.vertexCount,
      culledElements: this.culledCount,
      hardwareAcceleration: this.hardwareAcceleration,
      webglActive: !!this.gl,
      renderMode: this.renderMode,
    };
  }

  public toggleHardwareAcceleration(enabled: boolean) {
    this.hardwareAcceleration = enabled;
  }
}

export const webglRenderer = new WebGLRenderer();
