import React from 'react';
import { ImageToSvgVectorizerTabProps } from './types';
import { useImageVectorizer } from './useImageVectorizer';
import { VectorizerTopToolbar } from './VectorizerTopToolbar';
import { VectorizerSettingsPanel } from './VectorizerSettingsPanel';
import { VectorizerComparisonViewport } from './VectorizerComparisonViewport';

export const ImageToSvgVectorizerTab: React.FC<ImageToSvgVectorizerTabProps> = ({
  onTransferToRigAssembler,
  onTransferToGridSlicer,
}) => {
  const {
    sourceImageUrl,
    svgOutput,
    svgDataUrl,
    isConverting,
    errorMsg,
    copied,
    preset,
    applyPreset,
    params,
    updateParam,
    viewMode,
    setViewMode,
    splitPos,
    setSplitPos,
    zoomLevel,
    setZoomLevel,
    metaStats,
    fileInputRef,
    handleVectorize,
    handleFileUpload,
    handleSelectSample,
    handleDownloadSvg,
    handleCopySvgCode,
  } = useImageVectorizer();

  return (
    <div
      style={{
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        background: '#070b14',
        color: '#f8fafc',
        overflow: 'hidden',
      }}
    >
      {/* Top Toolbar */}
      <VectorizerTopToolbar
        preset={preset}
        onSelectPreset={applyPreset}
        isConverting={isConverting}
        onVectorize={handleVectorize}
        svgOutput={svgOutput}
        onDownloadSvg={handleDownloadSvg}
        onCopySvgCode={handleCopySvgCode}
        copied={copied}
      />

      {/* Main Workspace (Sidebar + Dual Viewport) */}
      <div style={{ flex: 1, display: 'flex', overflow: 'hidden' }}>
        <VectorizerSettingsPanel
          sourceImageUrl={sourceImageUrl}
          params={params}
          onUpdateParam={updateParam}
          onFileUpload={handleFileUpload}
          onSelectSample={handleSelectSample}
          fileInputRef={fileInputRef}
          svgDataUrl={svgDataUrl}
          onTransferToRigAssembler={onTransferToRigAssembler}
          onTransferToGridSlicer={onTransferToGridSlicer}
        />

        <VectorizerComparisonViewport
          sourceImageUrl={sourceImageUrl}
          svgDataUrl={svgDataUrl}
          isConverting={isConverting}
          errorMsg={errorMsg}
          viewMode={viewMode}
          onSetViewMode={setViewMode}
          splitPos={splitPos}
          onSetSplitPos={setSplitPos}
          zoomLevel={zoomLevel}
          onSetZoomLevel={setZoomLevel}
          metaStats={metaStats}
        />
      </div>
    </div>
  );
};

export default ImageToSvgVectorizerTab;
