/**
 * RigAngleSlotWing — Vertical column of angle slots for multi-angle texture assignment.
 * Users drag & drop or upload images for each camera angle (front, 3/4, side, back).
 * Layout modeled after AssemblerEquipWing with 76px×88px slot cards.
 */
import React, { useRef } from 'react';
import {
  Camera,
  Eye,
  RotateCcw,
  ArrowLeft,
  ArrowRight,
  FlipHorizontal,
  Upload,
} from 'lucide-react';
import { Character2DAngle, AngleSlotEntry } from '../../../types/scene2d';

interface AngleSlotUIItem {
  angle: Character2DAngle;
  label: string;
  subLabel: string;
  icon: string;
  degreeLabel: string;
}

const ANGLE_SLOT_ITEMS: AngleSlotUIItem[] = [
  { angle: 'front', label: 'Chính Diện', subLabel: '0° Front', icon: '👁️', degreeLabel: '0°' },
  { angle: 'three_quarter_left', label: '3/4 Trái', subLabel: '45° 3/4', icon: '◣', degreeLabel: '45°' },
  { angle: 'profile_left', label: 'Nghiêng Trái', subLabel: '90° Profile', icon: '◀', degreeLabel: '90°' },
  { angle: 'back', label: 'Sau Lưng', subLabel: '180° Back', icon: '🔙', degreeLabel: '180°' },
  { angle: 'three_quarter_right', label: '3/4 Phải', subLabel: '315° Mirror', icon: '◢', degreeLabel: '315°' },
  { angle: 'profile_right', label: 'Nghiêng Phải', subLabel: '270° Mirror', icon: '▶', degreeLabel: '270°' },
];

interface RigAngleSlotWingProps {
  angleSlots: AngleSlotEntry[];
  selectedAngle: Character2DAngle;
  onSelectAngle: (angle: Character2DAngle) => void;
  onUploadTexture: (angle: Character2DAngle, dataUrl: string) => void;
  onRemoveTexture: (angle: Character2DAngle) => void;
}

export const RigAngleSlotWing: React.FC<RigAngleSlotWingProps> = ({
  angleSlots,
  selectedAngle,
  onSelectAngle,
  onUploadTexture,
  onRemoveTexture,
}) => {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const pendingAngleRef = useRef<Character2DAngle>('front');

  const handleFileUpload = (angle: Character2DAngle) => {
    pendingAngleRef.current = angle;
    fileInputRef.current?.click();
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      if (reader.result) {
        onUploadTexture(pendingAngleRef.current, reader.result as string);
      }
    };
    reader.readAsDataURL(file);
    e.target.value = '';
  };

  return (
    <div
      style={{
        width: 100,
        display: 'flex',
        flexDirection: 'column',
        gap: 5,
        background: 'rgba(8, 13, 26, 0.92)',
        padding: 6,
        borderRadius: 8,
        border: '1px solid rgba(255, 255, 255, 0.08)',
        overflowY: 'auto',
      }}
    >
      {/* Header */}
      <div style={{
        fontSize: 9,
        fontWeight: 700,
        color: '#f59e0b',
        textAlign: 'center',
        letterSpacing: 0.5,
        padding: '2px 0',
      }}>
        📐 GÓC CAMERA
      </div>

      {/* Hidden file input */}
      <input
        ref={fileInputRef}
        type="file"
        accept=".jpg,.jpeg,.png,.webp,.gif,.bmp"
        style={{ display: 'none' }}
        onChange={handleFileChange}
      />

      {/* Angle Slots */}
      {ANGLE_SLOT_ITEMS.map((item) => {
        const slotData = angleSlots.find((s) => s.angle === item.angle);
        const hasTexture = !!slotData?.textureUrl;
        const isMirrored = slotData?.isMirrored || false;
        const isSelected = selectedAngle === item.angle;

        return (
          <div
            key={item.angle}
            onClick={() => onSelectAngle(item.angle)}
            style={{
              height: 72,
              borderRadius: 6,
              background: isSelected
                ? 'rgba(245, 158, 11, 0.2)'
                : hasTexture
                  ? 'rgba(15, 23, 42, 0.85)'
                  : 'rgba(0, 0, 0, 0.45)',
              border: isSelected
                ? '2px solid #f59e0b'
                : hasTexture
                  ? '1px solid rgba(245, 158, 11, 0.35)'
                  : '1px dashed rgba(255, 255, 255, 0.15)',
              boxShadow: isSelected ? '0 0 12px rgba(245, 158, 11, 0.5)' : 'none',
              cursor: 'pointer',
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              justifyContent: 'center',
              padding: 3,
              gap: 2,
              transition: 'all 0.15s ease',
              position: 'relative',
            }}
            title={`${item.label} (${item.subLabel})${isMirrored ? ' — Auto Mirror' : ''}`}
          >
            {/* Thumbnail / Icon */}
            <div
              style={{
                width: 38,
                height: 34,
                borderRadius: 4,
                background: isSelected ? 'rgba(245, 158, 11, 0.12)' : '#040711',
                border: isSelected ? '1px solid #f59e0b' : '1px solid rgba(255,255,255,0.08)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                overflow: 'hidden',
                position: 'relative',
              }}
            >
              {hasTexture ? (
                <img
                  src={slotData!.textureUrl!}
                  alt={item.label}
                  style={{
                    maxWidth: '90%',
                    maxHeight: '90%',
                    objectFit: 'contain',
                    transform: isMirrored ? 'scaleX(-1)' : 'none',
                  }}
                />
              ) : (
                <span style={{ fontSize: 16 }}>{item.icon}</span>
              )}

              {/* Mirror badge */}
              {isMirrored && (
                <span style={{
                  position: 'absolute',
                  bottom: 1,
                  right: 1,
                  fontSize: 8,
                  background: 'rgba(139, 92, 246, 0.8)',
                  color: '#fff',
                  borderRadius: 2,
                  padding: '0 2px',
                  fontWeight: 700,
                }}>
                  🔄
                </span>
              )}
            </div>

            {/* Label */}
            <div style={{
              fontSize: 8.5,
              fontWeight: isSelected ? 700 : 600,
              color: isSelected ? '#f59e0b' : '#f8fafc',
              textAlign: 'center',
              whiteSpace: 'nowrap',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              maxWidth: '100%',
            }}>
              {item.label}
            </div>

            {/* Degree badge */}
            <span style={{
              position: 'absolute',
              top: 2,
              right: 3,
              fontSize: 7,
              fontWeight: 700,
              color: isSelected ? '#f59e0b' : '#64748b',
            }}>
              {item.degreeLabel}
            </span>

            {/* Upload button (show on hover / always if empty) */}
            {!isMirrored && (
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  handleFileUpload(item.angle);
                }}
                style={{
                  position: 'absolute',
                  bottom: 2,
                  left: 3,
                  width: 16,
                  height: 16,
                  borderRadius: 3,
                  background: 'rgba(245, 158, 11, 0.3)',
                  border: '1px solid rgba(245, 158, 11, 0.5)',
                  color: '#f59e0b',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  cursor: 'pointer',
                  padding: 0,
                  fontSize: 8,
                }}
                title="Upload ảnh góc này"
              >
                <Upload size={9} />
              </button>
            )}
          </div>
        );
      })}
    </div>
  );
};
