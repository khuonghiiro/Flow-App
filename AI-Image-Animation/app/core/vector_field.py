from typing import List, Tuple
import numpy as np
from app.schemas.request_models import MotionVector, AnchorPin


class VectorFieldGenerator:
    """
    Interpolates sparse motion arrows and anchor pins into a dense 2D directional flow field.
    """
    @staticmethod
    def generate_flow_field(
        height: int,
        width: int,
        vectors: List[MotionVector],
        pins: List[AnchorPin],
        default_direction: Tuple[float, float] = (1.0, 0.0)
    ) -> Tuple[np.ndarray, np.ndarray]:
        """
        Creates (flow_x, flow_y) dense displacement fields normalized to pixel dimensions.
        """
        # Create normalized coordinate grid [0, 1]
        y_coords = np.linspace(0.0, 1.0, height, dtype=np.float32)
        x_coords = np.linspace(0.0, 1.0, width, dtype=np.float32)
        grid_x, grid_y = np.meshgrid(x_coords, y_coords)
        
        flow_x = np.zeros((height, width), dtype=np.float32)
        flow_y = np.zeros((height, width), dtype=np.float32)
        
        if not vectors:
            # Fallback to default wind direction (e.g. horizontal rightwards breeze)
            flow_x[:, :] = default_direction[0]
            flow_y[:, :] = default_direction[1]
        else:
            # Weight matrix accumulator
            total_weight = np.zeros((height, width), dtype=np.float32) + 1e-6
            
            for vec in vectors:
                vx = (vec.end_x - vec.start_x) * vec.strength
                vy = (vec.end_y - vec.start_y) * vec.strength
                
                # Center of the vector segment
                mid_x = (vec.start_x + vec.end_x) * 0.5
                mid_y = (vec.start_y + vec.end_y) * 0.5
                
                # Vector length determines influence radius
                vec_len = np.hypot(vec.end_x - vec.start_x, vec.end_y - vec.start_y)
                radius = max(0.15, vec_len * 1.5)
                
                # Distance squared from vector midpoint
                dist_sq = (grid_x - mid_x) ** 2 + (grid_y - mid_y) ** 2
                
                # Gaussian falloff kernel
                weight = np.exp(-dist_sq / (2.0 * (radius ** 2)))
                
                flow_x += vx * weight
                flow_y += vy * weight
                total_weight += weight
                
            flow_x /= total_weight
            flow_y /= total_weight

        # Apply static anchor pins to freeze face / body / unselected areas
        if pins:
            flow_x, flow_y = VectorFieldGenerator._apply_anchor_pins(flow_x, flow_y, grid_x, grid_y, pins)
            
        return flow_x, flow_y

    @staticmethod
    def _apply_anchor_pins(
        flow_x: np.ndarray,
        flow_y: np.ndarray,
        grid_x: np.ndarray,
        grid_y: np.ndarray,
        pins: List[AnchorPin]
    ) -> Tuple[np.ndarray, np.ndarray]:
        """
        Dampens motion vectors near static anchor pin points.
        """
        pin_dampening = np.ones_like(flow_x)
        
        for pin in pins:
            dist = np.sqrt((grid_x - pin.x) ** 2 + (grid_y - pin.y) ** 2)
            pin_radius = max(0.02, pin.radius)
            
            # Smooth Hermite / Cosine falloff
            influence = np.clip(dist / pin_radius, 0.0, 1.0)
            # Smoothstep curve: 3x^2 - 2x^3
            smooth_falloff = influence * influence * (3.0 - 2.0 * influence)
            
            # Blend with pin weight
            factor = (1.0 - pin.weight) + pin.weight * smooth_falloff
            pin_dampening = np.minimum(pin_dampening, factor)
            
        flow_x = flow_x * pin_dampening
        flow_y = flow_y * pin_dampening
        return flow_x, flow_y
