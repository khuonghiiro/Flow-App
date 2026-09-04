"""Standard Prompt Templates for Tab 4 Skill Tree Pipeline.

Pipeline execution order:
  Stage 1: Generate Master Base 0° (text_to_image)
  Stage 2: Generate 4 remaining angles (45°, 90°, 135°, 180°) using 0° as ref
  Stage 3: Generate action videos per angle (walk, idle, run, attack, etc.)
           Each action has 5 sub-prompts (one per angle), using that angle's
           reference image as start+end frame for 4s seamless loop.
"""

DEFAULT_CUSTOMIZER_VALUES = {
    "characterName": "Lâm Tiêu (Lin Xiao)",
    "style": "2D Xianxia/Fantasy anime chibi style, bold clean linework, flat cel-shaded coloring",
    "gender": "female",
    "age": "young adult (18-20)",
    "hairStyleColor": "Long silky platinum white hair with twin front braids, glowing jade hairpin",
    "outfitDescription": "Xianxia flowing silk daoist robes with wide sleeves, floating ribbons",
    "primaryColor": "Pure White & Soft Cyan Jade (#00E5FF)",
    "accentColor": "Soft Platinum & Lilac Purple",
    "skinTone": "Fair porcelain skin tone",
    "weaponType": "Enchanted Flying Sword (Lam Ngoc Kiem)",
    "spellElement": "Ice & Cyan Frost Aura",
    "chromaBgHex": "#00FF00",
}

# ─── Stage 1+2: 5 Body Angles Prompts ─────────────────────────
# Stage 1: angle "0" (text_to_image, no reference)
# Stage 2: angles "45","90","135","180" (image_to_image, ref = 0° image)

ANGLE_PROMPT_TEMPLATES = {
    "0": (
        "MASTER CHARACTER DESIGN — 2D XIANXIA CHIBI — 0° DIRECT FRONT VIEW\n"
        "CAMERA & POSE: Character MUST face DIRECTLY forward at camera (0° strict front view). "
        "Head facing 100% straight forward towards viewer. NO head turn, NO head tilt, NO 3/4 angle. "
        "Symmetrical standing pose, torso upright, shoulders level, both arms relaxed at sides, legs straight. "
        "CRITICAL — BLANK FACELESS HEAD: Completely BLANK, SMOOTH, FEATURELESS face surface. "
        "NO eyes, NO eyebrows, NO nose, NO mouth. Pure smooth porcelain skin. "
        "CHARACTER: Genre: {style}. Age: {age}. Gender: {gender}. "
        "HAIR: {hairStyleColor}. OUTFIT: {outfitDescription}. "
        "Colors: {primaryColor}. Accents: {accentColor}. Skin: {skinTone}. "
        "STYLE: Pure flat 2D anime/chibi illustration, bold clean linework, flat cel-shaded coloring. "
        "BACKGROUND: Solid chroma-key green {chromaBgHex}. Centered, full body visible."
    ),
    "45": (
        "ROTATE CHARACTER 45° TO THE LEFT — 2D XIANXIA CHIBI — THREE-QUARTER VIEW\n"
        "Use the 0° front view reference image as ABSOLUTE IDENTITY SOURCE. "
        "CRITICAL ROTATION: Rotate the ENTIRE character body 45 degrees to the LEFT. "
        "The character's LEFT SHOULDER must be closer to the camera. "
        "The character's RIGHT SHOULDER must be further from the camera. "
        "The chest faces diagonally left at exactly 45 degrees from the camera plane. "
        "Head also turned 45° left showing three-quarter profile of the blank faceless head. "
        "Both feet point towards the bottom-left corner of the frame. "
        "WHAT TO SEE: Left side of the body (left arm, left leg) is in front/prominent. "
        "Right side of the body is partially hidden behind the left side. "
        "Smooth blank faceless contour (NO eyes, NO nose, NO mouth). "
        "IDENTITY LOCK: Exact same hairstyle, hair color, costume design, proportions, "
        "skin tone {skinTone}, and color palette as the 0° reference. "
        "STYLE: Pure flat 2D anime illustration, bold linework, flat cel-shaded colors. "
        "BACKGROUND: Solid chroma-key green {chromaBgHex}. Centered, full body visible."
    ),
    "90": (
        "ROTATE CHARACTER 90° — FULL LEFT SIDE PROFILE — 2D XIANXIA CHIBI\n"
        "Use the 0° front view reference image as ABSOLUTE IDENTITY SOURCE. "
        "CRITICAL ROTATION: The character body is rotated EXACTLY 90 degrees. "
        "Camera sees the character from the PURE LEFT SIDE. "
        "The character's LEFT SHOULDER faces directly at the camera. "
        "The LEFT ARM is in front, the RIGHT ARM is completely hidden behind the body. "
        "The character's nose (if visible) would point to the LEFT EDGE of the frame. "
        "Both feet point to the LEFT side of the frame. "
        "WHAT TO SEE: Pure side silhouette — only the left half of the body is visible. "
        "The chest/torso is seen from the side (thin profile, not wide front). "
        "Hair falls along the side profile. "
        "Smooth blank faceless side silhouette (NO facial features). "
        "IDENTITY LOCK: Exact same hairstyle side view, costume profile, proportions, "
        "colors identical to 0° reference. Skin tone {skinTone}. "
        "STYLE: Pure flat 2D anime illustration, bold linework, flat cel-shaded colors. "
        "BACKGROUND: Solid chroma-key green {chromaBgHex}. Centered, full body visible."
    ),
    "135": (
        "ROTATE CHARACTER 135° — BACK-LEFT THREE-QUARTER VIEW — 2D XIANXIA CHIBI\n"
        "Use the 0° front reference as ABSOLUTE IDENTITY SOURCE. "
        "CRITICAL ROTATION: Character viewed from BEHIND, angled 45° towards the LEFT. "
        "Camera is BEHIND the character, slightly to the RIGHT. "
        "The character's BACK is mostly visible, tilted to show the left-back side. "
        "Back of hairstyle, hair ornaments, back of robes, sash ribbons visible. "
        "LEFT shoulder is closer to camera (back-left perspective). "
        "Both legs and boots angled away from camera towards the left. "
        "IDENTITY LOCK: Exact same hairstyle back details, outfit design, "
        "colors identical to reference. Skin tone {skinTone}. "
        "STYLE: Pure flat 2D anime illustration, bold linework, flat colors. "
        "BACKGROUND: Solid chroma-key green {chromaBgHex}. Centered, full body visible."
    ),
    "180": (
        "ROTATE CHARACTER 180° — PERFECT REAR VIEW — 2D XIANXIA CHIBI\n"
        "Use the 0° front-view reference image as ABSOLUTE IDENTITY SOURCE. "
        "CRITICAL ROTATION: Character faces 100% AWAY from camera (180° rear view). "
        "Full back of hairstyle and ornaments symmetrically displayed. "
        "Spine vertical, full back of robes facing camera. "
        "Both legs straight and symmetrical, heels facing camera, feet pointing away. "
        "IDENTITY LOCK: Exact same hair color, proportions, costume rear details. "
        "Skin tone {skinTone}. "
        "STYLE: Pure flat 2D anime illustration, bold linework, flat colors. "
        "BACKGROUND: Solid chroma-key green {chromaBgHex}. Centered, full body visible."
    ),
}

# ─── Stage 3: Action Video Prompts (4s Seamless Loop) ──────────
# Each action has 5 angle variants. The pipeline uses the angle's
# reference image as BOTH start frame AND end frame for seamless loop.

WALK_PROMPT_TEMPLATES = {
    "0": (
        "4-second seamless loop FRONT VIEW WALK CYCLE. "
        "Character walks in place facing DIRECTLY at camera (0° front view). "
        "Left foot steps forward, alternating with right foot in natural rhythm. "
        "Arms swing opposite to legs. Torso and head stay upright and stable. "
        "Subtle hair and robe sway with each step. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "45": (
        "4-second seamless loop 45° THREE-QUARTER WALK CYCLE. "
        "Character walks in place at 45° angle, body facing bottom-left. "
        "Natural diagonal strides, arms swinging along body plane. "
        "No body turning. Hair and clothing sway with walking cadence. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "90": (
        "4-second seamless loop SIDE PROFILE WALK CYCLE (90°). "
        "Character walks in place in pure left side profile. "
        "Legs swing with clear heel-to-toe contact, arms swing naturally in profile. "
        "Body stays strictly 90° side view. Subtle robe and hair sway. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "135": (
        "4-second seamless loop 135° BACK-LEFT WALK CYCLE. "
        "Character walks in place viewed from behind, angled towards left. "
        "Back of legs stepping, ribbons and hair flowing with steps. "
        "Body stays at 135° orientation. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "180": (
        "4-second seamless loop REAR VIEW WALK CYCLE (180°). "
        "Character walks in place facing directly away from camera. "
        "Symmetrical stepping, arms swing in rear perspective. "
        "Hair cascades with subtle rhythmic motion. Spine stays vertical. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
}

IDLE_PROMPT_TEMPLATES = {
    "0": (
        "4-second seamless loop IDLE BREATHING (0° front view). "
        "Character stands still facing camera, gentle breathing motion. "
        "Subtle chest rise/fall, soft hair sway, clothing floats slightly. "
        "No footstep, no arm movement. Calm, relaxed idle animation. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "45": (
        "4-second seamless loop IDLE BREATHING (45° three-quarter view). "
        "Character stands still at 45° angle, gentle breathing. "
        "Subtle body sway, hair and ribbons float softly. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "90": (
        "4-second seamless loop IDLE BREATHING (90° side profile). "
        "Character stands still in side profile, gentle breathing. "
        "Chest rises/falls subtly in profile view, hair sways. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "135": (
        "4-second seamless loop IDLE BREATHING (135° back-left view). "
        "Character stands still viewed from behind at angle, gentle breathing. "
        "Back hair and ribbons float softly. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "180": (
        "4-second seamless loop IDLE BREATHING (180° rear view). "
        "Character stands still facing away, gentle breathing from behind. "
        "Hair cascades with subtle motion, robe sways. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
}

RUN_PROMPT_TEMPLATES = {
    "0": (
        "4-second seamless loop FRONT VIEW RUN CYCLE. "
        "Character runs in place facing camera (0° front). Fast leg strides. "
        "Arms pump vigorously, hair and robes fly back with speed. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "45": (
        "4-second seamless loop 45° THREE-QUARTER RUN CYCLE. "
        "Character runs in place at 45° angle towards bottom-left. "
        "Fast diagonal strides, arms pump along body plane. "
        "Hair and clothing stream back with momentum. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "90": (
        "4-second seamless loop SIDE PROFILE RUN CYCLE (90°). "
        "Character runs in place in pure left side profile. "
        "Fast strides with clear leg extension, arms pump in profile. "
        "Hair streams behind, robes flutter. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "135": (
        "4-second seamless loop 135° BACK-LEFT RUN CYCLE. "
        "Character runs in place viewed from behind, angled left. "
        "Fast strides away from camera, hair and ribbons stream. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "180": (
        "4-second seamless loop REAR VIEW RUN CYCLE (180°). "
        "Character runs in place facing away from camera. "
        "Fast symmetrical strides, hair bounces with running rhythm. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
}

ATTACK_PROMPT_TEMPLATES = {
    "0": (
        "4-second seamless loop ATTACK COMBO (0° front view). "
        "Character performs a repeating sword slash combo facing camera. "
        "Weapon swings left-right-overhead in fluid sequence. "
        "Hair and robes whip with sword momentum. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "45": (
        "4-second seamless loop ATTACK COMBO (45° three-quarter view). "
        "Character performs repeating sword combo at 45° angle. "
        "Diagonal slashes with fluid body rotation. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "90": (
        "4-second seamless loop ATTACK COMBO (90° side profile). "
        "Character performs repeating horizontal slash in side profile. "
        "Sword extends forward then pulls back in rhythm. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "135": (
        "4-second seamless loop ATTACK COMBO (135° back-left view). "
        "Character performs repeating slash viewed from behind. "
        "Back muscles and robes twist with each swing. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "180": (
        "4-second seamless loop ATTACK COMBO (180° rear view). "
        "Character performs repeating slash facing away from camera. "
        "Weapon arcs visible from behind, hair whips with motion. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
}

DEFEND_PROMPT_TEMPLATES = {
    "0": (
        "4-second seamless loop GUARD STANCE (0° front view). "
        "Character holds defensive pose facing camera, sword raised as shield. "
        "Subtle breathing, slight guard-ready sway, muscles tense. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "45": (
        "4-second seamless loop GUARD STANCE (45° three-quarter). "
        "Character holds defensive pose at 45° angle. "
        "Guard-ready sway with sword angled for protection. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "90": (
        "4-second seamless loop GUARD STANCE (90° side profile). "
        "Character holds defensive pose in side profile. "
        "Sword raised vertically, body braced in profile view. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "135": (
        "4-second seamless loop GUARD STANCE (135° back-left). "
        "Character holds guard viewed from behind at angle. "
        "Back tension visible, sword positioned defensively. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "180": (
        "4-second seamless loop GUARD STANCE (180° rear view). "
        "Character holds guard facing away from camera. "
        "Sword behind back in defensive ready position. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
}

# ─── Action Registry ──────────────────────────────────────────
# Maps action keys to their template dicts for pipeline lookup

ACTION_TEMPLATES = {
    "walk": WALK_PROMPT_TEMPLATES,
    "idle": IDLE_PROMPT_TEMPLATES,
    "run": RUN_PROMPT_TEMPLATES,
    "attack": ATTACK_PROMPT_TEMPLATES,
    "defend": DEFEND_PROMPT_TEMPLATES,
}

# Pipeline stage execution order for the AI agent
PIPELINE_STAGES = [
    {
        "stage": 1,
        "key": "base_0",
        "label": "Tao Nhan Vat Goc 0 do",
        "type": "image",
        "mode": "text_to_image",
        "angles": ["0"],
        "ref": None,
    },
    {
        "stage": 2,
        "key": "angles_4",
        "label": "Tao 4 goc con lai tu anh 0 do",
        "type": "image",
        "mode": "image_to_image",
        "angles": ["45", "90", "135", "180"],
        "ref": "0",
    },
    {
        "stage": 3,
        "key": "actions",
        "label": "Tao video hanh dong theo 5 goc",
        "type": "video",
        "mode": "image_to_video_loop_4s",
        "actions": ["walk", "idle", "run", "attack", "defend"],
        "angles": ["0", "45", "90", "135", "180"],
        "ref": "per_angle",
    },
]


def format_template(template: str, customizer: dict = None) -> str:
    """Fill template variables with customizer values."""
    c = {**DEFAULT_CUSTOMIZER_VALUES, **(customizer or {})}
    text = template
    for key, val in c.items():
        placeholder = f"{{{key}}}"
        if placeholder in text:
            text = text.replace(placeholder, str(val))
    return text


def get_action_templates(action_key: str) -> dict:
    """Get prompt templates dict for an action key (walk, idle, run, etc.)."""
    return ACTION_TEMPLATES.get(action_key, {})
