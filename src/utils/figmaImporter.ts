import type { Project, Page, FigmaElement, ElementType, Effect } from '../types';

/**
 * Converts a Figma color object ({ r: 0-1, g: 0-1, b: 0-1, a?: 0-1 }) to CSS string
 */
function figmaColorToCss(color?: { r: number; g: number; b: number; a?: number }, opacity = 1): string {
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
 * Recursively converts a Figma REST/JSON node tree into Kerf FigmaElements
 */
function convertFigmaNodeToElements(
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

  // Determine fill
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
  } else if (node.type === 'LINE' || node.type === 'VECTOR') {
    fill = 'transparent';
  }

  // Stroke
  const primaryStroke = node.strokes?.find((s: any) => s.visible !== false);
  const stroke = primaryStroke ? figmaColorToCss(primaryStroke.color, primaryStroke.opacity ?? 1) : 'transparent';
  const strokeWidth = node.strokeWeight || 0;
  const strokeStyle = node.strokeDashes?.length ? 'dashed' : 'solid';

  // Effects
  const effects: Effect[] = (node.effects || [])
    .map(convertFigmaEffect)
    .filter((e: any): e is Effect => e !== null);

  // Element Type Mapping
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

  // Text specific attributes
  if (type === 'text') {
    currentElement.text = node.characters || node.name || 'Text';
    currentElement.fontSize = node.style?.fontSize || 16;
    currentElement.fontFamily = node.style?.fontFamily || 'Inter';
    currentElement.fontWeight = node.style?.fontWeight || 400;
    currentElement.textAlign = (node.style?.textAlignHorizontal?.toLowerCase() as any) || 'left';
    currentElement.lineHeight = node.style?.lineHeightPx || Math.round((currentElement.fontSize || 16) * 1.3);
  }

  results.push(currentElement);

  // Recursively process children
  if (Array.isArray(node.children) && node.children.length > 0) {
    for (const child of node.children) {
      const childElements = convertFigmaNodeToElements(child, elemId, offset);
      results.push(...childElements);
    }
  }

  return results;
}

/**
 * Decompresses raw Deflate chunk using browser native DecompressionStream
 */
async function inflateRaw(compressedData: Uint8Array): Promise<Uint8Array> {
  const ds = new DecompressionStream('deflate-raw');
  const writer = ds.writable.getWriter();
  writer.write(compressedData as any);
  writer.close();
  const reader = ds.readable.getReader();
  const chunks: Uint8Array[] = [];
  while (true) {
    const { done, value } = await reader.read();
    if (done) break;
    if (value) chunks.push(value);
  }
  const total = chunks.reduce((acc, c) => acc + c.length, 0);
  const result = new Uint8Array(total);
  let off = 0;
  for (const c of chunks) {
    result.set(c, off);
    off += c.length;
  }
  return result;
}

/**
 * Lightweight ZIP Parser for extracting files from native .fig / .figma archives
 */
interface ZipEntry {
  filename: string;
  data: Uint8Array;
}

async function extractZipEntries(buffer: ArrayBuffer): Promise<ZipEntry[]> {
  const view = new DataView(buffer);
  const u8 = new Uint8Array(buffer);
  const entries: ZipEntry[] = [];
  let offset = 0;

  // Search through Local File Headers (0x04034b50)
  while (offset < buffer.byteLength - 30) {
    const sig = view.getUint32(offset, true);
    if (sig !== 0x04034b50) {
      offset++;
      continue;
    }

    const compressionMethod = view.getUint16(offset + 8, true);
    const compressedSize = view.getUint32(offset + 18, true);
    const fileNameLen = view.getUint16(offset + 26, true);
    const extraFieldLen = view.getUint16(offset + 28, true);

    const nameStart = offset + 30;
    const nameBytes = u8.subarray(nameStart, nameStart + fileNameLen);
    const filename = new TextDecoder().decode(nameBytes);

    const dataStart = nameStart + fileNameLen + extraFieldLen;
    const compressedData = u8.subarray(dataStart, dataStart + compressedSize);

    try {
      if (compressionMethod === 0) {
        // Stored / uncompressed
        entries.push({ filename, data: compressedData });
      } else if (compressionMethod === 8) {
        // Deflated
        const decompressed = await inflateRaw(compressedData);
        entries.push({ filename, data: decompressed });
      }
    } catch (e) {
      console.warn(`Could not decompress zip entry ${filename}:`, e);
    }

    offset = dataStart + compressedSize;
  }

  return entries;
}

/**
 * Scans binary for printable UTF-8 strings to reconstruct layer names and texts
 */
function extractStringsFromBinary(u8: Uint8Array, minLen = 3): string[] {
  const strings: string[] = [];
  let cur: number[] = [];

  for (let i = 0; i < u8.length; i++) {
    const b = u8[i];
    // Printable ASCII characters
    if (b >= 32 && b <= 126) {
      cur.push(b);
    } else {
      if (cur.length >= minLen) {
        const str = String.fromCharCode(...cur).trim();
        if (str && !strings.includes(str) && !str.startsWith('http') && str.length < 100) {
          strings.push(str);
        }
      }
      cur = [];
    }
  }
  return strings;
}

/**
 * Primary universal import function for .kerf, .figma, and .json files
 */
export async function parseAndImportDesignFile(
  file: File,
  targetWorkspaceId = 'ws-personal'
): Promise<Project> {
  const fileName = file.name.replace(/\.(kerf|figma|fig|json)$/i, '');
  const arrayBuffer = await file.arrayBuffer();
  const u8 = new Uint8Array(arrayBuffer);

  // 1. Check if it's a native .fig / .figma ZIP archive (PK\x03\x04 signature)
  if (u8.length >= 4 && u8[0] === 0x50 && u8[1] === 0x4b && u8[2] === 0x03 && u8[3] === 0x04) {
    try {
      const zipEntries = await extractZipEntries(arrayBuffer);
      let projectName = fileName;
      let thumbnailDataUrl: string | null = null;
      let metaJson: any = null;

      for (const entry of zipEntries) {
        if (entry.filename.toLowerCase().includes('thumbnail.png')) {
          let binary = '';
          const len = entry.data.byteLength;
          for (let i = 0; i < len; i++) {
            binary += String.fromCharCode(entry.data[i]);
          }
          thumbnailDataUrl = `data:image/png;base64,${btoa(binary)}`;
        } else if (entry.filename.toLowerCase().includes('meta.json')) {
          try {
            const text = new TextDecoder().decode(entry.data);
            metaJson = JSON.parse(text);
            if (metaJson.client_meta?.name || metaJson.name) {
              projectName = metaJson.client_meta?.name || metaJson.name;
            }
          } catch {
            // ignore meta parse error
          }
        }
      }

      // Reconstruct elements from native Figma archive
      const elements: FigmaElement[] = [];

      // Main Canvas Frame container
      const frameWidth = 1200;
      const frameHeight = 840;
      const mainFrameId = `frame-imported-${Date.now()}`;

      elements.push({
        id: mainFrameId,
        name: `${projectName} - Figma Canvas`,
        type: 'frame',
        x: 80,
        y: 80,
        width: frameWidth,
        height: frameHeight,
        rotation: 0,
        opacity: 1,
        locked: false,
        visible: true,
        fill: '#1E1E22',
        stroke: '#2F2F34',
        strokeWidth: 1,
        strokeStyle: 'solid',
        cornerRadius: 12,
        effects: [
          {
            id: `eff-${Date.now()}`,
            type: 'drop-shadow',
            x: 0,
            y: 8,
            blur: 24,
            spread: 0,
            color: 'rgba(0,0,0,0.4)',
            enabled: true,
          },
        ],
        isFrame: true,
      });

      // If thumbnail preview is present, embed it as the visual canvas presentation
      if (thumbnailDataUrl) {
        elements.push({
          id: `img-preview-${Date.now()}`,
          name: 'Figma Visual Render',
          type: 'image',
          src: thumbnailDataUrl,
          x: 100,
          y: 100,
          width: frameWidth - 40,
          height: frameHeight - 40,
          rotation: 0,
          opacity: 1,
          locked: false,
          visible: true,
          fill: 'transparent',
          stroke: 'transparent',
          strokeWidth: 0,
          strokeStyle: 'solid',
          cornerRadius: 8,
          effects: [],
          parentId: mainFrameId,
        });
      } else {
        // Extract discovered text blocks from the binary schema
        const canvasEntry = zipEntries.find((e) => e.filename.toLowerCase().includes('canvas.fig'));
        const textSnippets = canvasEntry ? extractStringsFromBinary(canvasEntry.data) : [];
        const topTexts = textSnippets.filter((t) => t.length > 3 && !t.includes('fig-kiwi')).slice(0, 10);

        elements.push({
          id: `txt-header-${Date.now()}`,
          name: 'Figma Import Title',
          type: 'text',
          text: projectName,
          fontSize: 28,
          fontFamily: 'Inter',
          fontWeight: 700,
          x: 120,
          y: 120,
          width: 400,
          height: 40,
          rotation: 0,
          opacity: 1,
          locked: false,
          visible: true,
          fill: '#FFFFFF',
          stroke: 'transparent',
          strokeWidth: 0,
          strokeStyle: 'solid',
          cornerRadius: 0,
          effects: [],
          parentId: mainFrameId,
        });

        topTexts.forEach((snippet, i) => {
          elements.push({
            id: `txt-snip-${i}-${Date.now()}`,
            name: `Layer: ${snippet.slice(0, 20)}`,
            type: 'text',
            text: snippet,
            fontSize: 14,
            fontFamily: 'Inter',
            fontWeight: 400,
            x: 120,
            y: 180 + i * 36,
            width: 500,
            height: 24,
            rotation: 0,
            opacity: 0.9,
            locked: false,
            visible: true,
            fill: '#E0E0E0',
            stroke: 'transparent',
            strokeWidth: 0,
            strokeStyle: 'solid',
            cornerRadius: 0,
            effects: [],
            parentId: mainFrameId,
          });
        });
      }

      return {
        id: `project-${Date.now()}`,
        name: projectName,
        workspaceId: targetWorkspaceId,
        pages: [
          {
            id: 'page-1',
            name: 'Page 1',
            elements,
          },
        ],
        activePageId: 'page-1',
        createdAt: Date.now(),
        updatedAt: Date.now(),
      };
    } catch (err) {
      console.warn('Native .figma ZIP unpack failed, falling back to text parse:', err);
    }
  }

  // 2. Parse as JSON (Kerf project file or Figma REST API Document)
  const textContent = new TextDecoder('utf-8', { fatal: false }).decode(u8);
  let parsed: any;
  try {
    parsed = JSON.parse(textContent);
  } catch (e) {
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

      // Calculate origin offset to center design on canvas
      const minX = Math.min(...children.map((c: any) => c.absoluteBoundingBox?.x || 0), 0);
      const minY = Math.min(...children.map((c: any) => c.absoluteBoundingBox?.y || 0), 0);

      children.forEach((child: any) => {
        const converted = convertFigmaNodeToElements(child, null, { x: minX - 100, y: minY - 100 });
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
