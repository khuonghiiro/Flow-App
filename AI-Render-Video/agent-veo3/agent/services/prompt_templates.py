"""Standard Prompt Templates for Tab 4 Skill Tree Pipeline.

Pipeline execution order:
  Stage 1: Generate Master Base 0° (text_to_image)
  Stage 2: Generate 4 remaining angles (45°, 90°, 135°, 180°) using 0° as ref
  Stage 3: Generate action videos per angle (walk, idle, run, attack, etc.)
           Each action has 5 sub-prompts (one per angle), using that angle's
           reference image as start+end frame for 4s seamless loop.
"""
import re

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
        "2D Xianxia anime chibi character sprite — TRUE DEEP 45-DEGREE THREE-QUARTER ISOMETRIC VIEW.\n"
        "Use the 0° front view reference image for IDENTITY LOCK (hairstyle, hair color {hairStyleColor}, outfit {outfitDescription}, colors {primaryColor}, accents {accentColor}, skin tone {skinTone}). "
        "CAMERA PERSPECTIVE: Camera is positioned at a 45-degree isometric angle to the left of the character. "
        "The character is stepping forward in a deep 45-degree diagonal stance (facing 10:30 o'clock towards top-left). "
        "ASYMMETRICAL 3/4 POSE: Left foot and left shoulder are stepped forward in the front foreground. "
        "Right foot and right shoulder are placed backward in depth behind the body. "
        "The chest plane and torso are visibly rotated 45 degrees diagonally away from camera. "
        "Head is turned 45 degrees to the left showing three-quarter facial contour. "
        "CRITICAL — BLANK FACELESS HEAD: Completely BLANK, SMOOTH, FEATURELESS face surface (NO eyes, NO eyebrows, NO nose, NO mouth). Pure smooth porcelain skin. "
        "STYLE: Pure flat 2D anime chibi sprite illustration, clean linework, flat cel-shaded coloring. "
        "BACKGROUND: Solid chroma-key green {chromaBgHex}. Centered, full body visible."
    ),
    "90": (
        "2D Xianxia anime chibi character sprite — STRICT 90-DEGREE LEFT SIDE PROFILE VIEW.\n"
        "Use the 0° front view reference image for IDENTITY LOCK (hairstyle, hair color {hairStyleColor}, outfit {outfitDescription}, colors {primaryColor}, accents {accentColor}, skin tone {skinTone}). "
        "CAMERA PERSPECTIVE: Camera is positioned directly at the character left flank (pure profile view). "
        "The character faces 100% directly towards the LEFT edge of the frame (9 o'clock). "
        "BODY SILHOUETTE: Only the LEFT side of the body is visible. Slender narrow vertical side profile. "
        "The LEFT ARM hangs in front, the RIGHT ARM and right shoulder are 100% COMPLETELY HIDDEN behind the torso. "
        "Chest faces left (pure side profile, NO front chest visible, NO three-quarter angle). "
        "Both feet point directly towards the left edge of the screen. "
        "CRITICAL — BLANK FACELESS HEAD: Smooth blank porcelain mannequin head in pure side profile silhouette (NO facial features, NO eyes/nose/mouth). "
        "STYLE: Pure flat 2D anime chibi illustration, bold clean linework, flat cel-shaded colors. "
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

MOTION_ANTI_GLITCH_LOCK = (
    "CRITICAL MOTION STABILITY CONSTRAINTS (STRICT ANTI-GLITCH LOCK): "
    "Hands and forearms strictly stay below chest level at all times, moving ONLY in a narrow pendulum arc parallel to hips. "
    "STRICTLY ZERO wild arm flailing, ZERO arm waving, ZERO hand gestures, ZERO dancing or acrobatic posing. "
    "Feet remain grounded in clean stride cycle, STRICTLY ZERO hopping, ZERO bouncing up and down, ZERO airborne jumping, ZERO floating. "
    "Torso, shoulders, and head remain rock-steady and level with ZERO torso twisting, erratic bobbing, or body contortion. "
    "Faceless blank mannequin head remains completely smooth with ZERO facial expressions, mouth opening, or morphing."
)

ARCHETYPE_STYLES = {
    "young_male": {
        "label": "YOUTH/YOUNG ADULT",
        "walk_desc": "with a confident upright posture and steady decisive cadence. Left foot steps forward alternating with right foot in natural rhythm on floor plane. Arms swing naturally in a disciplined narrow pendulum arc strictly below chest level close to hips. Torso and head stay upright and rock-steady.",
        "run_desc": "with dynamic athletic momentum. Body has a slight forward athletic lean, elbows bent at 90 degrees pumping rhythmically close to ribs. Fast decisive strides with clean knee lifts on floor plane. Hair and robes stream back with speed.",
    },
    "maiden": {
        "label": "YOUNG MAIDEN/TEEN GIRL",
        "walk_desc": "with a graceful, delicate, light-footed, and demure cadence. Steps are petite, gentle, and light on the floor plane. Hands are held softly near waist or dress with minimal, modest subtle sway. Torso upright and poised, long hair and dress ribbons floating softly.",
        "run_desc": "with a graceful light trot and petite brisk footsteps. Hands held lightly near waist for balance, short quick light strides low to ground. Hair and silk ribbons streaming softly with motion.",
    },
    "mature_woman": {
        "label": "MATURE WOMAN/LADY",
        "walk_desc": "with an elegant, poised, majestic, and dignified stride. Upright posture, level shoulders, composed graceful arm carriage close to sides. Hips and robes move with natural organic grace along the floor plane. Head and upper body rock-steady.",
        "run_desc": "with a purposeful, dignified, and athletic graceful brisk run. Upright stable core, arms bent at elbows pumping smoothly along ribs. Clean rhythmic strides, elegant momentum.",
    },
    "child": {
        "label": "CHILD/LITTLE KID",
        "walk_desc": "with playful, innocent, cheerful child steps. Short quick pitter-patter footsteps on floor plane. Arms swing naturally in innocent small arcs close to body. Head held high with curious cheerful energy, fully grounded balance.",
        "run_desc": "with an energetic scampering sprint. Quick pitter-patter footsteps low to ground, arms pumping close to body with playful cheerful momentum, body leaning slightly forward.",
    },
    "elderly": {
        "label": "ELDERLY/OLD MASTER",
        "walk_desc": "with a slow, deliberate, measured, and cautious pace. Body has a subtle weathered, slightly stooped posture of an elder artisan. Steps are short, grounded, and gentle. Arms hang relaxed close to sides or resting on a staff, minimal subtle pendulum motion strictly below waist.",
        "run_desc": "with a hurried shuffling jog in place. Body leans forward cautiously with lower center of gravity. Short quick footsteps staying low to floor plane (low stride height, NO jumping). Arms held close to midriff for balance with minimal compact motion.",
    },
}


def build_walk_templates(archetype: str) -> dict:
    info = ARCHETYPE_STYLES.get(archetype, ARCHETYPE_STYLES["young_male"])
    label = info["label"]
    desc = info["walk_desc"]
    return {
        "0": (
            f"4-second seamless loop 0° DIRECT FRONT VIEW WALK CYCLE ({label}). "
            f"Character walks in place facing DIRECTLY at camera {desc} "
            f"{MOTION_ANTI_GLITCH_LOCK} "
            "Seamless loop: first frame = last frame. Camera static. Solid green {chromaBgHex} background."
        ),
        "45": (
            f"4-second seamless loop 45° THREE-QUARTER WALK CYCLE ({label}). "
            f"Character walks in place at 45° angle facing bottom-left {desc} "
            f"No body turning. {MOTION_ANTI_GLITCH_LOCK} "
            "Seamless loop: first frame = last frame. Camera static. Solid green {chromaBgHex} background."
        ),
        "90": (
            f"4-second seamless loop 90° SIDE PROFILE WALK CYCLE ({label}). "
            f"Character walks in place in strict left side profile (9 o'clock) {desc} "
            f"Body stays strictly 90° side silhouette. {MOTION_ANTI_GLITCH_LOCK} "
            "Seamless loop: first frame = last frame. Camera static. Solid green {chromaBgHex} background."
        ),
        "135": (
            f"4-second seamless loop 135° BACK-LEFT WALK CYCLE ({label}). "
            f"Character walks in place viewed from behind at 135° angle {desc} "
            f"Body stays at 135° orientation. {MOTION_ANTI_GLITCH_LOCK} "
            "Seamless loop: first frame = last frame. Camera static. Solid green {chromaBgHex} background."
        ),
        "180": (
            f"4-second seamless loop 180° REAR VIEW WALK CYCLE ({label}). "
            f"Character walks in place facing directly away from camera {desc} "
            f"Symmetrical stepping. {MOTION_ANTI_GLITCH_LOCK} "
            "Seamless loop: first frame = last frame. Camera static. Solid green {chromaBgHex} background."
        ),
    }


def build_run_templates(archetype: str) -> dict:
    info = ARCHETYPE_STYLES.get(archetype, ARCHETYPE_STYLES["young_male"])
    label = info["label"]
    desc = info["run_desc"]
    return {
        "0": (
            f"4-second seamless loop 0° FRONT VIEW ATHLETIC RUN CYCLE ({label}). "
            f"Character jogs in place facing camera {desc} "
            f"{MOTION_ANTI_GLITCH_LOCK} "
            "Seamless loop: first frame = last frame. Camera static. Solid green {chromaBgHex} background."
        ),
        "45": (
            f"4-second seamless loop 45° THREE-QUARTER ATHLETIC RUN CYCLE ({label}). "
            f"Character jogs in place at 45° angle towards bottom-left {desc} "
            f"Dynamic momentum. {MOTION_ANTI_GLITCH_LOCK} "
            "Seamless loop: first frame = last frame. Camera static. Solid green {chromaBgHex} background."
        ),
        "90": (
            f"4-second seamless loop 90° SIDE PROFILE ATHLETIC RUN CYCLE ({label}). "
            f"Character jogs in place in pure left side profile (9 o'clock) {desc} "
            f"Body stays strictly 90°. {MOTION_ANTI_GLITCH_LOCK} "
            "Seamless loop: first frame = last frame. Camera static. Solid green {chromaBgHex} background."
        ),
        "135": (
            f"4-second seamless loop 135° BACK-LEFT ATHLETIC RUN CYCLE ({label}). "
            f"Character jogs in place viewed from behind at 135° angle {desc} "
            f"{MOTION_ANTI_GLITCH_LOCK} "
            "Seamless loop: first frame = last frame. Camera static. Solid green {chromaBgHex} background."
        ),
        "180": (
            f"4-second seamless loop 180° REAR VIEW ATHLETIC RUN CYCLE ({label}). "
            f"Character jogs in place facing away from camera {desc} "
            f"{MOTION_ANTI_GLITCH_LOCK} "
            "Seamless loop: first frame = last frame. Camera static. Solid green {chromaBgHex} background."
        ),
    }


WALK_PROMPT_TEMPLATES = build_walk_templates("young_male")
RUN_PROMPT_TEMPLATES = build_run_templates("young_male")

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


def detect_motion_archetype(customizer: dict = None) -> str:
    """Classify character archetype into: 'elderly', 'child', 'maiden', 'mature_woman', 'young_male'."""
    if not customizer:
        return "young_male"
    text = " ".join([
        str(customizer.get("age", "")),
        str(customizer.get("gender", "")),
        str(customizer.get("bodyType", "")),
        str(customizer.get("characterName", "")),
        str(customizer.get("personality", "")),
    ]).lower()

    # Strip 'years old' / 'year-old' so it doesn't falsely trigger 'old'
    text_no_yo = re.sub(r"\byears?\s*old\b|\byear-old\b", "", text)

    # 1. Elderly / Senior (50-80+, lão, ông/bà lão)
    elderly_kw = ["elder", "senior", "lão", "bà lão", "ông lão", "thợ già", "50", "55", "60", "65", "70", "75", "80"]
    if any(k in text_no_yo for k in elderly_kw) or re.search(r"\b(old man|old woman|elderly)\b", text):
        return "elderly"

    # 2. Child / Kid (3-12 years old, trẻ con, thiếu nhi, bé)
    child_kw = ["toddler", "trẻ con", "thiếu nhi", "bé trai", "bé gái", "tiểu đồng", "little kid", "child", "little boy", "little girl"]
    if any(k in text_no_yo for k in child_kw) or re.search(r"\b(child|kid)\b", text_no_yo):
        return "child"
    if re.search(r"\b([3-9]|1[0-2])\s*(?:years?|tuổi|t)\b", text):
        return "child"

    # 3. Female classifications (Maiden vs Mature Woman)
    is_female = any(k in text for k in ["female", "nữ", "gái", "maiden", "woman"])
    if is_female:
        mature_kw = ["mature", "lady", "queen", "empress", "phụ nữ", "quý bà", "quý cô", "hoàng hậu", "nữ tướng", "mẫu thân"]
        if any(k in text for k in mature_kw):
            return "mature_woman"
        if re.search(r"\b(2[5-9]|[34][0-9])\b", text):
            return "mature_woman"
        return "maiden"

    return "young_male"


def get_action_templates(action_key: str, customizer: dict = None) -> dict:
    """Get prompt templates dict for an action key (walk, idle, run, etc.), tailored to archetype."""
    archetype = detect_motion_archetype(customizer)
    if action_key == "walk":
        return build_walk_templates(archetype)
    if action_key == "run":
        return build_run_templates(archetype)
    return ACTION_TEMPLATES.get(action_key, {})
