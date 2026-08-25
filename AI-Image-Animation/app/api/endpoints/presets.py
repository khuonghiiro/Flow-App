from typing import List
from fastapi import APIRouter
from app.schemas.response_models import WindPresetInfo

router = APIRouter(prefix="/presets", tags=["Physics Presets"])

PRESETS_DATA: List[WindPresetInfo] = [
    WindPresetInfo(
        id="gentle_breeze",
        name="Gió Thoảng Nhẹ (Gentle Breeze)",
        description="Chuyển động gợn sóng nhẹ nhàng, phù hợp cho tóc mái và tà áo mềm.",
        icon="🍃",
        wind_strength=0.8,
        wave_frequency=1.2,
        turbulence=0.3,
        flutter_scale=0.8
    ),
    WindPresetInfo(
        id="hair_sway",
        name="Tóc Bay Bồng Bềnh (Hair Sway)",
        description="Tối ưu cho tóc dài nhân vật bay theo chiều gió với độ nhấp nhô mượt mà.",
        icon="💇‍♀️",
        wind_strength=1.3,
        wave_frequency=1.8,
        turbulence=0.6,
        flutter_scale=1.4
    ),
    WindPresetInfo(
        id="fabric_flutter",
        name="Quần Áo Phất Phơ (Fabric Flutter)",
        description="Mô phỏng nếp gấp vải lụa, áo choàng, tà váy bay lượn trong gió.",
        icon="👗",
        wind_strength=1.5,
        wave_frequency=2.2,
        turbulence=0.8,
        flutter_scale=1.6
    ),
    WindPresetInfo(
        id="strong_gale",
        name="Gió Thổi Mạnh (Strong Gale)",
        description="Gió giật mạnh với biên độ lớn, tóc và y phục bay dữ dội.",
        icon="💨",
        wind_strength=2.5,
        wave_frequency=3.0,
        turbulence=1.2,
        flutter_scale=2.2
    ),
    WindPresetInfo(
        id="ocean_wave",
        name="Sóng Nước Nhấp Nhô (Ocean Wave)",
        description="Chuyển động lượn sóng tuần hoàn cho mặt nước, mây trời hoặc background.",
        icon="🌊",
        wind_strength=1.1,
        wave_frequency=0.9,
        turbulence=0.4,
        flutter_scale=1.0
    ),
    WindPresetInfo(
        id="idle_breathing",
        name="Nhịp Thở / Môi Trường (Idle Ambient)",
        description="Chuyển động vi mô siêu nhẹ tạo cảm giác ảnh nhân vật đang sống động.",
        icon="✨",
        wind_strength=0.4,
        wave_frequency=0.7,
        turbulence=0.2,
        flutter_scale=0.5
    )
]


@router.get("", response_model=List[WindPresetInfo], summary="Get list of wind & motion presets")
async def list_wind_presets():
    """
    Returns available physics presets for hair, clothes, and wind dynamics.
    """
    return PRESETS_DATA
