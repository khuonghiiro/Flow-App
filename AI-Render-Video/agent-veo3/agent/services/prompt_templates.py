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
        "CRITICAL ANATOMICAL PROPORTION & SCALE LOCK: Mature stylized semi-chibi anime sprite proportion (~4.8 to 5.0 heads tall, full-body vertical height occupies 85-88% canvas height). "
        "Head size is proportionate and balanced with shoulders; strictly ZERO oversized giant chibi head, ZERO shrunken tiny torso, ZERO bobblehead deformity. "
        "Torso and limbs have clear anatomical structure, slender waist, and long graceful legs grounded on floor plane.\n"
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
        "ROTATE THE CHARACTER 45 DEGREES — THREE-QUARTER VIEW — 2D Xianxia anime chibi sprite.\n"
        "FIRST AND MOST IMPORTANT INSTRUCTION: ROTATE the character's ENTIRE BODY 45 degrees to face 10 o'clock (upper-left diagonal). "
        "The character is NO LONGER facing the camera. The character is TURNED AWAY from the viewer by 45 degrees. "
        "THIS IS NOT A FRONT VIEW. STRICTLY FORBIDDEN to keep the body facing forward. STRICTLY FORBIDDEN to keep symmetrical front-facing pose.\n"
        "VISIBLE ROTATION PROOF — These elements MUST be visible to confirm 45-degree rotation: "
        "(1) The LEFT SHOULDER is pushed FORWARD toward the viewer, the RIGHT SHOULDER recedes BACKWARD behind the body. "
        "(2) The collar seam and robe crossover are shifted to the LEFT side of the torso, NOT centered. "
        "(3) The waist medallion/belt buckle is visible on the LEFT hip area, NOT in the center of the body. "
        "(4) The LEFT foot is stepped FORWARD, the RIGHT foot is stepped BACK in depth. Both feet point towards 10 o'clock. "
        "(5) The viewer can see part of the LEFT side of the character's body and a hint of the back of the RIGHT shoulder. "
        "If the collar, belt, and feet are all centered and symmetrical, the image is WRONG and must be rejected.\n"
        "CRITICAL SCALE & STATURE LOCK: Maintain the EXACT SAME TALL, SLENDER, ATHLETIC HEIGHT AND FULL-BODY PROPORTIONS as Reference 0° (~88% vertical canvas height, ~4.8-5.0 heads tall). "
        "NATURAL FABRIC DRAPE UNDER GRAVITY: Robe hems and sleeves hang straight down naturally under calm gravity. Strictly NO wind blowing, NO billowing fabric. "
        "CRITICAL — BLANK FACELESS HEAD: Smooth blank featureless face (NO eyes, NO nose, NO mouth). Skin matches neck ({skinTone}). "
        "CRITICAL — ZERO WEAPONS OR PROPS: NO weapons, NO swords, NO props. Both hands empty. "
        "LIGHTING: Natural clean flat cel-shading, strictly ZERO neon lighting. "
        "IDENTITY LOCK: Match reference character (hairstyle {hairStyleColor}, outfit {outfitDescription}, colors {primaryColor}, accents {accentColor}). "
        "STYLE: Pure flat 2D anime chibi sprite illustration, clean linework, flat cel-shaded coloring. "
        "BACKGROUND: Solid chroma-key green {chromaBgHex}. Centered, full body visible."
    ),
    "90": (
        "PURE 90-DEGREE SIDE PROFILE VIEW (FACING LEFT 9 O'CLOCK) — 2D Xianxia anime chibi sprite.\n"
        "MANDATORY 90-DEGREE SIDE PROFILE ROTATION: The character stands in a STRICT 90-DEGREE SIDE PROFILE facing directly left towards 9 o'clock. "
        "ONLY THE LEFT PROFILE is visible. The character's left arm and left shoulder face the viewer; the right shoulder and right arm are 100% COMPLETELY HIDDEN AND OCCLUDED behind the torso. "
        "STRICTLY FORBIDDEN to face the camera. STRICTLY FORBIDDEN to show front chest or three-quarter angle. Narrow vertical side profile silhouette. "
        "Both feet point directly towards the left edge of the screen (9 o'clock). "
        "Head is in pure 90-degree side profile showing one ear, side jawline, and nose contour. "
        "MULTI-REFERENCE INTERPOLATION: Interpolate between Reference 0° (front view) and Reference 45° (three-quarter view) for complete continuity of colors, robes, and layered sash. "
        "CRITICAL SCALE & STATURE LOCK: Maintain the EXACT SAME TALL, SLENDER, ATHLETIC HEIGHT AND FULL-BODY PROPORTIONS as References [0°, 45°] (~88% vertical canvas height, ~4.8-5.0 heads tall). "
        "STRICTLY ZERO SHRINKING, ZERO ZOOM-OUT, ZERO STATURE COMPRESSION. Identical camera framing and tall stature. "
        "PRESERVE 100% INTRICATE DETAILS: Preserve every intricate detail from references: sleeve embroidery, coat hem patterns, layered waist belt structure. "
        "NATURAL FABRIC DRAPE UNDER GRAVITY: The robes, sleeves, and coat hang straight down vertically along the body under calm gravity. Strictly NO wind blowing, NO billowing fabric, NO coat tails flying backward. "
        "CRITICAL — BLANK FACELESS HEAD & UNIFORM NATURAL SKIN: Smooth blank featureless face in pure side profile silhouette (NO eyes/nose/mouth). "
        "Facial skin color seamlessly and uniformly matches neck ({skinTone}). Strictly ZERO shiny white mask. "
        "CRITICAL — ZERO WEAPONS OR PROPS: NO weapons, NO swords on back or waist, NO props. Empty hands. "
        "LIGHTING: Natural flat cel-shading, strictly ZERO neon lighting, ZERO harsh glowing rim reflections. "
        "STYLE: Pure flat 2D anime chibi illustration, bold clean linework, flat cel-shaded colors. "
        "BACKGROUND: Solid chroma-key green {chromaBgHex}. Centered, full body visible."
    ),
    "135": (
        "2D XIANXIA ANIME CHIBI CHARACTER SPRITE — TRUE 135-DEGREE BACK-LEFT THREE-QUARTER ANGLE.\n"
        "CRITICAL POSE COPY FROM REFERENCE 1 (MANNEQUIN 135° POSE GUIDE):\n"
        "The character's entire body stance MUST strictly replicate the exact 135-degree posture in Reference 1:\n"
        "- Torso and spine are rotated at a 135-degree angle (facing diagonally away to the 8 o'clock direction).\n"
        "- Left shoulder, left arm, and left hip are prominent and close in the foreground.\n"
        "- Right shoulder and right arm are further away in depth, partially occluded by the angled torso.\n"
        "- FEET AND LEGS POSE: Strictly copy Reference 1. The left foot is rotated into profile facing left (9 o'clock). The right foot is angled diagonally away into depth. Both legs are clearly angled in three-quarter perspective.\n"
        "- STRICTLY FORBIDDEN: Symmetrical 180-degree rear-facing body, symmetrical shoulders, or symmetrical feet. The torso and feet MUST NOT be facing 180 degrees.\n"
        "COSTUME & HAIR IDENTITY (FROM REFERENCE 2):\n"
        "- Character wears the outfit ({outfitDescription}) with colors ({primaryColor}) and accents ({accentColor}).\n"
        "- Hairstyle: {hairStyleColor} viewed from the 135-degree rear-left angle.\n"
        "- Waist belt rear rule: {waistRearMotionLock} (viewed at 45-degree angled offset).\n"
        "- Footwear: flat cloth daoist shoes/boots (strictly flat, zero heels).\n"
        "- Face/Head: Side profile of fair natural skin ({skinTone}) cheek, jawline, delicate ear, and sideburns viewed from behind-left.\n"
        "SCALE & FABRIC PHYSICS:\n"
        "- Maintain mature semi-chibi anime sprite proportion (~4.8-5.0 heads tall, occupies 85-88% canvas height).\n"
        "- Natural fabric drape under calm gravity, strictly zero fake wind blowing.\n"
        "STYLE & BG:\n"
        "- Pure flat 2D anime chibi sprite illustration, bold clean linework, flat cel-shaded coloring. Genre: {style}.\n"
        "- Solid chroma-key green {chromaBgHex} background. Full body centered from head to feet."
    ),
    "180": (
        "ROTATE CHARACTER 180° — PERFECT DIRECT REAR VIEW — 2D XIANXIA CHIBI\n"
        "SINGLE MASTER REFERENCE: Use the 0° front-view master reference image as ABSOLUTE IDENTITY SOURCE to reconstruct the complete back view. "
        "CRITICAL ROTATION: Character faces 100% DIRECTLY AWAY from camera (strict 180.0° rear view). "
        "Full back of hairstyle, topknot bun, and hair ornaments symmetrically displayed, perfectly mirroring the crown and ornaments from 0°. "
        "Spine vertical, full back of robes facing camera with bilateral symmetry. "
        "WAIST BELT REAR: For male characters: strictly smooth continuous flat belt band around back with ZERO bow, ZERO ribbon knot. For female characters: follow the character's outfit description (either elegant butterfly bow with hanging silk ribbons or flat belt band as specified in outfit). "
        "NATURAL FABRIC DRAPE: Hems and sleeves hang straight down naturally under calm gravity, strictly NO wind blowing, NO billowing coat tails. "
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
        "4-second seamless loop IDLE BREATHING SPRITE (0° direct front view). "
        "Character stands completely grounded, facing camera in a calm, dignified standing stance. "
        "NATURAL SUBTLE BREATHING MOTION: Extremely gentle, rhythmic rise and fall of chest and shoulders simulating natural calm breathing (one smooth, relaxed breathing cycle across 4 seconds). "
        "CALM STATIC FABRIC UNDER GRAVITY: Robe hems, sleeves, and sashes hang naturally downward under calm gravity without artificial wind. "
        "STRICTLY FORBIDDEN: Any wind blowing, fabric fluttering, cloth warping, or unnatural ripples. Robes remain calm, settled, and stable. "
        "STRICT ARM & HAND IMMOBILITY: Both arms hang completely relaxed straight down along sides and DO NOT MOVE. "
        "Hands and fingers remain completely motionless at sides. STRICTLY ZERO ARM RAISING, ZERO HAND MOVEMENT, ZERO TOUCHING CHEST. "
        "Silky hair rests naturally and calmly along shoulders. Feet remain firmly planted on the ground plane. "
        "Faceless blank mannequin head remains completely smooth with uniform natural skin tone. "
        "Static camera, seamless loop: first frame = last frame. Solid green {chromaBgHex} background."
    ),
    "45": (
        "4-second seamless loop IDLE BREATHING SPRITE (45° three-quarter view). "
        "Character stands completely still and grounded at 45° angle facing diagonal 10 o'clock. "
        "NATURAL SUBTLE BREATHING MOTION: Extremely gentle, rhythmic rise and fall of chest and shoulders simulating natural calm breathing across 4 seconds. "
        "CALM STATIC FABRIC UNDER GRAVITY: Robes and sleeves hang straight down under natural gravity. "
        "STRICTLY FORBIDDEN: Wind blowing, fabric fluttering, or cloth warping. Robe fabric remains calm, settled, and static. "
        "STRICT ARM & BODY IMMOBILITY: Arms hang motionless at sides. Torso, head, and feet remain rock-steady on floor plane. "
        "Hair rests calm and settled along body. "
        "Static camera, seamless loop: first frame = last frame. Solid green {chromaBgHex} background."
    ),
    "90": (
        "4-second seamless loop IDLE BREATHING SPRITE (90° side profile). "
        "Character stands completely grounded in pure side profile (9 o'clock). "
        "NATURAL SUBTLE BREATHING MOTION: Subtle, peaceful expansion and relaxing of chest simulating gentle natural breathing cycle. "
        "CALM STATIC FABRIC UNDER GRAVITY: Robes hang vertically along body silhouette under natural gravity. STRICTLY ZERO wind fluttering. "
        "STRICT IMMOBILITY: Left arm hangs straight down motionless. Feet anchored to floor plane. Body silhouette rock-steady. "
        "Static camera, seamless loop: first frame = last frame. Solid green {chromaBgHex} background."
    ),
    "135": (
        "4-second seamless loop IDLE BREATHING SPRITE (135° back-left view). "
        "Character stands completely grounded viewed from behind at 135° angle. "
        "NATURAL SUBTLE BREATHING MOTION: Subtle, natural breathing rhythm of back and shoulders across 4 seconds. "
        "CALM STATIC FABRIC UNDER GRAVITY: Back robe fabric, sleeves, and sash hang naturally downward under calm gravity. STRICTLY ZERO wind flapping or warping. "
        "{waistRearMotionLock} "
        "Hair rests calm and settled along the back. Arms motionless at sides. Feet firmly planted. "
        "Static camera, seamless loop: first frame = last frame. Solid green {chromaBgHex} background."
    ),
    "180": (
        "4-second seamless loop IDLE BREATHING SPRITE (180° rear view). "
        "Character stands completely still facing directly away from camera in symmetrical rear stance. "
        "NATURAL SUBTLE BREATHING MOTION: Subtle, calm breathing rhythm of upper back and shoulders across 4 seconds. "
        "CALM STATIC FABRIC UNDER GRAVITY: Back of robes and long hair hang straight down naturally under gravity. STRICTLY ZERO wind blowing, ZERO fabric flutter. "
        "{waistRearMotionLock} "
        "Both arms motionless at sides. Heels planted firmly on ground plane. "
        "Static camera, seamless loop: first frame = last frame. Solid green {chromaBgHex} background."
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
    if "waistRearMotionLock" not in c:
        if c.get("gender") == "female":
            c["waistRearMotionLock"] = "Rear waist butterfly bow and cascading ribbons drift subtly with the gentle breeze."
        else:
            c["waistRearMotionLock"] = "Flat continuous waist belt remains completely smooth without any ties or bows."
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
