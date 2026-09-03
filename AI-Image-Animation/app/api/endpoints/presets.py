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
        wind_strength=0.7,
        wave_frequency=1.0,
        turbulence=0.3,
        flutter_scale=0.8
    ),
    WindPresetInfo(
        id="hair_sway",
        name="Tóc Bay Bồng Bềnh (Hair Sway)",
        description="Tối ưu cho tóc dài nhân vật bay theo chiều gió với độ nhấp nhô mượt mà.",
        icon="💇‍♀️",
        wind_strength=1.0,
        wave_frequency=1.0,
        turbulence=0.4,
        flutter_scale=1.1
    ),
    WindPresetInfo(
        id="fabric_flutter",
        name="Quần Áo Phất Phơ (Fabric Flutter)",
        description="Mô phỏng nếp gấp vải lụa, áo choàng, tà váy bay lượn trong gió.",
        icon="👗",
        wind_strength=1.2,
        wave_frequency=1.0,
        turbulence=0.6,
        flutter_scale=1.3
    ),
    WindPresetInfo(
        id="ocean_wave",
        name="Dòng Nước / Mây Trôi (Flow Stream)",
        description="Chuyển động dòng chảy liên tục cho mặt nước, mây bay, suối tóc.",
        icon="🌊",
        wind_strength=1.0,
        wave_frequency=1.0,
        turbulence=0.3,
        flutter_scale=1.0
    ),
    WindPresetInfo(
        id="idle_breathing",
        name="Nhịp Thở Sống Động (Idle Ambient)",
        description="Chuyển động vi mô siêu nhẹ tạo cảm giác nhân vật có hồn, tự nhiên.",
        icon="✨",
        wind_strength=0.35,
        wave_frequency=1.0,
        turbulence=0.15,
        flutter_scale=0.5
    ),
    WindPresetInfo(
        id="strong_gale",
        name="Gió Cuộn Mạnh (Dynamic Gale)",
        description="Biên độ gió lớn cho phân cảnh hành động, áo choàng và tóc tung bay.",
        icon="💨",
        wind_strength=1.8,
        wave_frequency=2.0,
        turbulence=0.8,
        flutter_scale=1.8
    )
]


@router.get("", response_model=List[WindPresetInfo], summary="Get list of wind & motion presets")
async def list_wind_presets():
    """
    Returns available physics presets for hair, clothes, and wind dynamics.
    """
    return PRESETS_DATA
