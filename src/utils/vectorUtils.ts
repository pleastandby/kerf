import type { FigmaElement } from '../types';

/**
 * Shifts SVG path string coordinates by (dx, dy)
 */
export function shiftSvgPath(pathStr: string, dx: number, dy: number): string {
  if (!pathStr) return '';
  return pathStr.replace(/([A-DF-Za-df-z])\s*([^A-DF-Za-df-z]*)/g, (match, cmd, coordsStr) => {
    if (!coordsStr.trim() || cmd.toUpperCase() === 'Z') return match;
    const nums = coordsStr.trim().split(/[\s,]+/).map(Number);
    const shifted: number[] = [];
    for (let i = 0; i < nums.length; i += 2) {
      if (!isNaN(nums[i])) {
        shifted.push(Math.round((nums[i] + dx) * 10) / 10);
        if (!isNaN(nums[i + 1])) {
          shifted.push(Math.round((nums[i + 1] + dy) * 10) / 10);
        }
      }
    }
    return `${cmd} ${shifted.join(' ')} `;
  });
}

/**
 * Ensures pathData is relative to (0, 0) of the element's bounding box (elem.x, elem.y)
 * Handles legacy absolute paths automatically.
 */
export function getRelativePathData(elem: FigmaElement): string {
  if (!elem.pathData) return '';

  // Extract starting M coordinate
  const match = elem.pathData.match(/^M\s*(-?[\d.]+)\s*[, ]\s*(-?[\d.]+)/i);
  if (!match) return elem.pathData;

  const startX = parseFloat(match[1]);
  const startY = parseFloat(match[2]);

  // If path starts at canvas coordinates (around or beyond elem.x, elem.y)
  if (elem.x > 5 && Math.abs(startX - elem.x) < 20 && Math.abs(startY - elem.y) < 20) {
    return shiftSvgPath(elem.pathData, -elem.x, -elem.y);
  }

  // If starting coordinates are far away from (0,0) and close to elem.x
  if (elem.x > 15 && startX >= elem.x - 5) {
    return shiftSvgPath(elem.pathData, -elem.x, -elem.y);
  }

  return elem.pathData;
}
