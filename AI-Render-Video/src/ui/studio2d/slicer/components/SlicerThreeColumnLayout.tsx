// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import React from 'react';
import { SlicerSidebarContainer, SlicerSidebarContainerProps } from '../SlicerSidebarContainer';
import { SlicerCanvasColumn, SlicerCanvasColumnProps } from '../canvas/SlicerCanvasColumn';
import { SlicerVerticalGalleryColumn, SlicerVerticalGalleryColumnProps } from '../SlicerVerticalGalleryColumn';

export interface SlicerThreeColumnLayoutProps {
  sidebarProps: SlicerSidebarContainerProps;
  canvasProps: SlicerCanvasColumnProps;
  galleryProps?: SlicerVerticalGalleryColumnProps;
  showGallery: boolean;
}

export const SlicerThreeColumnLayout: React.FC<SlicerThreeColumnLayoutProps> = ({
  sidebarProps,
  canvasProps,
  galleryProps,
  showGallery,
}) => {
  return (
    <div
      style={{
        flex: '1 1 0%',
        height: '100%',
        maxHeight: '100%',
        display: 'grid',
        gridTemplateColumns: showGallery
          ? 'minmax(280px, 320px) minmax(0, 1fr) minmax(300px, 340px)'
          : 'minmax(280px, 320px) minmax(0, 1fr)',
        gridTemplateRows: '100%',
        gap: 10,
        minHeight: 0,
        overflow: 'hidden',
        boxSizing: 'border-box',
      }}
    >
      {/* Column 1: Sidebar Controls */}
      <SlicerSidebarContainer {...sidebarProps} />

      {/* Column 2: Interactive Canvas */}
      <SlicerCanvasColumn {...canvasProps} />

      {/* Column 3: Vertical Gallery Column */}
      {showGallery && galleryProps && (
        <SlicerVerticalGalleryColumn {...galleryProps} />
      )}
    </div>
  );
};
