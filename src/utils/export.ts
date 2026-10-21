import type { FigmaElement, Project } from '../types';
import confetti from 'canvas-confetti';
import { getRelativePathData } from './vectorUtils';

export function downloadFile(content: string, fileName: string, contentType: string) {
  const blob = new Blob([content], { type: contentType });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

export function exportProjectJSON(project: Project) {
  const json = JSON.stringify(project, null, 2);
  downloadFile(json, `${project.name.toLowerCase().replace(/\s+/g, '-')}.kerf`, 'application/octet-stream');
  triggerConfetti();
}

export function exportSVG(elements: FigmaElement[], filename = 'design.svg') {
  if (elements.length === 0) return;

  const minX = Math.min(...elements.map((e) => e.x));
  const minY = Math.min(...elements.map((e) => e.y));
  const maxX = Math.max(...elements.map((e) => e.x + e.width));
  const maxY = Math.max(...elements.map((e) => e.y + e.height));

  const width = Math.max(maxX - minX + 40, 100);
  const height = Math.max(maxY - minY + 40, 100);

  const svgContent = `
<svg xmlns="http://www.w3.org/2000/svg" viewBox="${minX - 20} ${minY - 20} ${width} ${height}" width="${width}" height="${height}">
  <defs>
    ${elements
      .filter((e) => e.fillType === 'linear-gradient' && e.fillGradient)
      .map(
        (e) => `
      <linearGradient id="grad-${e.id}" x1="0%" y1="0%" x2="100%" y2="100%">
        <stop offset="0%" stop-color="${e.fillGradient?.start || '#000'}" />
        <stop offset="100%" stop-color="${e.fillGradient?.end || '#fff'}" />
      </linearGradient>`
      )
      .join('\n')}
  </defs>
  ${elements
    .map((elem) => {
      const fillAttr =
        elem.fillType === 'linear-gradient' ? `url(#grad-${elem.id})` : elem.fill;
      const rot = elem.rotation
        ? `transform="rotate(${elem.rotation} ${elem.x + elem.width / 2} ${elem.y + elem.height / 2})"`
        : '';
      const op = elem.opacity < 1 ? `opacity="${elem.opacity}"` : '';

      if (elem.type === 'rect' || elem.type === 'frame') {
        return `<rect x="${elem.x}" y="${elem.y}" width="${elem.width}" height="${elem.height}" rx="${elem.cornerRadius}" fill="${fillAttr}" stroke="${elem.stroke}" stroke-width="${elem.strokeWidth}" ${rot} ${op} />`;
      }
      if (elem.type === 'ellipse') {
        return `<ellipse cx="${elem.x + elem.width / 2}" cy="${elem.y + elem.height / 2}" rx="${elem.width / 2}" ry="${elem.height / 2}" fill="${fillAttr}" stroke="${elem.stroke}" stroke-width="${elem.strokeWidth}" ${rot} ${op} />`;
      }
      if (elem.type === 'text') {
        return `<text x="${elem.x}" y="${elem.y + (elem.fontSize || 16)}" font-family="${elem.fontFamily || 'Inter'}" font-size="${elem.fontSize || 16}" font-weight="${elem.fontWeight || 400}" fill="${elem.fill}" ${rot} ${op}>${elem.text || ''}</text>`;
      }
      if (elem.type === 'line') {
        return `<line x1="${elem.x}" y1="${elem.y}" x2="${elem.x + elem.width}" y2="${elem.y + elem.height}" stroke="${elem.stroke || elem.fill}" stroke-width="${elem.strokeWidth || 2}" ${rot} ${op} />`;
      }
      if ((elem.type === 'pen' || elem.type === 'pencil') && elem.pathData) {
        return `<g transform="translate(${elem.x}, ${elem.y})"><path d="${getRelativePathData(elem)}" fill="${elem.fill === 'none' ? 'none' : elem.fill}" stroke="${elem.stroke || '#0D99FF'}" stroke-width="${elem.strokeWidth || 2}" stroke-linecap="round" stroke-linejoin="round" ${rot} ${op} /></g>`;
      }
      return '';
    })
    .join('\n')}
</svg>`;

  downloadFile(svgContent, filename, 'image/svg+xml');
  triggerConfetti();
}

export function exportRasterImage(
  elements: FigmaElement[],
  format: 'png' | 'jpg' = 'png',
  scale = 2,
  filename = 'design'
) {
  if (elements.length === 0) return;

  const minX = Math.min(...elements.map((e) => e.x));
  const minY = Math.min(...elements.map((e) => e.y));
  const maxX = Math.max(...elements.map((e) => e.x + e.width));
  const maxY = Math.max(...elements.map((e) => e.y + e.height));

  const pad = 20;
  const width = Math.max(maxX - minX + pad * 2, 100);
  const height = Math.max(maxY - minY + pad * 2, 100);

  const canvas = document.createElement('canvas');
  canvas.width = width * scale;
  canvas.height = height * scale;
  const ctx = canvas.getContext('2d');
  if (!ctx) return;

  ctx.scale(scale, scale);
  ctx.translate(-minX + pad, -minY + pad);

  // Background if jpg
  if (format === 'jpg') {
    ctx.fillStyle = '#0F1115';
    ctx.fillRect(minX - pad, minY - pad, width, height);
  }

  elements.forEach((elem) => {
    ctx.save();
    ctx.globalAlpha = elem.opacity;

    // Rotation
    if (elem.rotation !== 0) {
      ctx.translate(elem.x + elem.width / 2, elem.y + elem.height / 2);
      ctx.rotate((elem.rotation * Math.PI) / 180);
      ctx.translate(-(elem.x + elem.width / 2), -(elem.y + elem.height / 2));
    }

    // Shadow
    const shadow = elem.effects?.find((e) => e.enabled && e.type === 'drop-shadow');
    if (shadow) {
      ctx.shadowColor = shadow.color;
      ctx.shadowBlur = shadow.blur;
      ctx.shadowOffsetX = shadow.x;
      ctx.shadowOffsetY = shadow.y;
    }

    // Fill style
    if (elem.fillType === 'linear-gradient' && elem.fillGradient) {
      const grad = ctx.createLinearGradient(elem.x, elem.y, elem.x + elem.width, elem.y + elem.height);
      grad.addColorStop(0, elem.fillGradient.start);
      grad.addColorStop(1, elem.fillGradient.end);
      ctx.fillStyle = grad;
    } else {
      ctx.fillStyle = elem.fill;
    }

    ctx.strokeStyle = elem.stroke;
    ctx.lineWidth = elem.strokeWidth;

    if (elem.type === 'rect' || elem.type === 'frame') {
      const r = Math.min(elem.cornerRadius, elem.width / 2, elem.height / 2);
      ctx.beginPath();
      ctx.roundRect(elem.x, elem.y, elem.width, elem.height, r);
      if (elem.fill && elem.fill !== 'transparent') ctx.fill();
      if (elem.strokeWidth > 0 && elem.stroke !== 'transparent') ctx.stroke();
    } else if (elem.type === 'ellipse') {
      ctx.beginPath();
      ctx.ellipse(
        elem.x + elem.width / 2,
        elem.y + elem.height / 2,
        elem.width / 2,
        elem.height / 2,
        0,
        0,
        Math.PI * 2
      );
      if (elem.fill && elem.fill !== 'transparent') ctx.fill();
      if (elem.strokeWidth > 0 && elem.stroke !== 'transparent') ctx.stroke();
    } else if (elem.type === 'text') {
      ctx.font = `${elem.fontWeight || 400} ${elem.fontSize || 16}px ${elem.fontFamily || 'Inter'}, sans-serif`;
      ctx.textBaseline = 'top';
      ctx.textAlign = (elem.textAlign as CanvasTextAlign) || 'left';
      const textX =
        elem.textAlign === 'center'
          ? elem.x + elem.width / 2
          : elem.textAlign === 'right'
          ? elem.x + elem.width
          : elem.x;
      ctx.fillText(elem.text || '', textX, elem.y);
    } else if (elem.type === 'line') {
      ctx.beginPath();
      ctx.moveTo(elem.x, elem.y);
      ctx.lineTo(elem.x + elem.width, elem.y + elem.height);
      ctx.stroke();
    } else if ((elem.type === 'pen' || elem.type === 'pencil') && elem.pathData) {
      try {
        const relPath = getRelativePathData(elem);
        const p2d = new Path2D(relPath);
        ctx.save();
        ctx.translate(elem.x, elem.y);
        if (elem.fill && elem.fill !== 'transparent' && elem.fill !== 'none') {
          ctx.fill(p2d);
        }
        if (elem.strokeWidth > 0 && elem.stroke !== 'transparent') {
          ctx.stroke(p2d);
        }
        ctx.restore();
      } catch (e) {
        // Fallback for browsers without Path2D
      }
    }

    ctx.restore();
  });

  const mime = format === 'jpg' ? 'image/jpeg' : 'image/png';
  const dataUrl = canvas.toDataURL(mime, 0.95);
  const a = document.createElement('a');
  a.href = dataUrl;
  a.download = `${filename}@${scale}x.${format}`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);

  triggerConfetti();
}

export function triggerConfetti() {
  try {
    confetti({
      particleCount: 75,
      spread: 60,
      origin: { y: 0.7 },
      colors: ['#0D99FF', '#10B981', '#F59E0B', '#EC4899', '#8B5CF6'],
    });
  } catch (e) {
    // Ignore if canvas confetti isn't ready
  }
}
