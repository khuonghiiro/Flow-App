import React, { useRef, useState } from 'react';
import * as THREE from 'three';
import { GLTFExporter } from 'three/examples/jsm/exporters/GLTFExporter.js';
import {
  Wrench,
  RotateCcw,
  Sparkles,
  CheckCircle,
  Eye,
  EyeOff,
  Play,
  Pause,
  Layers,
  Upload,
  Download,
  Save,
  FolderCheck,
  Info,
  Check,
  Shield,
  Heart,
  Shirt,
  Sparkle,
} from 'lucide-react';
import { CharacterCategory } from '../CharacterAssetRegistry';
import { CharacterAssembly } from '../../types/scene';
import { ProceduralMotionEngine } from '../../core/actors/ProceduralMotionEngine';

export interface CharacterAutoRigTabProps {
  availableCategories: CharacterCategory[];
  assembly: CharacterAssembly;
  onAssemblyChange?: (updatedAssembly: CharacterAssembly) => void;
  currentPreviewGroupRef?: React.MutableRefObject<THREE.Group | null>;
  modelToRig: string;
  onSelectModelToRig: (path: string) => void;
  isRigged: boolean;
  isRiggingLoading: boolean;
  onRunAutoRig: () => void;
  showJoints: boolean;
  onToggleJoints: () => void;
  activePose: string;
  onSelectPose: (pose: string) => void;
  isPosePlaying: boolean;
  onTogglePosePlay: () => void;
}

export const CharacterAutoRigTab: React.FC<CharacterAutoRigTabProps> = ({
  availableCategories,
  assembly,
  onAssemblyChange,
  currentPreviewGroupRef,
  modelToRig,
  onSelectModelToRig,
  isRigged,
  isRiggingLoading,
  onRunAutoRig,
  showJoints,
  onToggleJoints,
  activePose,
  onSelectPose,
  isPosePlaying,
  onTogglePosePlay,
}) => {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [importedFileName, setImportedFileName] = useState<string>('');
  const [isDragOver, setIsDragOver] = useState(false);
  const [isExporting, setIsExporting] = useState(false);
  const [exportSuccessToast, setExportSuccessToast] = useState<string>('');

  const VALID_3D_EXTS = ['.glb', '.gltf', '.fbx', '.vrm'];

  // Categories metadata mapping for friendly display
  const CATEGORY_META: Record<string, { label: string; icon: string }> = {
    than_co_ban: { label: 'Thân Cơ Bản', icon: '🧍' },
    base_body: { label: 'Thân Cơ Bản', icon: '🧍' },
    kieu_toc: { label: 'Kiểu Tóc', icon: '💇' },
    trang_phuc: { label: 'Trang Phục', icon: '👗' },
    giay_dep: { label: 'Giày Dép', icon: '👞' },
    khuon_mat: { label: 'Khuôn Mặt', icon: '😊' },
    canh: { label: 'Cánh Sau Lưng', icon: '🦅' },
    duoi: { label: 'Đuôi Thú', icon: '🦊' },
    phu_kien: { label: 'Phụ Kiện', icon: '💍' },
    mu_non: { label: 'Mũ Nón', icon: '🎩' },
    kieu_rau: { label: 'Kiểu Râu', icon: '🧔' },
    long_may: { label: 'Lông Mày', icon: '🤨' },
    mat: { label: 'Mắt & Đồng Tử', icon: '👁️' },
    mieng: { label: 'Khuôn Miệng', icon: '👄' },
    mui: { label: 'Dáng Mũi', icon: '👃' },
  };

  // Collect all active parts currently assembled in assembly object
  const activeParts = Object.entries(assembly || {}).filter(
    ([key, value]) => typeof value === 'string' && value.trim() !== '' && key !== 'base_body'
  );

  const importFile = (file: File) => {
    const ext = file.name.toLowerCase().substring(file.name.lastIndexOf('.'));
    if (!VALID_3D_EXTS.includes(ext)) return;
    const objectUrl = `${URL.createObjectURL(file)}#${encodeURIComponent(file.name)}`;
    setImportedFileName(file.name);
    onSelectModelToRig(objectUrl);
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    importFile(file);
    e.target.value = '';
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragOver(false);
    const file = e.dataTransfer.files?.[0];
    if (file) importFile(file);
  };

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(true);
  };

  const handleDragLeave = () => setIsDragOver(false);

  // Export rigged character to .glb file
  const handleExportRiggedGLB = () => {
    if (!currentPreviewGroupRef?.current) return;
    setIsExporting(true);

    try {
      const exporter = new GLTFExporter();
      const modelToExport = currentPreviewGroupRef.current;

      exporter.parse(
        modelToExport,
        (gltf) => {
          const blob = new Blob([gltf as ArrayBuffer], { type: 'model/gltf-binary' });
          const link = document.createElement('a');
          const cleanName = (importedFileName || 'character_assembled').replace(/\.[^/.]+$/, '');
          link.href = URL.createObjectURL(blob);
          link.download = `${cleanName}_rigged.glb`;
          link.click();
          URL.revokeObjectURL(link.href);

          setIsExporting(false);
          setExportSuccessToast(`Đã xuất file ${cleanName}_rigged.glb thành công!`);
          setTimeout(() => setExportSuccessToast(''), 4000);
        },
        (error) => {
          console.error('Lỗi khi xuất GLB:', error);
          setIsExporting(false);
        },
        { binary: true }
      );
    } catch (err) {
      console.error('Export GLB error:', err);
      setIsExporting(false);
    }
  };

  return (
    <div style={{ flex: 1, padding: 18, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 16 }}>
      {/* 1. Header Title */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <span style={{ fontSize: 16, fontWeight: 800, color: '#c084fc', letterSpacing: '0.2px' }}>
              🔧 Auto-Rigging Studio (Gắn Xương Tự Động 3D)
            </span>
            <span
              style={{
                fontSize: 10,
                background: 'rgba(168, 85, 247, 0.15)',
                color: '#c084fc',
                padding: '2px 8px',
                borderRadius: 12,
                fontWeight: 700,
                border: '1px solid rgba(168, 85, 247, 0.3)',
              }}
            >
              Anatomical Skinning
            </span>
          </div>
          <p style={{ margin: '4px 0 0 0', fontSize: 11, color: '#94a3b8' }}>
            Tự động sinh cây xương 17 khớp Humanoid và tính toán trọng số da cho toàn bộ bộ phận nhân vật đã lắp ráp.
          </p>
        </div>

        {isRigged && (
          <button
            onClick={onToggleJoints}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              padding: '6px 12px',
              fontSize: 11,
              fontWeight: 700,
              borderRadius: 8,
              background: showJoints ? 'rgba(56, 189, 248, 0.25)' : 'rgba(255,255,255,0.05)',
              border: showJoints ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.12)',
              color: showJoints ? '#38bdf8' : '#94a3b8',
              cursor: 'pointer',
              transition: 'all 0.2s ease',
            }}
          >
            {showJoints ? <Eye size={14} /> : <EyeOff size={14} />}
            <span>{showJoints ? 'Ẩn Khớp Xương' : 'Hiện Khớp Xương'}</span>
          </button>
        )}
      </div>

      {/* 2. Assembled Items Card (Hiển Thị Các Item Đã Lắp Ráp Ở Tab Lắp Ráp) */}
      <div
        style={{
          background: 'rgba(255,255,255,0.03)',
          padding: 14,
          borderRadius: 10,
          border: '1px solid rgba(255,255,255,0.08)',
          display: 'flex',
          flexDirection: 'column',
          gap: 10,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <Layers size={15} color="#a855f7" />
            <span style={{ fontSize: 12, fontWeight: 700, color: '#f1f5f9' }}>
              Danh Sách Các Item Nhân Vật Đang Lắp Ráp (Assembled Character)
            </span>
          </div>

          <span
            style={{
              fontSize: 10,
              background: 'rgba(168, 85, 247, 0.2)',
              color: '#c084fc',
              padding: '2px 8px',
              borderRadius: 6,
              fontWeight: 700,
            }}
          >
            {activeParts.length} Bộ Phận Đang Kích Hoạt
          </span>
        </div>

        {/* Assembled Items Breakdown Grid */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))', gap: 8 }}>
          {activeParts.map(([key, path]) => {
            const meta = CATEGORY_META[key] || { label: key, icon: '📦' };
            const cleanPath = String(path).split('/').pop() || String(path);

            return (
              <div
                key={key}
                style={{
                  background: 'rgba(15, 23, 42, 0.65)',
                  border: '1px solid rgba(255,255,255,0.08)',
                  borderRadius: 8,
                  padding: '8px 10px',
                  display: 'flex',
                  alignItems: 'center',
                  gap: 8,
                }}
              >
                <span style={{ fontSize: 18 }}>{meta.icon}</span>
                <div style={{ display: 'flex', flexDirection: 'column', minWidth: 0, flex: 1 }}>
                  <span style={{ fontSize: 11, fontWeight: 700, color: '#e2e8f0' }}>{meta.label}</span>
                  <span
                    style={{
                      fontSize: 10,
                      color: '#94a3b8',
                      overflow: 'hidden',
                      textOverflow: 'ellipsis',
                      whiteSpace: 'nowrap',
                    }}
                    title={String(path)}
                  >
                    {cleanPath}
                  </span>
                </div>
                <Check size={13} color="#4ade80" />
              </div>
            );
          })}
        </div>

        {activeParts.length === 0 && (
          <div style={{ fontSize: 11, color: '#94a3b8', fontStyle: 'italic', padding: '6px 0' }}>
            Chưa có bộ phận nào được chọn. Hãy sang tab <strong>Lắp Ráp Modular</strong> để chọn thân cơ bản, áo quần, kiểu tóc.
          </div>
        )}
      </div>

      {/* 3. External 3D Model Import Card */}
      <div
        style={{
          background: 'rgba(255,255,255,0.02)',
          padding: 14,
          borderRadius: 10,
          border: '1px solid rgba(255,255,255,0.06)',
          display: 'flex',
          flexDirection: 'column',
          gap: 10,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <Upload size={14} color="#38bdf8" />
            <span style={{ fontSize: 12, fontWeight: 700, color: '#38bdf8' }}>
              Nhập Mô Hình 3D Ngoài Cần Gắn Xương (.glb, .gltf, .fbx, .vrm)
            </span>
          </div>

          {importedFileName && (
            <span style={{ fontSize: 10, background: 'rgba(34, 197, 94, 0.2)', color: '#4ade80', padding: '2px 8px', borderRadius: 6, fontWeight: 700 }}>
              ✅ Đã nạp: {importedFileName}
            </span>
          )}
        </div>

        {/* Drag & Drop Zone */}
        <div
          onDrop={handleDrop}
          onDragOver={handleDragOver}
          onDragLeave={handleDragLeave}
          onClick={() => fileInputRef.current?.click()}
          style={{
            border: isDragOver ? '2px dashed #38bdf8' : '1px dashed rgba(255,255,255,0.15)',
            background: isDragOver ? 'rgba(56, 189, 248, 0.1)' : 'rgba(0,0,0,0.25)',
            padding: '14px 16px',
            borderRadius: 8,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 10,
            cursor: 'pointer',
            transition: 'all 0.15s ease',
          }}
        >
          <Upload size={18} color="#38bdf8" />
          <div style={{ textAlign: 'center' }}>
            <span style={{ fontSize: 12, fontWeight: 700, color: '#f8fafc', display: 'block' }}>
              {isDragOver ? 'Thả file mô hình vào đây!' : 'Kéo thả file 3D vào đây hoặc Click để chọn file từ máy'}
            </span>
            <span style={{ fontSize: 10, color: '#94a3b8' }}>
              Hỗ trợ đầy đủ định dạng .glb, .gltf, .fbx, .vrm từ Blender, Mixamo, Unreal, Sketchfab
            </span>
          </div>
          <input ref={fileInputRef} type="file" style={{ display: 'none' }} onChange={handleFileChange} />
        </div>
      </div>

      {/* 4. Action Button: Run Auto-Rig */}
      <div style={{ display: 'flex', gap: 10 }}>
        <button
          onClick={onRunAutoRig}
          disabled={isRiggingLoading}
          style={{
            flex: 1,
            padding: '12px 18px',
            background: isRigged
              ? 'linear-gradient(135deg, #059669, #047857)'
              : 'linear-gradient(135deg, #a855f7, #6366f1)',
            color: '#ffffff',
            border: 'none',
            borderRadius: 8,
            fontWeight: 800,
            fontSize: 13,
            cursor: isRiggingLoading ? 'not-allowed' : 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 8,
            boxShadow: isRigged ? '0 4px 14px rgba(5, 150, 105, 0.4)' : '0 4px 14px rgba(168, 85, 247, 0.4)',
            opacity: isRiggingLoading ? 0.7 : 1,
            transition: 'all 0.15s ease',
          }}
        >
          {isRiggingLoading ? (
            <>
              <RotateCcw size={16} className="animate-spin" /> Đang phân tích ma trận khớp & gắn trọng số da...
            </>
          ) : (
            <>
              <Sparkles size={16} /> {isRigged ? '🔄 GẮN LẠI XƯƠNG (RE-RIG BỘ NHÂN VẬT)' : '⚡ THỰC HIỆN AUTO-RIG TOÀN BỘ ITEM ĐÃ LẮP RÁP'}
            </>
          )}
        </button>
      </div>

      {/* 5. Rigged Status, Pose Tester & Save/Export Card */}
      {isRigged && (
        <div
          style={{
            background: 'rgba(34, 197, 94, 0.08)',
            padding: 14,
            borderRadius: 10,
            border: '1px solid rgba(34, 197, 94, 0.25)',
            display: 'flex',
            flexDirection: 'column',
            gap: 12,
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6, color: '#4ade80', fontWeight: 800, fontSize: 13 }}>
              <CheckCircle size={16} />
              <span>Gắn xương thành công (17 Khớp Humanoid + Hệ Thống Vật Lý Thứ Cấp)</span>
            </div>

            {/* Export Rigged Model Button */}
            <button
              onClick={handleExportRiggedGLB}
              disabled={isExporting}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 5,
                padding: '6px 12px',
                fontSize: 11,
                fontWeight: 700,
                borderRadius: 6,
                background: 'linear-gradient(135deg, #0ea5e9, #0284c7)',
                color: '#ffffff',
                border: 'none',
                cursor: isExporting ? 'not-allowed' : 'pointer',
                boxShadow: '0 2px 8px rgba(14, 165, 233, 0.3)',
                transition: 'all 0.15s ease',
              }}
            >
              {isExporting ? <RotateCcw size={13} className="animate-spin" /> : <Download size={13} />}
              <span>{isExporting ? 'Đang xuất GLB...' : 'Xuất File 3D (.glb) Đã Gắn Xương'}</span>
            </button>
          </div>

          {exportSuccessToast && (
            <div style={{ background: 'rgba(34, 197, 94, 0.2)', color: '#4ade80', padding: '6px 12px', borderRadius: 6, fontSize: 11, fontWeight: 700 }}>
              ✅ {exportSuccessToast}
            </div>
          )}

          {/* Quick Pose Tester */}
          <div style={{ marginTop: 2 }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
              <span style={{ fontSize: 12, fontWeight: 700, color: '#e2e8f0' }}>
                Kiểm Thử Cử Động Khớp Xương (Pose Test):
              </span>

              <button
                onClick={onTogglePosePlay}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 6,
                  padding: '4px 10px',
                  borderRadius: 6,
                  background: isPosePlaying ? 'rgba(239, 68, 68, 0.25)' : 'rgba(34, 197, 94, 0.25)',
                  border: isPosePlaying ? '1px solid #ef4444' : '1px solid #22c55e',
                  color: isPosePlaying ? '#f87171' : '#4ade80',
                  fontWeight: 700,
                  fontSize: 11,
                  cursor: 'pointer',
                }}
              >
                {isPosePlaying ? <Pause size={12} /> : <Play size={12} />}
                <span>{isPosePlaying ? 'Tạm Dừng' : 'Chạy Thử Cử Động'}</span>
              </button>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(170px, 1fr))', gap: 6 }}>
              {ProceduralMotionEngine.MOTION_LIBRARY.slice(0, 8).map((pose) => {
                const isSelected = activePose === pose.id;
                return (
                  <button
                    key={pose.id}
                    onClick={() => onSelectPose(pose.id)}
                    style={{
                      padding: '7px 10px',
                      borderRadius: 6,
                      background: isSelected ? 'rgba(168, 85, 247, 0.3)' : 'rgba(255,255,255,0.04)',
                      border: isSelected ? '1px solid #a855f7' : '1px solid rgba(255,255,255,0.08)',
                      color: isSelected ? '#ffffff' : '#cbd5e1',
                      fontSize: 11,
                      fontWeight: isSelected ? 700 : 500,
                      cursor: 'pointer',
                      textAlign: 'left',
                      display: 'flex',
                      alignItems: 'center',
                      gap: 6,
                      transition: 'all 0.15s ease',
                    }}
                  >
                    <span>{pose.icon}</span>
                    <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{pose.label}</span>
                  </button>
                );
              })}
            </div>
          </div>
        </div>
      )}

      {/* 6. Storage Folder Architecture Guide */}
      <div
        style={{
          background: 'rgba(15, 23, 42, 0.7)',
          padding: 14,
          borderRadius: 10,
          border: '1px solid rgba(56, 189, 248, 0.2)',
          display: 'flex',
          flexDirection: 'column',
          gap: 8,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <FolderCheck size={15} color="#38bdf8" />
          <span style={{ fontSize: 12, fontWeight: 700, color: '#38bdf8' }}>
            Quy Chuẩn Thư Mục Lưu Trữ Model & Khung Xương (Asset Storage Architecture)
          </span>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: 8, fontSize: 11, color: '#cbd5e1' }}>
          <div style={{ background: 'rgba(0,0,0,0.3)', padding: 10, borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
            <strong style={{ color: '#4ade80', display: 'block', marginBottom: 3 }}>
              📁 assets/nhan_vat/_da_gan_xuong/
            </strong>
            <span>
              Thư mục lưu trữ các file <code>.glb</code> đã hoàn tất Auto-Rig gắn xương và các mô hình 3D nhập ngoài có sẵn xương (như Columbina, VRM, Mixamo).
            </span>
          </div>

          <div style={{ background: 'rgba(0,0,0,0.3)', padding: 10, borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
            <strong style={{ color: '#c084fc', display: 'block', marginBottom: 3 }}>
              📁 assets/nhan_vat/_lap_rap/
            </strong>
            <span>
              Thư mục lưu trữ cấu hình Preset lắp ráp trang phục modular (các file cấu hình nhân vật tùy biến từ tab Lắp Ráp).
            </span>
          </div>
        </div>
      </div>
    </div>
  );
};
