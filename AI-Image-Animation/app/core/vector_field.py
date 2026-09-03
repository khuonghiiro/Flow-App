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
                vec_len_sq = (vec.end_x - vec.start_x) ** 2 + (vec.end_y - vec.start_y) ** 2 + 1e-6
                vec_len = np.sqrt(vec_len_sq)
                
                # Projection of each grid point onto vector line segment [0, 1]
                t_proj = ((grid_x - vec.start_x) * (vec.end_x - vec.start_x) + 
                          (grid_y - vec.start_y) * (vec.end_y - vec.start_y)) / vec_len_sq
                t_clamped = np.clip(t_proj, 0.0, 1.0)
                
                # Closest point on line segment
                closest_x = vec.start_x + t_clamped * (vec.end_x - vec.start_x)
                closest_y = vec.start_y + t_clamped * (vec.end_y - vec.start_y)
                
                # Perpendicular distance squared to vector segment
                dist_sq = (grid_x - closest_x) ** 2 + (grid_y - closest_y) ** 2
                radius = max(0.12, vec_len * 0.9)
                
                # Directional Gaussian falloff
                spatial_weight = np.exp(-dist_sq / (2.0 * (radius ** 2)))
                
                # Progressive tip amplification (roots sway less, tips sway more)
                progressive_gain = np.clip(0.3 + 0.9 * t_clamped, 0.2, 1.4)
                
                weight = spatial_weight * progressive_gain
                flow_x += vx * weight
                flow_y += vy * weight
                total_weight += spatial_weight
                
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
