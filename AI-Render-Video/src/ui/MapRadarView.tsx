import React, { useRef, useEffect } from 'react';
import * as THREE from 'three';
import { NavMeshManager } from '../core/navigation/NavMeshManager';
import { VRMAvatar } from '../core/actors/VRMAvatar';

interface MapRadarViewProps {
  actors: Map<string, VRMAvatar>;
  navMesh: NavMeshManager;
}

export const MapRadarView: React.FC<MapRadarViewProps> = ({ actors, navMesh }) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const width = canvas.width;
    const height = canvas.height;
    const centerX = width / 2;
    const centerY = height / 2;
    const scale = 14; // 1 meter = 14 pixels

    // Clear
    ctx.fillStyle = '#0a0d18';
    ctx.fillRect(0, 0, width, height);

    // Draw Radar Grid Circles & Axes
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.05)';
    ctx.lineWidth = 1;
    for (let r = 20; r <= 140; r += 30) {
      ctx.beginPath();
      ctx.arc(centerX, centerY, r, 0, Math.PI * 2);
      ctx.stroke();
    }

    ctx.beginPath();
    ctx.moveTo(0, centerY);
    ctx.lineTo(width, centerY);
    ctx.moveTo(centerX, 0);
    ctx.lineTo(centerX, height);
    ctx.stroke();

    // Draw Path in Middle
    ctx.fillStyle = 'rgba(79, 73, 67, 0.3)';
    ctx.fillRect(centerX - 2 * scale, 10, 4 * scale, height - 20);

    // Draw Obstacles
    const obstacles = navMesh.getObstacles();
    for (const obs of obstacles) {
      const ox = centerX + obs.position[0] * scale;
      const oy = centerY + obs.position[2] * scale;
      const or = obs.radius * scale;

      // Safe radius boundary
      ctx.fillStyle = 'rgba(239, 68, 68, 0.15)';
      ctx.beginPath();
      ctx.arc(ox, oy, or + 6, 0, Math.PI * 2);
      ctx.fill();

      // Obstacle core
      ctx.fillStyle = obs.type === 'tree' ? '#1f6629' : obs.type === 'chair' ? '#7c471b' : '#a855f7';
      ctx.beginPath();
      ctx.arc(ox, oy, or, 0, Math.PI * 2);
      ctx.fill();

      ctx.fillStyle = '#94a3b8';
      ctx.font = '9px "JetBrains Mono"';
      ctx.textAlign = 'center';
      ctx.fillText(obs.name, ox, oy - or - 3);
    }

    // Draw Actors
    for (const [id, avatar] of actors.entries()) {
      const pos = new THREE.Vector3();
      avatar.rootObject.getWorldPosition(pos);

      const ax = centerX + pos.x * scale;
      const ay = centerY + pos.z * scale;
      const isWarrior = id.includes('warrior');
      const color = isWarrior ? '#eab308' : '#a855f7';

      // Direction pointer
      const rotY = avatar.rootObject.rotation.y;
      const dirX = Math.sin(rotY) * 16;
      const dirY = Math.cos(rotY) * 16;

      ctx.strokeStyle = color;
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.moveTo(ax, ay);
      ctx.lineTo(ax + dirX, ay + dirY);
      ctx.stroke();

      // Actor Dot
      ctx.fillStyle = color;
      ctx.beginPath();
      ctx.arc(ax, ay, 7, 0, Math.PI * 2);
      ctx.fill();

      ctx.fillStyle = '#ffffff';
      ctx.font = 'bold 10px "Plus Jakarta Sans"';
      ctx.textAlign = 'center';
      ctx.fillText(isWarrior ? 'Chiến Binh' : 'Phù Thủy', ax, ay + 18);
    }
  });

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <span style={{ fontSize: 13, fontWeight: 700, color: '#f8fafc' }}>
          Radar Bản Đồ 2D (NavMesh)
        </span>
        <span style={{ fontSize: 11, color: '#34d399' }}>● Live Tracking</span>
      </div>
      <canvas ref={canvasRef} width={340} height={200} className="radar-canvas" />
    </div>
  );
};
