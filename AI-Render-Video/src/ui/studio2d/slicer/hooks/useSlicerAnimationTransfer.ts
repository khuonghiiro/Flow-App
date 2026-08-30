// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import { useCallback } from 'react';
import { GridCategoryDefinition } from '../../../../core/assets/GridSliceRegistry';
import { ChromaProcessOptions } from '../../../../core/utils/ChromaDespeckleProcessor';
import { SlicerUploadedImageItem } from './useSlicerMultiImageGallery';
import { sliceMultipleImagesSequentially, sliceSingleImageWithGrid } from '../utils/multiImageGridSliceHelper';

export interface UseSlicerAnimationTransferProps {
  imageList: SlicerUploadedImageItem[];
  checkedImageIds: Set<string>;
  activeImageId: string | null;
  currentCategory: GridCategoryDefinition;
  colDividers: number[];
  rowDividers: number[];
  paddingInset: number;
  chromaOptions: ChromaProcessOptions;
  onTransferToAnimationSlicer?: (data: { frames?: string[]; spriteSheetUrl?: string }) => void;
}

export function useSlicerAnimationTransfer({
  imageList,
  checkedImageIds,
  activeImageId,
  currentCategory,
  colDividers,
  rowDividers,
  paddingInset,
  chromaOptions,
  onTransferToAnimationSlicer,
}: UseSlicerAnimationTransferProps) {
  const handleTransferToAnimationSlicer = useCallback(async () => {
    if (!onTransferToAnimationSlicer) return;

    const checkedItems = imageList.filter((it) => checkedImageIds.has(it.id));
    const itemsToProcess =
      checkedItems.length > 0
        ? checkedItems
        : [imageList.find((it) => it.id === activeImageId) || imageList[0]].filter(Boolean);

    if (itemsToProcess.length === 0) return;

    const allAreSeparatedFrames = itemsToProcess.every(
      (it) => it.isFrameItem || it.isTransparentSeparated
    );

    if (allAreSeparatedFrames) {
      const directFrames = itemsToProcess
        .map((it) => it.transparentUrl || it.url)
        .filter(Boolean);
      if (directFrames.length > 0) {
        onTransferToAnimationSlicer({ frames: directFrames });
        return;
      }
    }

    if (itemsToProcess.length > 1) {
      const multiFrames = await sliceMultipleImagesSequentially(
        imageList,
        new Set(itemsToProcess.map((it) => it.id)),
        currentCategory,
        colDividers,
        rowDividers,
        paddingInset,
        chromaOptions
      );
      if (multiFrames.length > 0) {
        onTransferToAnimationSlicer({ frames: multiFrames });
        return;
      }
    }

    const singleItem = itemsToProcess[0];
    const sourceUrl = singleItem.originalUrl || singleItem.url;
    if (sourceUrl) {
      const singleFrames = await sliceSingleImageWithGrid(
        sourceUrl,
        currentCategory,
        singleItem.customColDividers || colDividers,
        singleItem.customRowDividers || rowDividers,
        paddingInset,
        chromaOptions
      );
      if (singleFrames.length > 0) {
        onTransferToAnimationSlicer({
          frames: singleFrames,
          spriteSheetUrl: singleItem.transparentUrl || singleItem.url,
        });
        return;
      }
    }

    onTransferToAnimationSlicer({
      spriteSheetUrl: singleItem.transparentUrl || singleItem.url,
    });
  }, [
    onTransferToAnimationSlicer,
    imageList,
    checkedImageIds,
    activeImageId,
    currentCategory,
    colDividers,
    rowDividers,
    paddingInset,
    chromaOptions,
  ]);

  return { handleTransferToAnimationSlicer };
}
