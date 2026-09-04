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
    "hairStyleColor": "Long silky platinum white hair with delicate silver lotus hairpin and silk ribbons",
    "outfitDescription": "Xianxia flowing silk daoist robes with wide sleeves, floating ribbons, flat cloth lotus shoes with zero heels",
    "primaryColor": "Pure White & Soft Pale Cyan",
    "accentColor": "Soft Silver & Lilac Purple",
    "skinTone": "Fair natural peach skin tone seamlessly matching neck and hands",
    "weaponType": "None (empty hands, pure martial arts)",
    "spellElement": "Soft wind aura",
    "chromaBgHex": "#00FF00",
}

# ─── Stage 1+2: 5 Body Angles Prompts ─────────────────────────
# Stage 1: angle "0" (text_to_image, no reference)
# Stage 2: angles "45","90","135","180" (image_to_image, ref = 0° image)

ANGLE_PROMPT_TEMPLATES = {
    "0": (
        "MASTER CHARACTER DESIGN — 2D XIANXIA CHIBI — 0° DIRECT FRONT VIEW\n"
        "CAMERA & POSE: Character MUST face 100% DIRECTLY forward at camera (strict 0.0° front view). "
        "Head facing 100% straight forward towards viewer. NO head turn, NO head tilt, NO 3/4 angle. "
        "Symmetrical standing pose, torso upright, both shoulders perfectly horizontal and level, both arms relaxed at sides, empty hands. "
        "Both feet planted parallel and pointing directly forward towards viewer (12 o'clock). "
        "NATURAL FABRIC DRAPE UNDER GRAVITY: Robes, sleeves, and hems hang straight down naturally under calm gravity. Strictly NO wind blowing, NO billowing fabric, NO flapping hems, NO flying coat tails. "
        "CRITICAL — BLANK FACELESS HEAD & UNIFORM SKIN TONE: Completely BLANK, SMOOTH, FEATURELESS face surface. "
        "NO eyes, NO eyebrows, NO nose, NO mouth. Facial skin color MUST seamlessly and uniformly match the neck, ears, and hands ({skinTone}) with 100% consistency across all angles. "
        "Clean flat cel-shaded anime skin, strictly zero facial features. "
        "CRITICAL — ZERO WEAPONS OR PROPS: The character carries NO weapons, NO swords on back or waist, NO musical instruments, NO props. Hands are empty and resting naturally. "
        "FABRIC TEXTURE & LIGHTING: Strictly flat matte fabric texture, zero metallic sheen, zero silvery gloss, zero specular reflections, zero satin sheen. Clean natural cel-shading, harmonious elegant palette. Strictly ZERO neon lighting, ZERO harsh glowing rim lights. "
        "CHARACTER: Genre: {style}. Age: {age}. Gender: {gender}. "
        "HAIR: {hairStyleColor}. OUTFIT: {outfitDescription}. "
        "Colors: {primaryColor}. Accents: {accentColor}. Skin: {skinTone}. "
        "STYLE: Pure flat 2D anime/chibi illustration, bold clean linework, flat cel-shaded coloring. "
        "BACKGROUND: Solid chroma-key green {chromaBgHex}. Centered, full body visible."
    ),
    "45": (
        "2D Xianxia anime chibi character sprite — TRUE THREE-QUARTER VIEW 45 DEGREES.\n"
        "SINGLE MASTER REFERENCE: Strictly reference ONLY the 0° front-view master image.\n"
        "CRITICAL SCALE & STATURE LOCK: Maintain the EXACT SAME TALL, SLENDER, ATHLETIC HEIGHT AND FULL-BODY PROPORTIONS as Reference 0° (~88% vertical canvas height from top of head/coronet to bottom of boots). "
        "STRICTLY ZERO SHRINKING, ZERO ZOOM-OUT, ZERO COMPRESSION OF STATURE. Identical camera framing and tall stature.\n"
        "POSE & CAMERA: Character body and head are turned 45 degrees towards the left (facing diagonal 10 o'clock direction). "
        "CRITICAL: STRICTLY FORBIDDEN to face the camera. ZERO direct front view. "
        "BODY ROTATION: The chest and torso are rotated 45 degrees away from camera. "
        "The character's left shoulder is forward in foreground; right shoulder is pulled back in depth and recessed. "
        "The neckline and waist sash ornament are displaced to the left diagonal side, NOT centered. "
        "NATURAL FABRIC DRAPE UNDER GRAVITY: Robe hems and sleeves hang straight down naturally under calm gravity. Strictly NO wind blowing, NO billowing fabric, NO flapping hems, NO flying coat tails. "
        "Left leg steps forward, right leg placed behind in asymmetrical depth. Both feet point diagonally at 45 degrees (10 o'clock). "
        "HEAD ROTATION: Head is visibly turned 45 degrees to the left showing three-quarter facial outline and left ear contour. "
        "CRITICAL — BLANK FACELESS HEAD & UNIFORM NATURAL SKIN: Completely BLANK, SMOOTH, FEATURELESS face surface (NO eyes, NO eyebrows, NO nose, NO mouth). "
        "Facial skin color seamlessly matches neck and body skin tone ({skinTone}). Strictly ZERO shiny white enamel mask, ZERO face-neck color mismatch. "
        "CRITICAL — ZERO WEAPONS OR PROPS: Character carries NO weapons, NO swords, NO props. Both hands empty. "
        "LIGHTING: Natural clean flat cel-shading, strictly ZERO neon lighting, ZERO glowing rim bleed. "
        "IDENTITY LOCK: Match reference character (hairstyle {hairStyleColor}, outfit {outfitDescription}, colors {primaryColor}, accents {accentColor}, skin tone {skinTone}). "
        "STYLE: Pure flat 2D anime chibi sprite illustration, clean linework, flat cel-shaded coloring. "
        "BACKGROUND: Solid chroma-key green {chromaBgHex}. Centered, full body visible."
    ),
    "90": (
        "2D Xianxia anime chibi character sprite — STRICT 90-DEGREE LEFT SIDE PROFILE VIEW.\n"
        "MULTI-REFERENCE INTERPOLATION: Interpolate between Reference 0° (front view) and Reference 45° (three-quarter view) for complete continuity and detail retention.\n"
        "CRITICAL SCALE & STATURE LOCK: Maintain the EXACT SAME TALL, SLENDER, ATHLETIC HEIGHT AND FULL-BODY PROPORTIONS as References [0°, 45°] (~88% vertical canvas height). "
        "STRICTLY ZERO SHRINKING, ZERO ZOOM-OUT, ZERO STATURE COMPRESSION. Identical camera framing and tall stature.\n"
        "PRESERVE 100% INTRICATE DETAILS: Preserve every intricate detail from references: sleeve embroidery, coat hem patterns, waist belt structure.\n"
        "CAMERA PERSPECTIVE: Camera is positioned directly at the character left flank (pure profile view). "
        "The character faces 100% directly towards the LEFT edge of the frame (9 o'clock). "
        "BODY SILHOUETTE: Only the LEFT side of the body is visible. Slender narrow vertical side profile. "
        "The LEFT ARM hangs in front, the RIGHT ARM and right shoulder are 100% COMPLETELY HIDDEN behind the torso. "
        "NATURAL FABRIC DRAPE UNDER GRAVITY: The robe and coat hang straight down vertically along the body under calm gravity. Strictly NO wind blowing, NO billowing fabric, NO coat tails flying backward. "
        "Chest faces left (pure side profile, NO front chest visible, NO three-quarter angle). "
        "Both feet point directly towards the left edge of the screen. "
        "CRITICAL — BLANK FACELESS HEAD & UNIFORM NATURAL SKIN: Smooth blank featureless face in pure side profile silhouette (NO eyes/nose/mouth). "
        "Facial skin color seamlessly and uniformly matches neck ({skinTone}). Strictly ZERO shiny white mask. "
        "CRITICAL — ZERO WEAPONS OR PROPS: NO weapons, NO swords on back or waist, NO props. Empty hands. "
        "LIGHTING: Natural flat cel-shading, strictly ZERO neon lighting, ZERO harsh glowing rim reflections. "
        "STYLE: Pure flat 2D anime chibi illustration, bold clean linework, flat cel-shaded colors. "
        "BACKGROUND: Solid chroma-key green {chromaBgHex}. Centered, full body visible."
    ),
    "135": (
        "ROTATE CHARACTER 135° — BACK-LEFT THREE-QUARTER VIEW — 2D XIANXIA CHIBI\n"
        "Use the 0° and 180° references as ABSOLUTE IDENTITY SOURCE. "
        "CRITICAL ROTATION: Character viewed from BEHIND, angled 45° towards the LEFT. "
        "Camera is BEHIND the character, slightly to the RIGHT. "
        "The character's BACK is mostly visible, tilted to show the left-back side. "
        "Back of hairstyle, hair ornaments, back of robes visible. "
        "WAIST BELT REAR: For male characters: strictly smooth continuous flat belt band around back with ZERO bow, ZERO ribbon knot. "
        "LEFT shoulder is closer to camera (back-left perspective); right shoulder is angled away in depth. "
        "NATURAL FABRIC DRAPE: Fabric hangs straight down under gravity, strictly NO wind blowing, NO billowing flaps. "
        "Both legs and flat cloth shoes angled away from camera towards the left. "
        "CRITICAL — ZERO WEAPONS OR PROPS: NO swords on back or waist, NO weapons, NO props. Clean back of robe. "
        "LIGHTING & SKIN: Skin tone {skinTone} uniformly matching. Strictly ZERO neon glow or edge reflections. "
        "IDENTITY LOCK: Exact same hairstyle back details, outfit design, colors identical to reference. "
        "STYLE: Pure flat 2D anime illustration, bold linework, flat colors. "
        "BACKGROUND: Solid chroma-key green {chromaBgHex}. Centered, full body visible."
    ),
    "180": (
        "ROTATE CHARACTER 180° — PERFECT REAR VIEW — 2D XIANXIA CHIBI\n"
        "Use the 0° front-view reference image as ABSOLUTE IDENTITY SOURCE. "
        "CRITICAL ROTATION: Character faces 100% AWAY from camera (180° rear view). "
        "Full back of hairstyle and ornaments symmetrically displayed. "
        "Spine vertical, full back of robes facing camera. "
        "WAIST BELT REAR: For male characters: strictly smooth continuous flat belt band around back with ZERO bow, ZERO ribbon knot. "
        "NATURAL FABRIC DRAPE: Hems and sleeves hang straight down under gravity, strictly NO wind blowing, NO billowing coat tails. "
        "Both legs straight and symmetrical, heels facing camera, feet pointing away in flat cloth shoes. "
        "CRITICAL — ZERO WEAPONS OR PROPS: NO swords on back, NO weapons, NO props. Pure clean robe back. "
        "LIGHTING: Natural clean flat colors, strictly ZERO neon lighting. "
        "IDENTITY LOCK: Exact same hair color, proportions, costume rear details. Skin tone {skinTone}. "
        "STYLE: Pure flat 2D anime illustration, bold linework, flat colors. "
        "BACKGROUND: Solid chroma-key green {chromaBgHex}. Centered, full body visible."
    ),
}

# ─── Stage 3: Action Video Prompts (4s Seamless Loop) ──────────
# Each action has 5 angle variants. The pipeline uses the angle's
# reference image as BOTH start frame AND end frame for seamless loop.

MOTION_ANTI_GLITCH_LOCK = (
    "CRITICAL MOTION STABILITY CONSTRAINTS (STRICT ANTI-GLITCH LOCK): "
    "Movement is natural, authentic, and dignified with ZERO exaggerated stiff posturing or forced posing. "
    "Hands and forearms strictly stay below chest level at all times, moving ONLY in a narrow organic pendulum arc parallel to hips. "
    "STRICTLY ZERO wild arm flailing, ZERO arm waving, ZERO hand gestures, ZERO dancing or acrobatic posing. "
    "Hair is realistically rooted to the scalp with soft organic secondary motion following body inertia and breeze; STRICTLY ZERO rigid wig swaying or detached floating hair. "
    "Feet remain grounded in clean stride cycle, STRICTLY ZERO hopping, ZERO bouncing up and down, ZERO airborne jumping, ZERO floating. "
    "At angled perspectives (45° and 135°), legs and feet stride strictly along the true diagonal vector aligned with body orientation; STRICTLY ZERO sideways crab-walking or lateral sliding. "
    "Torso, shoulders, and head remain rock-steady and level with ZERO torso twisting, erratic bobbing, or body contortion. "
    "Faceless blank mannequin head remains completely smooth with natural skin color seamlessly matching neck, ZERO facial expressions, mouth opening, or morphing. "
    "STRICTLY ZERO weapons, swords, or props. STRICTLY ZERO neon glow, neon reflections, or glowing edges."
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
            f"STRIDE DIRECTION LOCK: Legs, feet, and stride cycle track strictly forward along the 45-degree diagonal trajectory aligned with torso. Strictly ZERO sideways crab-walking or lateral sliding. No body turning. {MOTION_ANTI_GLITCH_LOCK} "
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
            f"STRIDE DIRECTION LOCK: Legs and feet step strictly along the 135-degree diagonal axis aligned with body orientation. Strictly ZERO crab-walking. Body stays at 135° orientation. {MOTION_ANTI_GLITCH_LOCK} "
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
        "4-second seamless loop IDLE STANDING (0° direct front view). "
        "Character stands completely still and motionless facing camera. "
        "STRICT ARM & HAND IMMOBILITY LOCK: Both arms hang completely relaxed straight down along sides and DO NOT MOVE. "
        "Hands and fingers remain completely still and motionless at sides. "
        "STRICTLY FORBIDDEN TO MOVE ARMS, STRICTLY ZERO ARM RAISING, ZERO HAND MOVEMENT, ZERO TOUCHING CHEST, ZERO RAISING PALMS. "
        "TRANQUIL ANIME BREEZE (ULTRA-GENTLE MICRO-MOTION): A peaceful, whisper-soft anime breeze tenderly caresses the character. "
        "Silky long hair hangs naturally downward under gravity, resting peacefully with only the hair tips drifting very softly, slowly, and subtly like a serene anime scene. "
        "STRICTLY ZERO strong wind, ZERO hair fluttering, ZERO whipping strands. "
        "SOFT SILK FABRIC DRIFTING: Wide sleeve borders and lower robe hems drape naturally under calm gravity, wafting with slow, whisper-soft, graceful silk softness and tender, gentle breathing micro-sway. "
        "Fabric looks exceptionally soft, supple, and lightweight without any stiff flapping or aggressive ripples. "
        "Any hair ornaments or fixed accessories sway very subtly and minimally with the cloth. "
        "Body, torso, head, and feet remain completely static and anchored to the floor plane. "
        "Faceless blank mannequin head remains completely smooth with uniform skin tone seamlessly matching neck. "
        "STRICTLY ZERO hopping, ZERO bouncing, ZERO body turning. STRICTLY ZERO weapons or props. "
        "Static camera, seamless loop: first frame = last frame. Camera static. Solid green {chromaBgHex} background."
    ),
    "45": (
        "4-second seamless loop IDLE STANDING (45° three-quarter view). "
        "Character stands completely still and motionless at 45° angle facing diagonal 10 o'clock. "
        "STRICT ARM & HAND IMMOBILITY LOCK: Both arms hang completely relaxed straight down at sides and DO NOT MOVE. "
        "Hands and fingers remain completely motionless at sides. STRICTLY ZERO ARM RAISING, ZERO TOUCHING CHEST. "
        "TRANQUIL ANIME BREEZE (ULTRA-GENTLE MICRO-MOTION): A peaceful, whisper-soft anime breeze tenderly caresses the character. "
        "Silky long hair hangs naturally downward with only the hair tips drifting very softly and slowly like a serene anime scene. "
        "STRICTLY ZERO strong wind, ZERO hair fluttering. "
        "SOFT SILK FABRIC DRIFTING: Wide sleeve cuffs and lower coat hem borders drape softly under gravity, wafting with slow, whisper-soft, graceful silk softness and tender micro-sway. "
        "Hair ornaments sway subtly with the gentle breeze. Torso, head, and feet remain rock-steady and grounded. "
        "Static camera, seamless loop: first frame = last frame. Camera static. Solid green {chromaBgHex} background."
    ),
    "90": (
        "4-second seamless loop IDLE STANDING (90° side profile). "
        "Character stands completely still and motionless in pure side silhouette (9 o'clock). "
        "STRICT ARM & HAND IMMOBILITY LOCK: Left arm hangs straight down relaxed at side with zero movement. Strictly zero arm lifting. "
        "TRANQUIL ANIME BREEZE (ULTRA-GENTLE MICRO-MOTION): Faint, whisper-soft anime breeze tenderly wafts the hair ends and lower coat hem backward with peaceful, very soft, slow floating micro-motion. "
        "STRICTLY ZERO strong wind, hair stays calm and settled with only delicate drift at the tips. Robe fabric hangs gracefully with soft silk suppleness. "
        "Hairpin and ornaments sway subtly with the soft breeze. Body completely static. "
        "Static camera, seamless loop: first frame = last frame. Camera static. Solid green {chromaBgHex} background."
    ),
    "135": (
        "4-second seamless loop IDLE STANDING (135° back-left view). "
        "Character stands completely still and motionless viewed from behind at 135° angle. "
        "STRICT ARM & BODY IMMOBILITY LOCK: Body and arms remain completely motionless. Zero arm raising. "
        "TRANQUIL ANIME BREEZE (ULTRA-GENTLE MICRO-MOTION): Faint, whisper-soft anime breeze tenderly caresses long flowing back hair and coat hem. "
        "Hair cascades naturally down the back, with only hair tips drifting softly and slowly in the air like a peaceful anime scene. "
        "Robe hems and fabric waft softly with delicate silk suppleness under gravity. "
        "Flat continuous waist belt remains clean against back. Hair ornaments sway subtly. "
        "Static camera, seamless loop: first frame = last frame. Camera static. Solid green {chromaBgHex} background."
    ),
    "180": (
        "4-second seamless loop IDLE STANDING (180° rear view). "
        "Character stands completely still and motionless facing directly away from camera. "
        "STRICT ARM & BODY IMMOBILITY LOCK: Symmetrical grounded posture, heels facing camera, arms motionless at sides. "
        "TRANQUIL ANIME BREEZE (ULTRA-GENTLE MICRO-MOTION): Whisper-soft anime breeze gently caresses back robe fabric, wide sleeves, and silky long hair cascading down the spine. "
        "Hair rests calm and settled along the spine with only the tips floating with subtle, slow, tender micro-motion. "
        "STRICTLY ZERO strong wind. Robe fabric drapes naturally with soft, graceful, lightweight silk softness. "
        "Hair ornaments sway subtly with the soft breeze. Flat continuous waist belt remains completely smooth without any ties or bows. "
        "Static camera, seamless loop: first frame = last frame. Camera static. Solid green {chromaBgHex} background."
    ),
}

ATTACK_PROMPT_TEMPLATES = {
    "0": (
        "4-second seamless loop MARTIAL ARTS PALM STRIKE COMBO (0° front view). "
        "Character performs an elegant empty-handed martial arts palm strike sequence facing camera. "
        "Fluid flowing arm extensions, palm thrusts, and qigong hand movements in rhythmic cadence. "
        "Hair and robe sleeves move gracefully with momentum. Strictly ZERO weapons. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "45": (
        "4-second seamless loop MARTIAL ARTS PALM STRIKE COMBO (45° three-quarter view). "
        "Character performs empty-handed martial arts palm strike combo at 45° angle. "
        "Diagonal martial palm strikes with fluid body balance. Empty hands. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "90": (
        "4-second seamless loop MARTIAL ARTS PALM STRIKE COMBO (90° side profile). "
        "Character performs rhythmic horizontal palm thrust and retraction in side profile. "
        "Empty hands, fluid martial extension. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "135": (
        "4-second seamless loop MARTIAL ARTS PALM STRIKE COMBO (135° back-left view). "
        "Character performs martial arts palm strike sequence viewed from behind at 135° angle. "
        "Shoulders and robe sleeves turn rhythmically with each strike. Empty hands. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "180": (
        "4-second seamless loop MARTIAL ARTS PALM STRIKE COMBO (180° rear view). "
        "Character performs rhythmic martial arts palm sequence facing away from camera. "
        "Arm extensions and flowing robe sleeves visible from behind. Empty hands. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
}

DEFEND_PROMPT_TEMPLATES = {
    "0": (
        "4-second seamless loop MARTIAL DEFENSIVE GUARD STANCE (0° front view). "
        "Character holds an empty-handed martial defensive stance facing camera, palms raised in balanced guard posture. "
        "Subtle breathing, grounded poise, sleeves fluttering gently. Strictly ZERO weapons. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "45": (
        "4-second seamless loop MARTIAL DEFENSIVE GUARD STANCE (45° three-quarter). "
        "Character holds empty-handed defensive guard stance at 45° angle. "
        "Poised martial balance, palms up in guard posture. Empty hands. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "90": (
        "4-second seamless loop MARTIAL DEFENSIVE GUARD STANCE (90° side profile). "
        "Character holds defensive martial stance in side profile. "
        "Arms raised in protective martial guard posture, body braced. Empty hands. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "135": (
        "4-second seamless loop MARTIAL DEFENSIVE GUARD STANCE (135° back-left). "
        "Character holds martial guard viewed from behind at 135° angle. "
        "Back posture grounded, defensive ready stance. Empty hands. "
        "Seamless loop: first frame = last frame. "
        "Camera static. Solid green {chromaBgHex} background."
    ),
    "180": (
        "4-second seamless loop MARTIAL DEFENSIVE GUARD STANCE (180° rear view). "
        "Character holds martial defensive stance facing away from camera. "
        "Balanced empty-handed stance visible from behind. "
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
