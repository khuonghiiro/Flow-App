// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// Video Slicer Left Sidebar Controls (BBox Loading & Harmonious Typography)
// =========================================================================================
import React from 'react';
import {
  Upload,
  Crop,
  Layers,
  Sparkles,
  Scissors,
  CheckCircle,
  Loader,
  Pipette,
  Eye,
} from 'lucide-react';
import { VideoMetadata } from '../../../../types/video_slicer';

export interface VideoSlicerSidebarProps {
  videoMetadata: VideoMetadata | null;
  videoFileInputRef: React.RefObject<HTMLInputElement | null>;
  onSelectVideoFile: (file: File) => void;

  // Pipeline All-in-One States
  isPipelineMode: boolean;
  setIsPipelineMode: (v: boolean) => void;
  pipelineIncludeExtract: boolean;
  setPipelineIncludeExtract: (v: boolean) => void;
  pipelineIncludeBBox: boolean;
  setPipelineIncludeBBox: (v: boolean) => void;
  pipelineIncludeChroma: boolean;
  setPipelineIncludeChroma: (v: boolean) => void;
  isPipelineRunning: boolean;
  pipelineStatusText: string;
  onRunAllInOnePipeline: () => void;

  // Logic 1: Range & Extraction
  startTime: number;
  setStartTime: (v: number) => void;
  endTime: number;
  setEndTime: (v: number) => void;
  targetFps: number;
  setTargetFps: (v: number) => void;
  maxFrames: number;
  setMaxFrames: (v: number) => void;
  estimatedTotalFrames: number;
  isExtracting: boolean;
  extractProgress: number;
  extractStatusText: string;
  onApplyExtractOnly: () => void;

  // Frames count
  framesCount: number;

  // Logic 2: Chroma Key Background Peeling
  keyColorType: 'chroma_green' | 'pure_white' | 'custom';
  setKeyColorType: (t: 'chroma_green' | 'pure_white' | 'custom') => void;
  keyColorHex: string;
  setKeyColorHex: (hex: string) => void;
  isolationMode: 'all' | 'outer_only';
  setIsolationMode: (mode: 'all' | 'outer_only') => void;
  tolerance: number;
  setTolerance: (v: number) => void;
  feather: number;
  setFeather: (v: number) => void;
  shadowRetention: number;
  setShadowRetention: (v: number) => void;
  despeckleSize: number;
  setDespeckleSize: (v: number) => void;
  defringeStrength: number;
  setDefringeStrength: (v: number) => void;
  isApplyingAll: boolean;
  isApplyingSingle?: boolean;
  peelStatusText: string;
  onApplyChromaSingleFrame: () => void;
  onApplyChromaOnly: () => void;
  onTriggerEyedropper?: () => void;
  isEyedropperActive?: boolean;

  // Logic 3: BBox Cropping
  isBBoxCropMode: boolean;
  setIsBBoxCropMode: (v: boolean) => void;
  isCroppingBBox?: boolean;
  cropBBoxStatusText?: string;
  onApplyBBoxCropOnly: () => void;
  onAutoTrimAllBBox: () => void;
}

export const VideoSlicerSidebar: React.FC<VideoSlicerSidebarProps> = ({
  videoMetadata,
  videoFileInputRef,
  onSelectVideoFile,
  isPipelineMode,
  setIsPipelineMode,
  pipelineIncludeExtract,
  setPipelineIncludeExtract,
  pipelineIncludeBBox,
  setPipelineIncludeBBox,
  pipelineIncludeChroma,
  setPipelineIncludeChroma,
  isPipelineRunning,
  pipelineStatusText,
  onRunAllInOnePipeline,
  startTime,
  setStartTime,
  endTime,
  setEndTime,
  targetFps,
  setTargetFps,
  estimatedTotalFrames,
  isExtracting,
  extractProgress,
  extractStatusText,
  onApplyExtractOnly,
  framesCount,
  keyColorType,
  setKeyColorType,
  keyColorHex,
  setKeyColorHex,
  isolationMode,
  setIsolationMode,
  tolerance,
  setTolerance,
  feather,
  setFeather,
  shadowRetention,
  setShadowRetention,
  despeckleSize,
  setDespeckleSize,
  defringeStrength,
  setDefringeStrength,
  isApplyingAll,
  isApplyingSingle = false,
  peelStatusText,
  onApplyChromaSingleFrame,
  onApplyChromaOnly,
  onTriggerEyedropper,
  isEyedropperActive = false,
  isBBoxCropMode,
  setIsBBoxCropMode,
  isCroppingBBox = false,
  cropBBoxStatusText = '',
  onApplyBBoxCropOnly,
  onAutoTrimAllBBox,
}) => {
  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 8,
        height: '100%',
        minHeight: 0,
        overflowY: 'auto',
        padding: '0 8px 12px 10px',
        boxSizing: 'border-box',
        fontFamily: "var(--font-main, 'Be Vietnam Pro', 'Inter', system-ui, sans-serif)",
      }}
    >
      {/* ─── UPLOAD VIDEO CARD ─────────────────────────────────── */}
      <div
        style={{
          background: 'rgba(15, 23, 42, 0.7)',
          border: '1px dashed rgba(56, 189, 248, 0.3)',
          borderRadius: 8,
          padding: '10px 12px',
          textAlign: 'center',
          cursor: 'pointer',
          transition: 'all 0.15s ease',
        }}
        onClick={() => videoFileInputRef.current?.click()}
      >
        <input
          ref={videoFileInputRef as any}
          type="file"
          accept="video/*"
          style={{ display: 'none' }}
          onChange={(e) => {
            const f = e.target.files?.[0];
            if (f) onSelectVideoFile(f);
          }}
        />
        <Upload size={18} color="#38bdf8" style={{ margin: '0 auto 4px' }} />
        <div style={{ fontSize: 11, fontWeight: 700, color: '#f8fafc' }}>
          {videoMetadata ? 'Đổi Video Hoạt Ảnh' : 'Tải Lên Video Hoạt Ảnh'}
        </div>
        <div style={{ fontSize: 9.5, color: '#94a3b8', marginTop: 2 }}>
          Hỗ trợ MP4, WEBM, MOV (Tối ưu FFmpeg 8.0.1)
        </div>
      </div>

      {/* ─── MASTER TOGGLE: ALL-IN-ONE PIPELINE ────────────────── */}
      <div
        style={{
          background: isPipelineMode ? 'rgba(56, 189, 248, 0.08)' : 'rgba(15, 23, 42, 0.6)',
          border: `1px solid ${isPipelineMode ? '#0284c7' : 'rgba(255, 255, 255, 0.08)'}`,
          borderRadius: 8,
          padding: 10,
          display: 'flex',
          flexDirection: 'column',
          gap: 6,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 5 }}>
            <Sparkles size={13} />
            ⚡ Xử Lý Đồng Thời 1 Chạm
          </div>
          <input
            type="checkbox"
            checked={isPipelineMode}
            onChange={(e) => setIsPipelineMode(e.target.checked)}
            style={{ cursor: 'pointer', accentColor: '#0284c7', width: 14, height: 14 }}
          />
        </div>

        {isPipelineMode && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6, marginTop: 4 }}>
            <div style={{ display: 'flex', gap: 10, fontSize: 10, color: '#cbd5e1' }}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 4, cursor: 'pointer' }}>
                <input
                  type="checkbox"
                  checked={pipelineIncludeExtract}
                  onChange={(e) => setPipelineIncludeExtract(e.target.checked)}
                  style={{ accentColor: '#38bdf8' }}
                />
                Cắt Video
              </label>
              <label style={{ display: 'flex', alignItems: 'center', gap: 4, cursor: 'pointer' }}>
                <input
                  type="checkbox"
                  checked={pipelineIncludeBBox}
                  onChange={(e) => setPipelineIncludeBBox(e.target.checked)}
                  style={{ accentColor: '#f59e0b' }}
                />
                Cắt BBox
              </label>
              <label style={{ display: 'flex', alignItems: 'center', gap: 4, cursor: 'pointer' }}>
                <input
                  type="checkbox"
                  checked={pipelineIncludeChroma}
                  onChange={(e) => setPipelineIncludeChroma(e.target.checked)}
                  style={{ accentColor: '#10b981' }}
                />
                Bóc Nền
              </label>
            </div>

            <button
              onClick={onRunAllInOnePipeline}
              disabled={!videoMetadata || isPipelineRunning}
              style={{
                background: 'linear-gradient(135deg, #0284c7, #10b981)',
                border: 'none',
                borderRadius: 6,
                color: '#fff',
                padding: '6px 10px',
                fontSize: 10.5,
                fontWeight: 700,
                cursor: videoMetadata && !isPipelineRunning ? 'pointer' : 'not-allowed',
                boxShadow: '0 0 12px rgba(2, 132, 199, 0.4)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 5,
              }}
            >
              {isPipelineRunning ? <Loader size={12} className="spin" /> : <Sparkles size={12} />}
              {isPipelineRunning ? 'Đang Xử Lý Đồng Thời...' : '🚀 ÁP DỤNG ĐỒNG THỜI TẤT CẢ'}
            </button>
            {pipelineStatusText && (
              <div style={{ fontSize: 9.5, color: '#38bdf8', textAlign: 'center' }}>{pipelineStatusText}</div>
            )}
          </div>
        )}
      </div>

      {/* ─── LOGIC 1: CẮT VIDEO & TRÍCH XUẤT FRAME GỐC ─────────── */}
      <div
        style={{
          background: 'rgba(15, 23, 42, 0.6)',
          border: '1px solid rgba(255, 255, 255, 0.08)',
          borderRadius: 8,
          padding: 10,
          display: 'flex',
          flexDirection: 'column',
          gap: 6,
        }}
      >
        <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 5 }}>
          <Scissors size={13} />
          1. Cài Đặt Trích Xuất Frame Gốc
        </div>

        {/* Target FPS Slider */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8' }}>
            <span>Số frame trích xuất / 1s:</span>
            <span style={{ color: '#38bdf8', fontWeight: 700 }}>{targetFps} FPS</span>
          </div>
          <input
            type="range"
            min={1}
            max={30}
            value={targetFps}
            onChange={(e) => setTargetFps(Number(e.target.value))}
            style={{ accentColor: '#0284c7', width: '100%', height: 4 }}
          />
        </div>

        {/* Estimated total frames */}
        <div
          style={{
            background: 'rgba(0, 0, 0, 0.3)',
            borderRadius: 4,
            padding: '5px 8px',
            fontSize: 10,
            color: '#cbd5e1',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
          }}
        >
          <span>Tổng frame dự kiến:</span>
          <span style={{ color: '#38bdf8', fontWeight: 700 }}>
            {estimatedTotalFrames} frames ({((endTime || videoMetadata?.duration || 1) - startTime).toFixed(2)}s)
          </span>
        </div>

        {/* Individual Apply Button for Logic 1 */}
        <button
          onClick={onApplyExtractOnly}
          disabled={!videoMetadata || isExtracting}
          style={{
            background: '#0284c7',
            border: 'none',
            borderRadius: 6,
            color: '#fff',
            padding: '6px 10px',
            fontSize: 10.5,
            fontWeight: 700,
            cursor: videoMetadata && !isExtracting ? 'pointer' : 'not-allowed',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 5,
            marginTop: 2,
          }}
        >
          {isExtracting ? <Loader size={12} className="spin" /> : <Scissors size={12} />}
          {isExtracting ? `Đang trích xuất (${extractProgress}%)...` : '⚡ Áp Dụng: Cắt Video & Trích Xuất Frame'}
        </button>
        {extractStatusText && (
          <div style={{ fontSize: 9.5, color: '#38bdf8', textAlign: 'center' }}>{extractStatusText}</div>
        )}
      </div>

      {/* ─── LOGIC 2: CẮT KHUNG VIDEO BBOX ─────────────────────── */}
      <div
        style={{
          background: 'rgba(15, 23, 42, 0.6)',
          border: '1px solid rgba(255, 255, 255, 0.08)',
          borderRadius: 8,
          padding: 10,
          display: 'flex',
          flexDirection: 'column',
          gap: 6,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ fontSize: 11, fontWeight: 700, color: '#f59e0b', display: 'flex', alignItems: 'center', gap: 5 }}>
            <Crop size={13} />
            2. Cắt Khung Nhân Vật (BBox)
          </div>
          <button
            onClick={() => setIsBBoxCropMode(!isBBoxCropMode)}
            style={{
              background: isBBoxCropMode ? '#f59e0b' : 'rgba(255, 255, 255, 0.08)',
              border: 'none',
              borderRadius: 4,
              color: isBBoxCropMode ? '#000' : '#94a3b8',
              fontSize: 10,
              fontWeight: 700,
              padding: '3px 7px',
              cursor: 'pointer',
            }}
          >
            {isBBoxCropMode ? 'Đang Bật BBox' : 'Bật BBox'}
          </button>
        </div>

        <div style={{ fontSize: 9.5, color: '#94a3b8' }}>
          Kéo 4 góc núm vàng và di chuyển hộp BBox trên khung hình để ôm sát nhân vật cần cắt.
        </div>

        {/* Buttons for Logic 2 */}
        <div style={{ display: 'flex', gap: 6 }}>
          <button
            onClick={onApplyBBoxCropOnly}
            disabled={!isBBoxCropMode || framesCount === 0 || isCroppingBBox}
            title={!isBBoxCropMode ? "Vui lòng bấm 'Bật BBox' trước khi cắt khung" : "Cắt khung theo BBox hiện tại"}
            style={{
              flex: 1,
              background: isBBoxCropMode ? '#d97706' : 'rgba(255, 255, 255, 0.08)',
              border: 'none',
              borderRadius: 6,
              color: isBBoxCropMode ? '#fff' : '#64748b',
              padding: '6px 8px',
              fontSize: 10,
              fontWeight: 700,
              cursor: isBBoxCropMode && framesCount > 0 && !isCroppingBBox ? 'pointer' : 'not-allowed',
              opacity: isBBoxCropMode && framesCount > 0 ? 1 : 0.4,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
            }}
          >
            {isCroppingBBox ? <Loader size={11} className="spin" /> : <Crop size={11} />}
            {isCroppingBBox ? 'Đang cắt BBox...' : '✂️ Áp Dụng: Cắt BBox'}
          </button>

          <button
            onClick={onAutoTrimAllBBox}
            disabled={!isBBoxCropMode || framesCount === 0 || isCroppingBBox}
            title={!isBBoxCropMode ? "Vui lòng bấm 'Bật BBox' trước khi Auto Trim" : "Tự động phát hiện viền trong suốt và cắt gọn"}
            style={{
              background: isBBoxCropMode ? 'rgba(245, 158, 11, 0.2)' : 'rgba(255, 255, 255, 0.05)',
              border: isBBoxCropMode ? '1px solid rgba(245, 158, 11, 0.3)' : '1px solid rgba(255, 255, 255, 0.08)',
              borderRadius: 6,
              color: isBBoxCropMode ? '#fbbf24' : '#64748b',
              padding: '6px 8px',
              fontSize: 10,
              fontWeight: 700,
              cursor: isBBoxCropMode && framesCount > 0 && !isCroppingBBox ? 'pointer' : 'not-allowed',
              opacity: isBBoxCropMode && framesCount > 0 ? 1 : 0.4,
            }}
          >
            Auto Trim
          </button>
        </div>

        {cropBBoxStatusText && (
          <div style={{ fontSize: 9.5, color: '#fbbf24', textAlign: 'center', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 4 }}>
            {isCroppingBBox && <Loader size={10} className="spin" />}
            {cropBBoxStatusText}
          </div>
        )}
      </div>

      {/* ─── LOGIC 3: BÓC NÈN CHROMA KEY (TAB 1 ENGINE) ────────── */}
      <div
        style={{
          background: 'rgba(15, 23, 42, 0.6)',
          border: '1px solid rgba(255, 255, 255, 0.08)',
          borderRadius: 8,
          padding: 10,
          display: 'flex',
          flexDirection: 'column',
          gap: 6,
        }}
      >
        <div style={{ fontSize: 11, fontWeight: 700, color: '#10b981', display: 'flex', alignItems: 'center', gap: 5 }}>
          <Layers size={13} />
          3. Chế Độ Bóc Nền (Chroma Key Tab 1)
        </div>

        {/* Color Preset & Pipette Eyedropper Picker */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <select
            value={keyColorType}
            onChange={(e) => setKeyColorType(e.target.value as any)}
            style={{
              flex: 1,
              background: '#090e1a',
              border: '1px solid rgba(255, 255, 255, 0.15)',
              borderRadius: 4,
              color: '#f8fafc',
              fontSize: 10,
              padding: '4px 6px',
            }}
          >
            <option value="chroma_green">Màn Xanh Lá (Green)</option>
            <option value="pure_white">Màn Trắng (White)</option>
            <option value="custom">Màu Tùy Chỉnh</option>
          </select>

          <input
            type="color"
            value={keyColorHex}
            onChange={(e) => {
              setKeyColorHex(e.target.value);
              setKeyColorType('custom');
            }}
            style={{
              width: 26,
              height: 24,
              border: 'none',
              borderRadius: 4,
              cursor: 'pointer',
              background: 'transparent',
            }}
          />

          {/* Pipette Eyedropper Button */}
          <button
            onClick={onTriggerEyedropper}
            title="Hút màu trực tiếp từ ảnh / video"
            style={{
              background: isEyedropperActive ? '#f59e0b' : 'rgba(255, 255, 255, 0.08)',
              border: `1px solid ${isEyedropperActive ? '#fbbf24' : 'rgba(255, 255, 255, 0.15)'}`,
              borderRadius: 4,
              color: isEyedropperActive ? '#000' : '#f8fafc',
              padding: '4px 7px',
              fontSize: 10,
              fontWeight: 600,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 3,
            }}
          >
            <Pipette size={11} /> Hút Màu
          </button>
        </div>

        {/* Mode Selector */}
        <div style={{ display: 'flex', gap: 4 }}>
          {(['all', 'outer_only'] as ('all' | 'outer_only')[]).map((m) => (
            <button
              key={m}
              onClick={() => setIsolationMode(m)}
              style={{
                flex: 1,
                padding: '4px 6px',
                borderRadius: 4,
                border: 'none',
                background: isolationMode === m ? '#10b981' : 'rgba(255, 255, 255, 0.05)',
                color: isolationMode === m ? '#fff' : '#94a3b8',
                fontSize: 10,
                fontWeight: isolationMode === m ? 700 : 500,
                cursor: 'pointer',
              }}
            >
              {m === 'all' ? 'Tách Toàn Bộ' : 'Chỉ Viền Ngoài'}
            </button>
          ))}
        </div>

        {/* Tolerance Slider */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8' }}>
            <span>Độ nhạy màu (Tolerance):</span>
            <span style={{ color: '#34d399', fontWeight: 700 }}>{tolerance}%</span>
          </div>
          <input
            type="range"
            min={5}
            max={100}
            value={tolerance}
            onChange={(e) => setTolerance(Number(e.target.value))}
            style={{ accentColor: '#10b981', width: '100%', height: 4 }}
          />
        </div>

        {/* Feather Slider */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8' }}>
            <span>Làm mềm viền (Feather):</span>
            <span style={{ color: '#34d399', fontWeight: 700 }}>{feather}px</span>
          </div>
          <input
            type="range"
            min={0}
            max={20}
            value={feather}
            onChange={(e) => setFeather(Number(e.target.value))}
            style={{ accentColor: '#10b981', width: '100%', height: 4 }}
          />
        </div>

        {/* Shadow Retention Slider */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8' }}>
            <span>Giữ bóng đổ (Shadow):</span>
            <span style={{ color: '#34d399', fontWeight: 700 }}>{shadowRetention}%</span>
          </div>
          <input
            type="range"
            min={0}
            max={100}
            value={shadowRetention}
            onChange={(e) => setShadowRetention(Number(e.target.value))}
            style={{ accentColor: '#10b981', width: '100%', height: 4 }}
          />
        </div>

        {/* Despeckle & Defringe Row */}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
            <div style={{ fontSize: 9.5, color: '#94a3b8' }}>Khử rác: {despeckleSize}px</div>
            <input
              type="range"
              min={0}
              max={150}
              value={despeckleSize}
              onChange={(e) => setDespeckleSize(Number(e.target.value))}
              style={{ accentColor: '#10b981', width: '100%', height: 4 }}
            />
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
            <div style={{ fontSize: 9.5, color: '#94a3b8' }}>Khử viền ám: {defringeStrength}%</div>
            <input
              type="range"
              min={0}
              max={100}
              value={defringeStrength}
              onChange={(e) => setDefringeStrength(Number(e.target.value))}
              style={{ accentColor: '#10b981', width: '100%', height: 4 }}
            />
          </div>
        </div>

        {/* 2 SEPARATE APPLY BUTTONS FOR CHROMA PEELING */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 5, marginTop: 4 }}>
          {/* Button 1: Apply to Selected Frame ONLY */}
          <button
            onClick={onApplyChromaSingleFrame}
            disabled={framesCount === 0 || isApplyingSingle}
            style={{
              background: 'rgba(16, 185, 129, 0.2)',
              border: '1px solid rgba(16, 185, 129, 0.4)',
              borderRadius: 6,
              color: '#34d399',
              padding: '6px 8px',
              fontSize: 10,
              fontWeight: 700,
              cursor: framesCount > 0 && !isApplyingSingle ? 'pointer' : 'not-allowed',
              opacity: framesCount > 0 ? 1 : 0.5,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
            }}
          >
            {isApplyingSingle ? <Loader size={11} className="spin" /> : <Eye size={11} />}
            👁️ Áp Dụng: Bóc Nền Frame Này
          </button>

          {/* Button 2: Apply to ALL FRAMES */}
          <button
            onClick={onApplyChromaOnly}
            disabled={framesCount === 0 || isApplyingAll}
            style={{
              background: '#10b981',
              border: 'none',
              borderRadius: 6,
              color: '#fff',
              padding: '6px 10px',
              fontSize: 10.5,
              fontWeight: 700,
              cursor: framesCount > 0 && !isApplyingAll ? 'pointer' : 'not-allowed',
              opacity: framesCount > 0 ? 1 : 0.5,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
            }}
          >
            {isApplyingAll ? <Loader size={12} className="spin" /> : <CheckCircle size={12} />}
            {isApplyingAll ? 'Đang bóc nền...' : '🎨 Áp Dụng: Bóc Nền Toàn Bộ Frame'}
          </button>
        </div>

        {peelStatusText && (
          <div style={{ fontSize: 9.5, color: '#34d399', textAlign: 'center' }}>{peelStatusText}</div>
        )}
      </div>
    </div>
  );
};
