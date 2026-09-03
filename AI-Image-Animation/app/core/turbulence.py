import numpy as np


class TurbulenceGenerator:
    """
    Generates multi-harmonic fluttering waves and organic fluid turbulence for hair, cloth, and fluid dynamics.
    Guarantees seamless temporal looping and smooth spatial propagation.
    """
    @staticmethod
    def compute_wave_displacement(
        height: int,
        width: int,
        flow_x: np.ndarray,
        flow_y: np.ndarray,
        phase: float,
        wave_frequency: float = 1.0,
        turbulence: float = 0.5,
        flutter_scale: float = 1.0
    ) -> np.ndarray:
        """
        Calculates a 2D continuous displacement map at normalized phase [0.0, 1.0].
        """
        # Normalized spatial coordinate grids [0, 1]
        y_grid = np.linspace(0.0, 1.0, height, dtype=np.float32)[:, None]
        x_grid = np.linspace(0.0, 1.0, width, dtype=np.float32)[None, :]
        
        # Exact integer harmonic base for perfect seamless loop (frequency in cycles)
        freq_k = max(1, round(wave_frequency))
        phase_rad = phase * 2.0 * np.pi * freq_k
        
        # Wind-aligned projection
        flow_magnitude = np.sqrt(flow_x ** 2 + flow_y ** 2) + 1e-6
        norm_fx = flow_x / flow_magnitude
        norm_fy = flow_y / flow_magnitude
        
        # Smooth spatial wave propagation along vector direction (1-2 wave ripples across image)
        spatial_lag = (x_grid * norm_fx + y_grid * norm_fy) * (np.pi * 2.0)
        
        # 1. Primary harmonic fundamental wave (large organic sway)
        wave1 = np.sin(phase_rad - spatial_lag * 0.75)
        
        # 2. Secondary overtone harmonic (natural ripple along hair/cloth)
        wave2 = 0.35 * np.sin(phase_rad * 2.0 - spatial_lag * 1.5 + 0.5)
        
        # 3. Tertiary micro-flutter (high-frequency strand vibration)
        wave3 = 0.15 * np.sin(phase_rad * 3.0 - spatial_lag * 2.5 + 1.2)
        
        # Blend harmonics with turbulence multiplier
        total_oscillation = wave1 + turbulence * (wave2 + wave3)
        
        # Normalize peak amplitude to prevent harsh over-stretching
        return total_oscillation * (flutter_scale * 0.85)

