import type { FigmaElement } from '../types';

export interface AutoLayoutConfig {
  layoutMode: 'horizontal' | 'vertical' | 'wrap' | 'grid';
  itemSpacing: number;
  paddingTop: number;
  paddingRight: number;
  paddingBottom: number;
  paddingLeft: number;
  alignItems: 'start' | 'center' | 'end' | 'baseline';
  justifyContent: 'start' | 'center' | 'end' | 'space-between';
  layoutSizingHorizontal: 'fixed' | 'hug' | 'fill';
  layoutSizingVertical: 'fixed' | 'hug' | 'fill';
  gridColumns?: number;
}

export const DEFAULT_AUTOLAYOUT_CONFIG: AutoLayoutConfig = {
  layoutMode: 'horizontal',
  itemSpacing: 12,
  paddingTop: 16,
  paddingRight: 16,
  paddingBottom: 16,
  paddingLeft: 16,
  alignItems: 'start',
  justifyContent: 'start',
  layoutSizingHorizontal: 'hug',
  layoutSizingVertical: 'hug',
  gridColumns: 2,
};

/**
 * Creates an Auto Layout Frame (Shift+A) containing selected elements
 */
export function createAutoLayoutFrame(
  selectedElements: FigmaElement[],
  allElements: FigmaElement[],
  forcedDirection?: 'horizontal' | 'vertical'
): { updatedElements: FigmaElement[]; newFrameId: string } {
  if (selectedElements.length === 0) {
    return { updatedElements: allElements, newFrameId: '' };
  }

  // If a single frame is selected, turn auto-layout ON for it
  if (selectedElements.length === 1 && (selectedElements[0].type === 'frame' || selectedElements[0].isFrame)) {
    const frame = selectedElements[0];
    const updatedFrame: FigmaElement = {
      ...frame,
      isAutoLayout: true,
      layoutMode: frame.layoutMode || forcedDirection || 'vertical',
      itemSpacing: frame.itemSpacing ?? 12,
      paddingTop: frame.paddingTop ?? 16,
      paddingRight: frame.paddingRight ?? 16,
      paddingBottom: frame.paddingBottom ?? 16,
      paddingLeft: frame.paddingLeft ?? 16,
      alignItems: frame.alignItems || 'start',
      justifyContent: frame.justifyContent || 'start',
      layoutSizingHorizontal: frame.layoutSizingHorizontal || 'hug',
      layoutSizingVertical: frame.layoutSizingVertical || 'hug',
    };

    const newElements = allElements.map((el) => (el.id === frame.id ? updatedFrame : el));
    const recomputed = applyAutoLayoutToFrame(updatedFrame, newElements);
    return { updatedElements: recomputed, newFrameId: frame.id };
  }

  // Calculate bounding box of elements
  const minX = Math.min(...selectedElements.map((e) => e.x));
  const minY = Math.min(...selectedElements.map((e) => e.y));
  const maxX = Math.max(...selectedElements.map((e) => e.x + e.width));
  const maxY = Math.max(...selectedElements.map((e) => e.y + e.height));
  const spanW = maxX - minX;
  const spanH = maxY - minY;

  // Natural direction: horizontal if wider, else vertical
  const direction: 'horizontal' | 'vertical' = forcedDirection || (spanW >= spanH ? 'horizontal' : 'vertical');

  // Sort children by primary axis order
  const sortedChildren = [...selectedElements].sort((a, b) => {
    return direction === 'horizontal' ? a.x - b.x : a.y - b.y;
  });

  const frameId = `frame-autolayout-${Date.now()}`;
  const frameCount = allElements.filter((e) => e.isFrame || e.type === 'frame').length + 1;

  const padT = 16;
  const padR = 16;
  const padB = 16;
  const padL = 16;
  const spacing = 12;

  // Create new Auto Layout frame
  const newFrame: FigmaElement = {
    id: frameId,
    name: `Auto Layout Frame ${frameCount}`,
    type: 'frame',
    isFrame: true,
    isAutoLayout: true,
    layoutMode: direction,
    itemSpacing: spacing,
    paddingTop: padT,
    paddingRight: padR,
    paddingBottom: padB,
    paddingLeft: padL,
    alignItems: 'start',
    justifyContent: 'start',
    layoutSizingHorizontal: 'hug',
    layoutSizingVertical: 'hug',
    x: minX,
    y: minY,
    width: Math.max(80, spanW + padL + padR),
    height: Math.max(60, spanH + padT + padB),
    rotation: 0,
    opacity: 1,
    locked: false,
    visible: true,
    fill: '#24252A',
    stroke: '#3E4048',
    strokeWidth: 1,
    strokeStyle: 'solid',
    cornerRadius: 8,
    clipContent: true,
    effects: [
      {
        id: `eff-${Date.now()}`,
        type: 'drop-shadow',
        x: 0,
        y: 4,
        blur: 14,
        spread: 0,
        color: 'rgba(0,0,0,0.3)',
        enabled: true,
      },
    ],
  };

  // Find index of first selected item to place the frame at appropriate z-index
  const firstIdx = allElements.findIndex((e) => selectedElements.some((s) => s.id === e.id));
  const insertIndex = firstIdx >= 0 ? firstIdx : allElements.length;

  // Update children parentId and keep non-children
  const childIds = new Set(selectedElements.map((e) => e.id));
  const otherElements = allElements.filter((e) => !childIds.has(e.id));

  // Build children with parentId
  const updatedChildren = sortedChildren.map((child) => ({
    ...child,
    parentId: frameId,
  }));

  // Insert frame before children
  const listWithFrame = [...otherElements];
  listWithFrame.splice(insertIndex, 0, newFrame, ...updatedChildren);

  // Apply auto-layout math to correctly position children & hug frame dimensions
  const finalElements = applyAutoLayoutToFrame(newFrame, listWithFrame);

  return {
    updatedElements: finalElements,
    newFrameId: frameId,
  };
}

/**
 * Re-computes child positions and frame dimensions for an Auto Layout Frame
 */
export function applyAutoLayoutToFrame(
  frame: FigmaElement,
  allElements: FigmaElement[]
): FigmaElement[] {
  if (!frame.isAutoLayout) return allElements;

  const mode = frame.layoutMode || 'horizontal';
  const spacing = frame.itemSpacing ?? 12;
  const padT = frame.paddingTop ?? 16;
  const padR = frame.paddingRight ?? 16;
  const padB = frame.paddingBottom ?? 16;
  const padL = frame.paddingLeft ?? 16;
  const align = frame.alignItems || 'start';
  const justify = frame.justifyContent || 'start';
  const sizingH = frame.layoutSizingHorizontal || 'hug';
  const sizingV = frame.layoutSizingVertical || 'hug';

  // Get direct children of this frame, keeping their current order
  const children = allElements.filter((el) => el.parentId === frame.id && el.visible);

  if (children.length === 0) {
    if (sizingH === 'hug' || sizingV === 'hug') {
      const updatedFrame: FigmaElement = {
        ...frame,
        width: sizingH === 'hug' ? padL + padR + 40 : frame.width,
        height: sizingV === 'hug' ? padT + padB + 40 : frame.height,
      };
      return allElements.map((el) => (el.id === frame.id ? updatedFrame : el));
    }
    return allElements;
  }

  const childMap = new Map<string, Partial<FigmaElement>>();

  if (mode === 'horizontal') {
    // HORIZONTAL ROW LAYOUT
    const totalChildrenW = children.reduce((sum, c) => sum + c.width, 0);
    const totalGaps = (children.length - 1) * spacing;
    const innerW = frame.width - padL - padR;

    let computedSpacing = spacing;
    let startX = frame.x + padL;

    if (justify === 'space-between' && children.length > 1 && innerW > totalChildrenW) {
      computedSpacing = (innerW - totalChildrenW) / (children.length - 1);
    } else if (justify === 'center' && innerW > totalChildrenW + totalGaps) {
      startX = frame.x + padL + (innerW - (totalChildrenW + totalGaps)) / 2;
    } else if (justify === 'end' && innerW > totalChildrenW + totalGaps) {
      startX = frame.x + frame.width - padR - (totalChildrenW + totalGaps);
    }

    let currX = startX;
    let maxChildH = 0;

    children.forEach((child) => {
      maxChildH = Math.max(maxChildH, child.height);

      let childY = frame.y + padT;
      const availableH = frame.height - padT - padB;
      if (align === 'center') {
        childY = frame.y + padT + Math.max(0, (availableH - child.height) / 2);
      } else if (align === 'end') {
        childY = frame.y + frame.height - padB - child.height;
      }

      childMap.set(child.id, {
        x: Math.round(currX),
        y: Math.round(childY),
      });

      currX += child.width + computedSpacing;
    });

    const hugW = Math.round(currX - computedSpacing + padR - frame.x);
    const hugH = Math.round(padT + maxChildH + padB);

    const updatedFrame: FigmaElement = {
      ...frame,
      width: sizingH === 'hug' ? Math.max(40, hugW) : frame.width,
      height: sizingV === 'hug' ? Math.max(30, hugH) : frame.height,
    };

    return allElements.map((el) => {
      if (el.id === frame.id) return updatedFrame;
      const childUpdate = childMap.get(el.id);
      return childUpdate ? { ...el, ...childUpdate } : el;
    });
  } else if (mode === 'vertical') {
    // VERTICAL COLUMN LAYOUT
    const totalChildrenH = children.reduce((sum, c) => sum + c.height, 0);
    const totalGaps = (children.length - 1) * spacing;
    const innerH = frame.height - padT - padB;

    let computedSpacing = spacing;
    let startY = frame.y + padT;

    if (justify === 'space-between' && children.length > 1 && innerH > totalChildrenH) {
      computedSpacing = (innerH - totalChildrenH) / (children.length - 1);
    } else if (justify === 'center' && innerH > totalChildrenH + totalGaps) {
      startY = frame.y + padT + (innerH - (totalChildrenH + totalGaps)) / 2;
    } else if (justify === 'end' && innerH > totalChildrenH + totalGaps) {
      startY = frame.y + frame.height - padB - (totalChildrenH + totalGaps);
    }

    let currY = startY;
    let maxChildW = 0;

    children.forEach((child) => {
      maxChildW = Math.max(maxChildW, child.width);

      let childX = frame.x + padL;
      const availableW = frame.width - padL - padR;
      if (align === 'center') {
        childX = frame.x + padL + Math.max(0, (availableW - child.width) / 2);
      } else if (align === 'end') {
        childX = frame.x + frame.width - padR - child.width;
      }

      childMap.set(child.id, {
        x: Math.round(childX),
        y: Math.round(currY),
      });

      currY += child.height + computedSpacing;
    });

    const hugH = Math.round(currY - computedSpacing + padB - frame.y);
    const hugW = Math.round(padL + maxChildW + padR);

    const updatedFrame: FigmaElement = {
      ...frame,
      width: sizingH === 'hug' ? Math.max(40, hugW) : frame.width,
      height: sizingV === 'hug' ? Math.max(30, hugH) : frame.height,
    };

    return allElements.map((el) => {
      if (el.id === frame.id) return updatedFrame;
      const childUpdate = childMap.get(el.id);
      return childUpdate ? { ...el, ...childUpdate } : el;
    });
  } else if (mode === 'wrap') {
    // WRAP FLEX LAYOUT
    let currX = frame.x + padL;
    let currY = frame.y + padT;
    let rowMaxH = 0;
    let maxRowW = 0;
    const maxBoundW = frame.width;

    children.forEach((child) => {
      // Check if item exceeds right boundary
      if (currX + child.width + padR > frame.x + maxBoundW && currX > frame.x + padL) {
        // Wrap to next line
        maxRowW = Math.max(maxRowW, currX - spacing - frame.x + padR);
        currX = frame.x + padL;
        currY += rowMaxH + spacing;
        rowMaxH = 0;
      }

      childMap.set(child.id, {
        x: Math.round(currX),
        y: Math.round(currY),
      });

      rowMaxH = Math.max(rowMaxH, child.height);
      currX += child.width + spacing;
    });

    maxRowW = Math.max(maxRowW, currX - spacing - frame.x + padR);
    const totalH = currY + rowMaxH + padB - frame.y;

    const updatedFrame: FigmaElement = {
      ...frame,
      width: sizingH === 'hug' ? Math.max(60, maxRowW) : frame.width,
      height: sizingV === 'hug' ? Math.max(40, totalH) : frame.height,
    };

    return allElements.map((el) => {
      if (el.id === frame.id) return updatedFrame;
      const childUpdate = childMap.get(el.id);
      return childUpdate ? { ...el, ...childUpdate } : el;
    });
  } else if (mode === 'grid') {
    // GRID MATRIX LAYOUT
    const cols = Math.max(1, frame.gridColumns || 2);
    let rowH = 0;
    let colIdx = 0;
    let rowIdx = 0;

    // Calculate column width
    const availableW = frame.width - padL - padR;
    const colW = (availableW - (cols - 1) * spacing) / cols;

    children.forEach((child, idx) => {
      colIdx = idx % cols;
      rowIdx = Math.floor(idx / cols);

      const cellX = frame.x + padL + colIdx * (colW + spacing);
      const cellY = frame.y + padT + rowIdx * (child.height + spacing);

      childMap.set(child.id, {
        x: Math.round(cellX),
        y: Math.round(cellY),
      });

      rowH = Math.max(rowH, child.height);
    });

    const totalRows = Math.ceil(children.length / cols);
    const totalGridH = padT + totalRows * rowH + (totalRows - 1) * spacing + padB;

    const updatedFrame: FigmaElement = {
      ...frame,
      height: sizingV === 'hug' ? Math.max(40, totalGridH) : frame.height,
    };

    return allElements.map((el) => {
      if (el.id === frame.id) return updatedFrame;
      const childUpdate = childMap.get(el.id);
      return childUpdate ? { ...el, ...childUpdate } : el;
    });
  }

  return allElements;
}

/**
 * Re-orders child in its parent Auto Layout frame (e.g. Move Left/Right or Move Up/Down)
 */
export function reorderAutoLayoutChild(
  childId: string,
  direction: 'prev' | 'next',
  allElements: FigmaElement[]
): FigmaElement[] {
  const child = allElements.find((e) => e.id === childId);
  if (!child || !child.parentId) return allElements;

  const parentFrame = allElements.find((e) => e.id === child.parentId);
  if (!parentFrame || !parentFrame.isAutoLayout) return allElements;

  const siblingIds = allElements
    .filter((e) => e.parentId === parentFrame.id)
    .map((e) => e.id);

  const curIdx = siblingIds.indexOf(childId);
  if (curIdx === -1) return allElements;

  const targetIdx = direction === 'prev' ? curIdx - 1 : curIdx + 1;
  if (targetIdx < 0 || targetIdx >= siblingIds.length) return allElements;

  // Swap elements in allElements array
  const childElem = child;
  const targetElem = allElements.find((e) => e.id === siblingIds[targetIdx]);
  if (!targetElem) return allElements;

  const fromGlobalIdx = allElements.findIndex((e) => e.id === childId);
  const toGlobalIdx = allElements.findIndex((e) => e.id === targetElem.id);

  const updatedList = [...allElements];
  updatedList.splice(fromGlobalIdx, 1);
  updatedList.splice(toGlobalIdx, 0, childElem);

  return applyAutoLayoutToFrame(parentFrame, updatedList);
}

/**
 * Helper to remove Auto Layout from a Frame
 */
export function removeAutoLayout(
  frameId: string,
  allElements: FigmaElement[]
): FigmaElement[] {
  return allElements.map((el) => {
    if (el.id === frameId) {
      return {
        ...el,
        isAutoLayout: false,
      };
    }
    return el;
  });
}
