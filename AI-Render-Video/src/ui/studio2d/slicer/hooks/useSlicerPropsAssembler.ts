// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import React from 'react';
import { SlicerSidebarContainerProps } from '../SlicerSidebarContainer';
import { SlicerCanvasColumnProps } from '../canvas/SlicerCanvasColumn';
import { SlicerVerticalGalleryColumnProps } from '../SlicerVerticalGalleryColumn';

export interface UseSlicerPropsAssemblerInput {
  // Sidebar inputs
  sidebarProps: SlicerSidebarContainerProps;
  // Canvas inputs
  canvasProps: SlicerCanvasColumnProps;
  // Gallery inputs
  galleryProps?: SlicerVerticalGalleryColumnProps;
}

export function useSlicerPropsAssembler(input: UseSlicerPropsAssemblerInput) {
  return {
    sidebarProps: input.sidebarProps,
    canvasProps: input.canvasProps,
    galleryProps: input.galleryProps,
  };
}
