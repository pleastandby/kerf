import type { FigmaElement } from '../types';

export function generateCSS(element: FigmaElement): string {
  const lines: string[] = [];

  lines.push(`/* ${element.name} (${element.type}) */`);
  lines.push(`.element-${element.id} {`);
  lines.push(`  width: ${Math.round(element.width)}px;`);
  lines.push(`  height: ${Math.round(element.height)}px;`);

  if (element.isAutoLayout) {
    if (element.layoutMode === 'grid') {
      lines.push('  display: grid;');
      lines.push(`  grid-template-columns: repeat(${element.gridColumns || 2}, 1fr);`);
    } else {
      lines.push('  display: flex;');
      lines.push(`  flex-direction: ${element.layoutMode === 'vertical' ? 'column' : 'row'};`);
      if (element.layoutMode === 'wrap') {
        lines.push('  flex-wrap: wrap;');
      }
    }
    lines.push(`  gap: ${element.itemSpacing ?? 12}px;`);
    lines.push(`  padding: ${element.paddingTop ?? 16}px ${element.paddingRight ?? 16}px ${element.paddingBottom ?? 16}px ${element.paddingLeft ?? 16}px;`);
    const alignItemsMap = { start: 'flex-start', center: 'center', end: 'flex-end', baseline: 'baseline' };
    const justifyMap = { start: 'flex-start', center: 'center', end: 'flex-end', 'space-between': 'space-between' };
    lines.push(`  align-items: ${alignItemsMap[element.alignItems || 'start']};`);
    lines.push(`  justify-content: ${justifyMap[element.justifyContent || 'start']};`);
  }

  if (element.rotation !== 0) {
    lines.push(`  transform: rotate(${element.rotation}deg);`);
  }

  if (element.opacity < 1) {
    lines.push(`  opacity: ${element.opacity};`);
  }

  if (element.fillType === 'linear-gradient' && element.fillGradient) {
    lines.push(
      `  background: linear-gradient(${element.fillGradient.angle}deg, ${element.fillGradient.start}, ${element.fillGradient.end});`
    );
  } else if (element.fill && element.fill !== 'transparent') {
    if (element.type === 'text') {
      lines.push(`  color: ${element.fill};`);
    } else {
      lines.push(`  background-color: ${element.fill};`);
    }
  }

  if (element.strokeWidth > 0 && element.stroke !== 'transparent') {
    lines.push(`  border: ${element.strokeWidth}px ${element.strokeStyle} ${element.stroke};`);
  }

  if (element.cornerRadius > 0) {
    lines.push(`  border-radius: ${element.cornerRadius}px;`);
  }

  if (element.effects && element.effects.length > 0) {
    const shadowEffects = element.effects.filter(
      (e) => e.enabled && (e.type === 'drop-shadow' || e.type === 'inner-shadow')
    );
    if (shadowEffects.length > 0) {
      const shadowCss = shadowEffects
        .map(
          (e) =>
            `${e.type === 'inner-shadow' ? 'inset ' : ''}${e.x}px ${e.y}px ${e.blur}px ${e.spread}px ${e.color}`
        )
        .join(', ');
      lines.push(`  box-shadow: ${shadowCss};`);
    }

    const blurEffect = element.effects.find((e) => e.enabled && e.type === 'layer-blur');
    if (blurEffect) {
      lines.push(`  filter: blur(${blurEffect.blur}px);`);
    }
  }

  if (element.type === 'text') {
    if (element.fontFamily) lines.push(`  font-family: '${element.fontFamily}', sans-serif;`);
    if (element.fontSize) lines.push(`  font-size: ${element.fontSize}px;`);
    if (element.fontWeight) lines.push(`  font-weight: ${element.fontWeight};`);
    if (element.textAlign) lines.push(`  text-align: ${element.textAlign};`);
    if (element.lineHeight) lines.push(`  line-height: ${element.lineHeight}px;`);
    if (element.letterSpacing) lines.push(`  letter-spacing: ${element.letterSpacing}px;`);
  }

  lines.push('}');
  return lines.join('\n');
}

export function generateTailwind(element: FigmaElement): string {
  const classes: string[] = [];

  classes.push(`w-[${Math.round(element.width)}px]`);
  classes.push(`h-[${Math.round(element.height)}px]`);

  if (element.cornerRadius > 0) {
    classes.push(`rounded-[${element.cornerRadius}px]`);
  }

  if (element.fillType === 'linear-gradient' && element.fillGradient) {
    classes.push(`bg-gradient-to-r from-[${element.fillGradient.start}] to-[${element.fillGradient.end}]`);
  } else if (element.fill && element.fill !== 'transparent') {
    if (element.type === 'text') {
      classes.push(`text-[${element.fill}]`);
    } else {
      classes.push(`bg-[${element.fill}]`);
    }
  }

  if (element.strokeWidth > 0 && element.stroke !== 'transparent') {
    classes.push(`border-[${element.strokeWidth}px] border-[${element.stroke}]`);
  }

  if (element.type === 'text') {
    if (element.fontSize) classes.push(`text-[${element.fontSize}px]`);
    if (element.fontWeight) classes.push(`font-[${element.fontWeight}]`);
    if (element.textAlign) classes.push(`text-${element.textAlign}`);
  }

  if (element.opacity < 1) {
    classes.push(`opacity-${Math.round(element.opacity * 100)}`);
  }

  return `<div className="${classes.join(' ')}">\n  ${element.type === 'text' ? element.text || '' : ''}\n</div>`;
}

export function generateSVGCode(elements: FigmaElement[], width = 800, height = 600): string {
  const svgElements = elements.map((elem) => {
    const rot = elem.rotation ? ` transform="rotate(${elem.rotation} ${elem.x + elem.width / 2} ${elem.y + elem.height / 2})"` : '';
    const op = elem.opacity < 1 ? ` opacity="${elem.opacity}"` : '';

    if (elem.type === 'rect' || elem.type === 'frame') {
      return `  <rect x="${elem.x}" y="${elem.y}" width="${elem.width}" height="${elem.height}" rx="${elem.cornerRadius}" fill="${elem.fill}" stroke="${elem.stroke}" stroke-width="${elem.strokeWidth}"${rot}${op} />`;
    }

    if (elem.type === 'ellipse') {
      return `  <ellipse cx="${elem.x + elem.width / 2}" cy="${elem.y + elem.height / 2}" rx="${elem.width / 2}" ry="${elem.height / 2}" fill="${elem.fill}" stroke="${elem.stroke}" stroke-width="${elem.strokeWidth}"${rot}${op} />`;
    }

    if (elem.type === 'text') {
      return `  <text x="${elem.x}" y="${elem.y + (elem.fontSize || 16)}" font-family="${elem.fontFamily || 'Inter'}" font-size="${elem.fontSize || 16}" font-weight="${elem.fontWeight || 400}" fill="${elem.fill}"${rot}${op}>${elem.text || ''}</text>`;
    }

    if (elem.type === 'line') {
      return `  <line x1="${elem.x}" y1="${elem.y}" x2="${elem.x + elem.width}" y2="${elem.y + elem.height}" stroke="${elem.stroke || elem.fill}" stroke-width="${elem.strokeWidth || 2}"${rot}${op} />`;
    }

    if (elem.type === 'star') {
      return `  <!-- Star (${elem.name}) -->\n  <polygon points="..." fill="${elem.fill}" stroke="${elem.stroke}" stroke-width="${elem.strokeWidth}"${rot}${op} />`;
    }

    if ((elem.type === 'pen' || elem.type === 'pencil') && elem.pathData) {
      return `  <g transform="translate(${elem.x}, ${elem.y})"><path d="${elem.pathData}" fill="${elem.fill === 'none' ? 'none' : elem.fill}" stroke="${elem.stroke || '#0D99FF'}" stroke-width="${elem.strokeWidth || 2}" stroke-linecap="round" stroke-linejoin="round"${rot}${op} /></g>`;
    }

    return '';
  }).filter(Boolean).join('\n');

  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${width} ${height}" width="${width}" height="${height}">\n${svgElements}\n</svg>`;
}
