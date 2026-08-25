import React from 'react';
import { SlicerGridGalleryColumn, SlicerGridGalleryColumnProps } from './SlicerGridGalleryColumn';

export type SlicerVerticalGalleryColumnProps = SlicerGridGalleryColumnProps;

export const SlicerVerticalGalleryColumn: React.FC<SlicerVerticalGalleryColumnProps> = (props) => {
  return <SlicerGridGalleryColumn {...props} />;
};

export { SlicerGridGalleryColumn };
