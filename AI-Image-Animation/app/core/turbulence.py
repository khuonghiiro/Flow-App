import numpy as np


class TurbulenceGenerator:
    """
    Generates multi-harmonic fluttering waves and fluid turbulence for hair & fabric.
    """
    @staticmethod
    def compute_wave_displacement(
        height: int,
        width: int,
        flow_x: np.ndarray,
        flow_y: np.ndarray,
        phase: float,
        wave_frequency: float = 1.5,
        turbulence: float = 0.5,
        flutter_scale: float = 1.0
    ) -> np.ndarray:
        """
        Calculates a 2D scalar displacement oscillation map at normalized phase [0, 1].
        """
        # Coordinate grids
        y_grid = np.linspace(0.0, np.pi * 4.0, height, dtype=np.float32)[:, None]
        x_grid = np.linspace(0.0, np.pi * 4.0, width, dtype=np.float32)[None, :]
        
        # Primary harmonic traveling wave
        phase_rad = phase * 2.0 * np.pi * wave_frequency
        
        # Wind-aligned projection
        flow_magnitude = np.sqrt(flow_x ** 2 + flow_y ** 2) + 1e-6
        norm_fx = flow_x / flow_magnitude
        norm_fy = flow_y / flow_magnitude
        
        spatial_phase = (x_grid * norm_fx + y_grid * norm_fy) * 0.8
        
        # Base wave oscillation
        wave1 = np.sin(spatial_phase - phase_rad)
        
        # Secondary faster harmonic (fluttering tips of hair / cloth edges)
        wave2 = 0.4 * np.sin(spatial_phase * 2.3 - phase_rad * 1.7 + 1.2)
        
        # Tertiary micro-ripple
        wave3 = 0.2 * np.cos(spatial_phase * 4.7 - phase_rad * 2.9 + 2.5)
        
        total_oscillation = wave1 + turbulence * (wave2 + wave3)
        return total_oscillation * flutter_scale
