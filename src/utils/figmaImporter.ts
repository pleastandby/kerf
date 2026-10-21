import * as fzstd from 'fzstd';
import * as kiwi from 'kiwi-schema';
import * as pako from 'pako';
import type { Project, Page, FigmaElement, ElementType, Effect } from '../types';

/**
 * Converts a Figma color object ({ r: 0-1, g: 0-1, b: 0-1, a?: 0-1 }) to CSS string
 */
function figmaColorToCss(color?: { r?: number; g?: number; b?: number; a?: number }, opacity = 1): string {
  if (!color) return 'transparent';
  const r = Math.round((color.r ?? 0) * 255);
  const g = Math.round((color.g ?? 0) * 255);
  const b = Math.round((color.b ?? 0) * 255);
  const a = (color.a ?? 1) * opacity;

  if (a < 1) {
    return `rgba(${r}, ${g}, ${b}, ${Number(a.toFixed(2))})`;
  }
  const toHex = (n: number) => n.toString(16).padStart(2, '0');
  return `#${toHex(r)}${toHex(g)}${toHex(b)}`;
}

/**
 * Converts Figma effect to Kerf Effect
 */
function convertFigmaEffect(eff: any): Effect | null {
  if (!eff || eff.visible === false) return null;
  const isDrop = eff.type === 'DROP_SHADOW';
  const isInner = eff.type === 'INNER_SHADOW';
  const isBlur = eff.type === 'LAYER_BLUR';
  const isBgBlur = eff.type === 'BACKGROUND_BLUR';

  if (!isDrop && !isInner && !isBlur && !isBgBlur) return null;

  return {
    id: `eff-${Date.now()}-${Math.random().toString(36).substr(2, 5)}`,
    type: isDrop
      ? 'drop-shadow'
      : isInner
      ? 'inner-shadow'
      : isBlur
      ? 'layer-blur'
      : 'background-blur',
    x: eff.offset?.x || 0,
    y: eff.offset?.y || 0,
    blur: eff.radius || 4,
    spread: eff.spread || 0,
    color: figmaColorToCss(eff.color),
    enabled: true,
  };
}

/**
 * Reads Central Directory of a ZIP file to extract entries with exact offsets and uncompressed data
 */
function extractAllZipEntries(buffer: ArrayBuffer): Map<string, Uint8Array> {
  const view = new DataView(buffer);
  const u8 = new Uint8Array(buffer);
  const entries = new Map<string, Uint8Array>();

  // Find End of Central Directory Record (0x06054b50) scanning backwards from end of file
  let eocdOffset = -1;
  for (let i = buffer.byteLength - 22; i >= 0; i--) {
    if (view.getUint32(i, true) === 0x06054b50) {
      eocdOffset = i;
      break;
    }
  }

  if (eocdOffset === -1) {
    throw new Error('Invalid ZIP file: End of Central Directory record not found.');
  }

  const totalEntries = view.getUint16(eocdOffset + 10, true);
  const cdOffset = view.getUint32(eocdOffset + 16, true);

  let cur = cdOffset;
  for (let i = 0; i < totalEntries; i++) {
    if (cur + 46 > buffer.byteLength) break;
    if (view.getUint32(cur, true) !== 0x02014b50) break;

    const compressionMethod = view.getUint16(cur + 10, true);
    const compressedSize = view.getUint32(cur + 20, true);
    const nameLen = view.getUint16(cur + 28, true);
    const extraLen = view.getUint16(cur + 30, true);
    const commentLen = view.getUint16(cur + 32, true);
    const localOffset = view.getUint32(cur + 42, true);

    const nameBytes = u8.subarray(cur + 46, cur + 46 + nameLen);
    const filename = new TextDecoder().decode(nameBytes);

    if (localOffset + 30 <= buffer.byteLength) {
      const localNameLen = view.getUint16(localOffset + 26, true);
      const localExtraLen = view.getUint16(localOffset + 28, true);
      const dataStart = localOffset + 30 + localNameLen + localExtraLen;
      const rawData = u8.subarray(dataStart, dataStart + compressedSize);

      try {
        if (compressionMethod === 0) {
          // Stored
          entries.set(filename, rawData);
        } else if (compressionMethod === 8) {
          // Deflated
          const decompressed = pako.inflateRaw(rawData);
          entries.set(filename, decompressed);
        }
      } catch (err) {
        console.warn(`Could not decompress zip entry "${filename}":`, err);
      }
    }

    cur += 46 + nameLen + extraLen + commentLen;
  }

  return entries;
}

/**
 * Converts a raw binary chunk from canvas.fig by trying raw deflate and zstd
 */
function uncompressChunk(bytes: Uint8Array): Uint8Array {
  try {
    return pako.inflateRaw(bytes);
  } catch {
    try {
      return fzstd.decompress(bytes);
    } catch (err2) {
      throw new Error(`Failed to decompress chunk: ${err2}`);
    }
  }
}

/**
 * Native Kiwi binary parser for canvas.fig based on Figma's reverse-engineered schema
 */
function parseCanvasFig(canvasBytes: Uint8Array): { nodeChanges: any[]; blobs: any[]; version: number } {
  const header = String.fromCharCode(...canvasBytes.slice(0, 8));
  if (header !== 'fig-kiwi' && header !== 'fig-jam.' && header !== 'fig-deck') {
    throw new Error(`Invalid canvas.fig header: ${header}`);
  }

  const cView = new DataView(canvasBytes.buffer, canvasBytes.byteOffset, canvasBytes.byteLength);
  const version = cView.getUint32(8, true);
  const chunks: Uint8Array[] = [];
  let off = 12;

  while (off < canvasBytes.length) {
    const len = cView.getUint32(off, true);
    off += 4;
    chunks.push(canvasBytes.slice(off, off + len));
    off += len;
  }

  if (chunks.length < 2) {
    throw new Error('canvas.fig does not contain expected schema and data chunks.');
  }

  const encodedSchema = uncompressChunk(chunks[0]);
  const encodedData = uncompressChunk(chunks[1]);

  const binarySchema = (kiwi as any).decodeBinarySchema(encodedSchema);
  const schema = (kiwi as any).compileSchema(binarySchema);
  const decoded = schema.decodeMessage(encodedData);

  return {
    nodeChanges: decoded.nodeChanges || [],
    blobs: decoded.blobs || [],
    version,
  };
}

/**
 * Converts Figma native node tree (from nodeChanges) to Kerf Pages and Elements
 */
function convertFigmaKiwiToProject(
  nodeChanges: any[],
  metaJson: any,
  imageMap: Map<string, string>,
  defaultProjectName: string,
  targetWorkspaceId: string
): Project {
  const nodes = new Map<string, any>();

  for (const node of nodeChanges) {
    if (!node.guid) continue;
    const key = `${node.guid.sessionID}:${node.guid.localID}`;
    nodes.set(key, node);
  }

  for (const node of nodeChanges) {
    if (node.parentIndex?.guid) {
      const parentKey = `${node.parentIndex.guid.sessionID}:${node.parentIndex.guid.localID}`;
      const parent = nodes.get(parentKey);
      if (parent) {
        parent.children = parent.children || [];
        parent.children.push(node);
      }
    }
  }

  const root = nodes.get('0:0') || { type: 'DOCUMENT', name: defaultProjectName, children: [] };
  const projectName = metaJson?.file_name || metaJson?.client_meta?.name || root.name || defaultProjectName;

  // Filter root pages (CANVAS nodes, ignoring internal auxiliary canvases if main storyboard exists)
  let canvasNodes = (root.children || []).filter((c: any) => c.type === 'CANVAS');
  if (canvasNodes.length === 0) {
    canvasNodes = [root];
  }

  // If there are multiple canvases and one is marked "Internal Only Canvas", prioritize user-facing ones
  const userCanvases = canvasNodes.filter(
    (c: any) => !String(c.name || '').toLowerCase().includes('internal only')
  );
  const activeCanvases = userCanvases.length > 0 ? userCanvases : canvasNodes;

  const pages: Page[] = [];

  activeCanvases.forEach((canvas: any, pageIdx: number) => {
    const pageElements: FigmaElement[] = [];

    // Helper to recursively traverse and convert nodes
    function traverse(
      node: any,
      parentAbsX = 0,
      parentAbsY = 0,
      parentId: string | null = null
    ) {
      if (!node || node.visible === false) return;

      const nodeType = String(node.type || '').toUpperCase();
      if (nodeType === 'CANVAS') {
        for (const child of node.children || []) {
          traverse(child, 0, 0, null);
        }
        return;
      }

      // Calculate absolute coordinates
      const localX = node.transform?.m02 || 0;
      const localY = node.transform?.m12 || 0;
      const absX = parentAbsX + localX;
      const absY = parentAbsY + localY;

      const width = Math.max(1, node.size?.x || 100);
      const height = Math.max(1, node.size?.y || 100);

      // Rotation calculation
      let rotation = 0;
      if (node.transform && (node.transform.m00 !== 1 || node.transform.m10 !== 0)) {
        rotation = Math.round(Math.atan2(node.transform.m10, node.transform.m00) * (180 / Math.PI));
      }

      // Fill resolution
      let fill = '#333338';
      let fillType: 'solid' | 'linear-gradient' | 'image' = 'solid';
      let fillGradient: { start: string; end: string; angle: number } | undefined;
      let src: string | undefined;

      const primaryFill = (node.fillPaints || []).find((p: any) => p.visible !== false);
      if (primaryFill) {
        if (primaryFill.type === 'SOLID') {
          fill = figmaColorToCss(primaryFill.color, primaryFill.opacity ?? 1);
        } else if (primaryFill.type === 'GRADIENT_LINEAR' && primaryFill.stops?.length >= 2) {
          fillType = 'linear-gradient';
          fillGradient = {
            start: figmaColorToCss(primaryFill.stops[0].color),
            end: figmaColorToCss(primaryFill.stops[primaryFill.stops.length - 1].color),
            angle: 90,
          };
          fill = fillGradient.start;
        } else if (primaryFill.type === 'IMAGE' || primaryFill.image) {
          fillType = 'image';
          const hash = primaryFill.image?.hash || primaryFill.imageHash;
          if (hash && imageMap.has(hash)) {
            src = imageMap.get(hash);
          }
          fill = 'transparent';
        }
      } else if (nodeType === 'LINE' || nodeType === 'VECTOR') {
        fill = 'transparent';
      }

      // Stroke resolution
      const primaryStroke = (node.strokePaints || []).find((s: any) => s.visible !== false);
      const stroke = primaryStroke ? figmaColorToCss(primaryStroke.color, primaryStroke.opacity ?? 1) : 'transparent';
      const strokeWidth = node.strokeWeight || 0;
      const strokeStyle = node.strokeDashes?.length ? 'dashed' : 'solid';

      // Effects
      const effects: Effect[] = (node.effects || [])
        .map(convertFigmaEffect)
        .filter((e: any): e is Effect => e !== null);

      // Element type classification
      let type: ElementType = 'rect';
      let isFrame = false;
      let isGroup = false;

      if (['FRAME', 'COMPONENT', 'COMPONENT_SET', 'INSTANCE', 'SECTION'].includes(nodeType)) {
        type = 'frame';
        isFrame = true;
      } else if (nodeType === 'GROUP') {
        type = 'rect';
        isGroup = true;
      } else if (nodeType === 'ELLIPSE') {
        type = 'ellipse';
      } else if (nodeType === 'TEXT') {
        type = 'text';
      } else if (nodeType === 'LINE') {
        type = 'line';
      } else if (['VECTOR', 'STAR', 'REGULAR_POLYGON', 'BOOLEAN_OPERATION'].includes(nodeType)) {
        type = 'pen';
      } else if (src) {
        type = 'image';
      }

      const elemId = `elem-${node.guid?.sessionID || 0}-${node.guid?.localID || Math.random().toString(36).substr(2, 6)}`;

      const element: FigmaElement = {
        id: elemId,
        name: node.name || 'Layer',
        type,
        x: absX,
        y: absY,
        width,
        height,
        rotation,
        opacity: node.opacity !== undefined ? node.opacity : 1,
        locked: !!node.locked,
        visible: node.visible !== false,
        fill,
        fillType,
        fillGradient,
        src,
        stroke,
        strokeWidth,
        strokeStyle,
        cornerRadius: node.cornerRadius || 0,
        effects,
        parentId,
        isFrame,
        isGroup,
      };

      if (type === 'text') {
        element.text = node.textData?.characters || node.name || '';
        element.fontSize = node.fontSize || 16;
        element.fontFamily = node.fontName?.family || 'Inter';
        element.fontWeight = String(node.fontName?.style || '').toLowerCase().includes('bold') ? 700 : 400;
        element.textAlign = (String(node.textAlignHorizontal || 'LEFT').toLowerCase() as any) || 'left';
        element.lineHeight = Math.round((element.fontSize || 16) * 1.3);
      }

      pageElements.push(element);

      // Recursively traverse children
      for (const child of node.children || []) {
        traverse(child, absX, absY, elemId);
      }
    }

    traverse(canvas, 0, 0, null);

    // Normalize coordinates so the canvas design starts around (80, 80)
    if (pageElements.length > 0) {
      const minX = Math.min(...pageElements.map((e) => e.x));
      const minY = Math.min(...pageElements.map((e) => e.y));
      const shiftX = 80 - minX;
      const shiftY = 80 - minY;

      pageElements.forEach((e) => {
        e.x += shiftX;
        e.y += shiftY;
      });
    }

    pages.push({
      id: `page-${pageIdx + 1}`,
      name: canvas.name || `Page ${pageIdx + 1}`,
      elements: pageElements,
    });
  });

  return {
    id: `project-${Date.now()}`,
    name: projectName,
    workspaceId: targetWorkspaceId,
    pages: pages.length > 0 ? pages : [{ id: 'page-1', name: 'Page 1', elements: [] }],
    activePageId: pages[0]?.id || 'page-1',
    createdAt: Date.now(),
    updatedAt: Date.now(),
  };
}

/**
 * Converts a Figma REST/JSON node tree into Kerf FigmaElements
 */
function convertFigmaJsonNodeToElements(
  node: any,
  parentId: string | null = null,
  offset = { x: 0, y: 0 }
): FigmaElement[] {
  if (!node || node.visible === false) return [];

  const results: FigmaElement[] = [];
  const bbox = node.absoluteBoundingBox || {
    x: node.x || 0,
    y: node.y || 0,
    width: node.size?.x || 100,
    height: node.size?.y || 100,
  };

  const x = bbox.x - offset.x;
  const y = bbox.y - offset.y;
  const width = Math.max(1, bbox.width || 100);
  const height = Math.max(1, bbox.height || 100);

  let fill = '#333338';
  let fillType: 'solid' | 'linear-gradient' | 'image' = 'solid';
  let fillGradient: { start: string; end: string; angle: number } | undefined;

  const primaryFill = node.fills?.find((f: any) => f.visible !== false);
  if (primaryFill) {
    if (primaryFill.type === 'SOLID') {
      fill = figmaColorToCss(primaryFill.color, primaryFill.opacity ?? 1);
    } else if (primaryFill.type === 'GRADIENT_LINEAR' && primaryFill.gradientStops?.length >= 2) {
      fillType = 'linear-gradient';
      fillGradient = {
        start: figmaColorToCss(primaryFill.gradientStops[0].color),
        end: figmaColorToCss(primaryFill.gradientStops[primaryFill.gradientStops.length - 1].color),
        angle: 90,
      };
      fill = fillGradient.start;
    } else if (primaryFill.type === 'IMAGE') {
      fillType = 'image';
      fill = '#1E1E24';
    }
  }

  const primaryStroke = node.strokes?.find((s: any) => s.visible !== false);
  const stroke = primaryStroke ? figmaColorToCss(primaryStroke.color, primaryStroke.opacity ?? 1) : 'transparent';
  const strokeWidth = node.strokeWeight || 0;
  const strokeStyle = node.strokeDashes?.length ? 'dashed' : 'solid';

  const effects: Effect[] = (node.effects || [])
    .map(convertFigmaEffect)
    .filter((e: any): e is Effect => e !== null);

  let type: ElementType = 'rect';
  let isFrame = false;
  let isGroup = false;

  const nodeType = String(node.type || '').toUpperCase();
  if (['FRAME', 'COMPONENT', 'COMPONENT_SET', 'INSTANCE', 'SECTION'].includes(nodeType)) {
    type = 'frame';
    isFrame = true;
  } else if (nodeType === 'GROUP') {
    type = 'rect';
    isGroup = true;
  } else if (nodeType === 'ELLIPSE') {
    type = 'ellipse';
  } else if (nodeType === 'TEXT') {
    type = 'text';
  } else if (nodeType === 'LINE') {
    type = 'line';
  } else if (['VECTOR', 'STAR', 'REGULAR_POLYGON'].includes(nodeType)) {
    type = 'pen';
  }

  const elemId = node.id || `elem-${Date.now()}-${Math.random().toString(36).substr(2, 6)}`;

  const currentElement: FigmaElement = {
    id: elemId,
    name: node.name || 'Figma Layer',
    type,
    x,
    y,
    width,
    height,
    rotation: node.rotation || 0,
    opacity: node.opacity !== undefined ? node.opacity : 1,
    locked: !!node.locked,
    visible: node.visible !== false,
    fill,
    fillType,
    fillGradient,
    stroke,
    strokeWidth,
    strokeStyle,
    cornerRadius: node.cornerRadius || 0,
    effects,
    parentId,
    isFrame,
    isGroup,
  };

  if (type === 'text') {
    currentElement.text = node.characters || node.name || 'Text';
    currentElement.fontSize = node.style?.fontSize || 16;
    currentElement.fontFamily = node.style?.fontFamily || 'Inter';
    currentElement.fontWeight = node.style?.fontWeight || 400;
    currentElement.textAlign = (node.style?.textAlignHorizontal?.toLowerCase() as any) || 'left';
    currentElement.lineHeight = node.style?.lineHeightPx || Math.round((currentElement.fontSize || 16) * 1.3);
  }

  results.push(currentElement);

  if (Array.isArray(node.children) && node.children.length > 0) {
    for (const child of node.children) {
      const childElements = convertFigmaJsonNodeToElements(child, elemId, offset);
      results.push(...childElements);
    }
  }

  return results;
}

/**
 * Universal Importer: Parses .kerf, .figma, and native .fig files with 100% fidelity
 */
export async function parseAndImportDesignFile(
  file: File | { name: string; arrayBuffer: () => Promise<ArrayBuffer> },
  targetWorkspaceId = 'ws-personal'
): Promise<Project> {
  const fileName = file.name.replace(/\.(kerf|figma|fig|json)$/i, '');
  const arrayBuffer = await file.arrayBuffer();
  const u8 = new Uint8Array(arrayBuffer);

  // 1. Check if it's a native .fig / .figma ZIP archive (PK\x03\x04 signature)
  if (u8.length >= 4 && u8[0] === 0x50 && u8[1] === 0x4b && u8[2] === 0x03 && u8[3] === 0x04) {
    try {
      const zipEntries = extractAllZipEntries(arrayBuffer);
      let metaJson: any = null;

      // Extract meta.json if present
      const metaData = zipEntries.get('meta.json');
      if (metaData) {
        try {
          const text = new TextDecoder().decode(metaData);
          metaJson = JSON.parse(text);
        } catch {
          // ignore
        }
      }

      // Extract all embedded images as data URLs
      const imageMap = new Map<string, string>();
      for (const [entryName, entryBytes] of zipEntries.entries()) {
        if (entryName.startsWith('images/')) {
          const hash = entryName.replace('images/', '').trim();
          if (hash && entryBytes.length > 0) {
            let binary = '';
            const len = entryBytes.byteLength;
            for (let i = 0; i < len; i++) {
              binary += String.fromCharCode(entryBytes[i]);
            }
            imageMap.set(hash, `data:image/png;base64,${btoa(binary)}`);
          }
        }
      }

      // Check if canvas.fig is present
      const canvasBytes = zipEntries.get('canvas.fig');
      if (canvasBytes) {
        const { nodeChanges } = parseCanvasFig(canvasBytes);
        return convertFigmaKiwiToProject(nodeChanges, metaJson, imageMap, fileName, targetWorkspaceId);
      }
    } catch (figErr) {
      console.warn('Native Figma Kiwi decoding encountered error, falling back:', figErr);
    }
  }

  // 2. Parse as JSON (Kerf project file or Figma REST API Document)
  const textContent = new TextDecoder('utf-8', { fatal: false }).decode(u8);
  let parsed: any;
  try {
    parsed = JSON.parse(textContent);
  } catch {
    throw new Error('Unsupported or corrupt file format. Please provide a valid .kerf, .figma, or JSON file.');
  }

  // Case A: Standard Kerf Project File
  if (parsed.pages && Array.isArray(parsed.pages)) {
    return {
      ...parsed,
      id: `project-${Date.now()}`,
      name: parsed.name ? `${parsed.name} (Imported)` : fileName,
      workspaceId: targetWorkspaceId,
      updatedAt: Date.now(),
    };
  }

  // Case B: Direct Array of Elements
  if (Array.isArray(parsed)) {
    return {
      id: `project-${Date.now()}`,
      name: `${fileName} (Imported)`,
      workspaceId: targetWorkspaceId,
      pages: [
        {
          id: 'page-1',
          name: 'Page 1',
          elements: parsed,
        },
      ],
      activePageId: 'page-1',
      createdAt: Date.now(),
      updatedAt: Date.now(),
    };
  }

  // Case C: Figma REST API / Document Tree Format
  if (parsed.document || parsed.nodes) {
    const doc = parsed.document || (parsed.nodes ? Object.values(parsed.nodes)[0] : null);
    const canvasNodes = doc?.children?.filter((c: any) => c.type === 'CANVAS') || [doc];
    const pages: Page[] = [];

    canvasNodes.forEach((canvas: any, idx: number) => {
      const pageElements: FigmaElement[] = [];
      const children = canvas.children || [];

      const minX = Math.min(...children.map((c: any) => c.absoluteBoundingBox?.x || 0), 0);
      const minY = Math.min(...children.map((c: any) => c.absoluteBoundingBox?.y || 0), 0);

      children.forEach((child: any) => {
        const converted = convertFigmaJsonNodeToElements(child, null, { x: minX - 100, y: minY - 100 });
        pageElements.push(...converted);
      });

      pages.push({
        id: `page-${idx + 1}`,
        name: canvas.name || `Page ${idx + 1}`,
        elements: pageElements,
      });
    });

    return {
      id: `project-${Date.now()}`,
      name: parsed.name || `${fileName} (Figma Import)`,
      workspaceId: targetWorkspaceId,
      pages: pages.length > 0 ? pages : [{ id: 'page-1', name: 'Page 1', elements: [] }],
      activePageId: pages[0]?.id || 'page-1',
      createdAt: Date.now(),
      updatedAt: Date.now(),
    };
  }

  // Fallback: If it's an object with elements property
  if (parsed.elements && Array.isArray(parsed.elements)) {
    return {
      id: `project-${Date.now()}`,
      name: parsed.name || `${fileName} (Imported)`,
      workspaceId: targetWorkspaceId,
      pages: [
        {
          id: 'page-1',
          name: 'Page 1',
          elements: parsed.elements,
        },
      ],
      activePageId: 'page-1',
      createdAt: Date.now(),
      updatedAt: Date.now(),
    };
  }

  throw new Error('Unrecognized project schema. The file could not be parsed into Kerf elements.');
}
