import React from 'react';
import { 
  Move, RotateCcw, Maximize2, Trash2, Copy, Eye, ArrowDown, 
  Layers, User, Box, Shield, Compass, Sparkles, Sliders, ChevronDown
} from 'lucide-react';
import { MasterSceneConfig } from '../types/scene';
import { PlacedProp } from '../types/map_preset';

export interface SelectedSceneObject {
  id: string;
  name: string;
  category: 'actor' | 'prop' | 'camera';
  position: [number, number, number];
  rotation: [number, number, number]; // in degrees
  scale: number;
  isObstacle?: boolean;
  obstacleRadius?: number;
  socketType?: 'none' | 'sit' | 'climb';
}

interface TransformInspectorProps {
  scene: MasterSceneConfig;
  selectedObject: SelectedSceneObject | null;
  onSelectObject: (obj: SelectedSceneObject | null) => void;
  onUpdateTransform: (updated: SelectedSceneObject) => void;
  onDeleteProp?: (propId: string) => void;
  onDuplicateProp?: (prop: PlacedProp) => void;
  onFocusObject?: (position: [number, number, number]) => void;
}

interface TransformAxisSliderProps {
  label: string;
  badgeClass: 'axis-x' | 'axis-y' | 'axis-z' | 'axis-scale';
  value: number;
  min: number;
  max: number;
  step: number;
  unit?: string;
  onChange: (newVal: number) => void;
  onNudge?: (delta: number) => void;
}

const TransformAxisSlider: React.FC<TransformAxisSliderProps> = ({
  label,
  badgeClass,
  value,
  min,
  max,
  step,
  unit = '',
  onChange,
  onNudge,
}) => {
  return (
    <div className={`unity-slider-row ${badgeClass}`}>
      <span className="slider-axis-badge">{label}</span>

      {/* Range Slider for smooth dragging */}
      <input
        type="range"
        className="axis-range-slider"
        min={min}
        max={max}
        step={step}
        value={value}
        onChange={(e) => onChange(parseFloat(e.target.value) || 0)}
      />

      {/* Editable Number Input Box */}
      <div className="slider-number-input">
        <input
          type="number"
          step={step}
          value={value}
          onChange={(e) => onChange(parseFloat(e.target.value) || 0)}
        />
        {unit && <span className="unit-label">{unit}</span>}
      </div>

      {/* Nudge Buttons */}
      {onNudge && (
        <div className="slider-nudge-group">
          <button title="Tăng giá trị" onClick={() => onNudge(step * 2)}>
            ▲
          </button>
          <button title="Giảm giá trị" onClick={() => onNudge(-step * 2)}>
            ▼
          </button>
        </div>
      )}
    </div>
  );
};

export const TransformInspector: React.FC<TransformInspectorProps> = ({
  scene,
  selectedObject,
  onSelectObject,
  onUpdateTransform,
  onDeleteProp,
  onDuplicateProp,
  onFocusObject,
}) => {
  // Collect all selectable objects in the scene
  const allObjects: SelectedSceneObject[] = React.useMemo(() => {
    const list: SelectedSceneObject[] = [];

    // 1. Actors
    (scene.actors || []).forEach((actor) => {
      list.push({
        id: actor.id,
        name: `👤 ${actor.name || actor.id}`,
        category: 'actor',
        position: [actor.spawn_point[0], actor.spawn_point[1], actor.spawn_point[2]],
        rotation: [0, Math.round(((actor.rotation_y || 0) * 180) / Math.PI), 0],
        scale: 1.0,
      });
    });

    // 2. Placed Props
    (scene.environment.placed_props || []).forEach((prop) => {
      const rot = prop.rotation || [0, 0, 0];
      list.push({
        id: prop.id,
        name: `📦 ${prop.id.replace('placed_', '').replace('prop_', '')}`,
        category: 'prop',
        position: [prop.position[0], prop.position[1], prop.position[2]],
        rotation: [
          Math.round((rot[0] * 180) / Math.PI),
          Math.round((rot[1] * 180) / Math.PI),
          Math.round((rot[2] * 180) / Math.PI),
        ],
        scale: typeof prop.scale === 'number' ? prop.scale : 1.0,
        isObstacle: prop.is_obstacle,
        obstacleRadius: prop.obstacle_radius || 0.6,
        socketType: (prop.smart_socket?.socket_type as any) || 'none',
      });
    });

    return list;
  }, [scene]);

  // Current active object or fallback to first
  const activeObj = React.useMemo(() => {
    if (!selectedObject) return allObjects[0] || null;
    return allObjects.find((o) => o.id === selectedObject.id) || selectedObject;
  }, [selectedObject, allObjects]);

  const handlePositionChange = (axisIndex: 0 | 1 | 2, val: number) => {
    if (!activeObj) return;
    const newPos: [number, number, number] = [...activeObj.position];
    newPos[axisIndex] = parseFloat(val.toFixed(2));
    onUpdateTransform({
      ...activeObj,
      position: newPos,
    });
  };

  const handleRotationChange = (axisIndex: 0 | 1 | 2, degVal: number) => {
    if (!activeObj) return;
    const newRot: [number, number, number] = [...activeObj.rotation];
    newRot[axisIndex] = Math.round(degVal);
    onUpdateTransform({
      ...activeObj,
      rotation: newRot,
    });
  };

  const handleScaleChange = (scaleVal: number) => {
    if (!activeObj) return;
    onUpdateTransform({
      ...activeObj,
      scale: Math.max(0.05, Math.min(10.0, parseFloat(scaleVal.toFixed(2)))),
    });
  };

  const handleSnapToGround = () => {
    if (!activeObj) return;
    handlePositionChange(1, 0.0);
  };

  const handleNudge = (axisIndex: 0 | 1 | 2, delta: number) => {
    if (!activeObj) return;
    handlePositionChange(axisIndex, activeObj.position[axisIndex] + delta);
  };

  if (!activeObj) {
    return (
      <div className="unity-inspector-empty">
        <Box size={28} color="#64748b" />
        <span>Chưa có vật thể nào trong Scene để tùy chỉnh</span>
      </div>
    );
  }

  const rawProp = (scene.environment.placed_props || []).find((p) => p.id === activeObj.id);

  return (
    <div className="unity-transform-inspector">
      {/* Object Selector Header */}
      <div className="inspector-target-selector">
        <div className="target-label">
          <Layers size={13} color="#38bdf8" />
          <span>VẬT THỂ ĐANG CHỌN (HIERARCHY)</span>
        </div>

        <select
          className="target-dropdown"
          value={activeObj.id}
          onChange={(e) => {
            const found = allObjects.find((o) => o.id === e.target.value);
            if (found) onSelectObject(found);
          }}
        >
          <optgroup label="Diễn Viên & Nhân Vật">
            {allObjects.filter((o) => o.category === 'actor').map((o) => (
              <option key={o.id} value={o.id}>
                {o.name}
              </option>
            ))}
          </optgroup>
          <optgroup label="Đồ Vật & Cảnh Quan (Placed Props)">
            {allObjects.filter((o) => o.category === 'prop').map((o) => (
              <option key={o.id} value={o.id}>
                {o.name}
              </option>
            ))}
          </optgroup>
        </select>
      </div>

      {/* Unity-Style Transform Component Box */}
      <div className="unity-component-box">
        <div className="component-header">
          <div className="comp-title">
            <Move size={13} color="#38bdf8" />
            <span>Transform (Tọa Độ & Góc Xoay)</span>
          </div>
          <button
            className="comp-reset-btn"
            title="Reset vị trí về gốc (0, 0, 0)"
            onClick={() => {
              onUpdateTransform({
                ...activeObj,
                position: [0, 0, 0],
                rotation: [0, 0, 0],
                scale: 1.0,
              });
            }}
          >
            <RotateCcw size={11} /> Reset
          </button>
        </div>

        <div className="transform-fields">
          {/* 1. POSITION (X, Y, Z) WITH DEDICATED SLIDERS + NUMBER INPUTS */}
          <div className="transform-section-header">
            <span>VỊ TRÍ (POSITION - Mét)</span>
          </div>

          <TransformAxisSlider
            label="X"
            badgeClass="axis-x"
            value={activeObj.position[0]}
            min={-40}
            max={40}
            step={0.1}
            unit="m"
            onChange={(val) => handlePositionChange(0, val)}
            onNudge={(d) => handleNudge(0, d)}
          />

          <TransformAxisSlider
            label="Y"
            badgeClass="axis-y"
            value={activeObj.position[1]}
            min={-5}
            max={25}
            step={0.1}
            unit="m"
            onChange={(val) => handlePositionChange(1, val)}
            onNudge={(d) => handleNudge(1, d)}
          />

          <TransformAxisSlider
            label="Z"
            badgeClass="axis-z"
            value={activeObj.position[2]}
            min={-40}
            max={40}
            step={0.1}
            unit="m"
            onChange={(val) => handlePositionChange(2, val)}
            onNudge={(d) => handleNudge(2, d)}
          />

          {/* 2. ROTATION (X, Y, Z) WITH DEDICATED SLIDERS + NUMBER INPUTS */}
          <div className="transform-section-header" style={{ marginTop: 8 }}>
            <span>GÓC XOAY (ROTATION - Độ)</span>
          </div>

          <TransformAxisSlider
            label="X°"
            badgeClass="axis-x"
            value={activeObj.rotation[0]}
            min={-180}
            max={180}
            step={5}
            unit="°"
            onChange={(val) => handleRotationChange(0, val)}
          />

          <TransformAxisSlider
            label="Y°"
            badgeClass="axis-y"
            value={activeObj.rotation[1]}
            min={-180}
            max={180}
            step={5}
            unit="°"
            onChange={(val) => handleRotationChange(1, val)}
          />

          <TransformAxisSlider
            label="Z°"
            badgeClass="axis-z"
            value={activeObj.rotation[2]}
            min={-180}
            max={180}
            step={5}
            unit="°"
            onChange={(val) => handleRotationChange(2, val)}
          />

          {/* 3. SCALE WITH SLIDER + NUMBER INPUT */}
          <div className="transform-section-header" style={{ marginTop: 8 }}>
            <span>KÍCH THƯỚC (SCALE)</span>
          </div>

          <TransformAxisSlider
            label="Scale"
            badgeClass="axis-scale"
            value={activeObj.scale}
            min={0.1}
            max={5.0}
            step={0.05}
            unit="x"
            onChange={(val) => handleScaleChange(val)}
          />
        </div>

        {/* Quick Helper Tools */}
        <div className="quick-helper-toolbar">
          <button
            className="helper-tool-btn"
            title="Hạ vật thể tiếp đất chính xác tại Y = 0"
            onClick={handleSnapToGround}
          >
            <ArrowDown size={12} /> Tiếp Đất (Y=0)
          </button>

          <button
            className="helper-tool-btn"
            title="Xoay góc nhìn camera tới vật thể này"
            onClick={() => onFocusObject?.(activeObj.position)}
          >
            <Eye size={12} /> Focus Cam
          </button>

          {activeObj.category === 'prop' && rawProp && onDuplicateProp && (
            <button
              className="helper-tool-btn"
              title="Nhân bản thêm một vật thể tương tự"
              onClick={() => onDuplicateProp(rawProp)}
            >
              <Copy size={12} /> Nhân Bản
            </button>
          )}

          {activeObj.category === 'prop' && onDeleteProp && (
            <button
              className="helper-tool-btn danger"
              title="Xóa vật thể khỏi Scene"
              onClick={() => onDeleteProp(activeObj.id)}
            >
              <Trash2 size={12} /> Xóa
            </button>
          )}
        </div>
      </div>

      {/* Smart Socket & Physics Collider Properties (for Props) */}
      {activeObj.category === 'prop' && rawProp && (
        <div className="unity-component-box">
          <div className="component-header">
            <div className="comp-title">
              <Shield size={13} color="#10b981" />
              <span>Smart Socket & Collider</span>
            </div>
          </div>

          <div className="component-properties">
            <div className="prop-setting-row">
              <span className="prop-label">Cản va chạm (Obstacle):</span>
              <label className="switch">
                <input
                  type="checkbox"
                  checked={activeObj.isObstacle ?? true}
                  onChange={(e) => {
                    onUpdateTransform({
                      ...activeObj,
                      isObstacle: e.target.checked,
                    });
                  }}
                />
                <span className="slider round"></span>
              </label>
            </div>

            <div className="prop-setting-row">
              <span className="prop-label">Loại tương tác (Socket):</span>
              <select
                className="socket-select"
                value={activeObj.socketType || 'none'}
                onChange={(e) => {
                  onUpdateTransform({
                    ...activeObj,
                    socketType: e.target.value as any,
                  });
                }}
              >
                <option value="none">Không có (Trang trí)</option>
                <option value="sit">Ngồi nghỉ (Sit Chair)</option>
                <option value="climb">Leo trèo (Climb Tree)</option>
              </select>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
