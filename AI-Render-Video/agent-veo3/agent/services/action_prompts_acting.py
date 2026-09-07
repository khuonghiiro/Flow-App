"""Acting, Social, Emotion, and Expressive Action Prompts for Character Animation Pipeline.
Separated module to maintain strict modularity and prevent file bloat.
Each action supports 5 camera angles (0°, 45°, 90°, 135°, 180°) with seamless 4s loop (start_frame = end_frame).
"""

from agent.services.prompt_templates import GLOBAL_VIDEO_LOCK

# ─── 1. WAVE / VẪY TAY CHÀO ──────────────────────────────────────────
WAVE_PROMPT_TEMPLATES = {
    "0": (
        "[wave-0°] 4-second seamless loop GENTLE WELCOMING HAND WAVE (0° direct front view). "
        "Character stands poised facing camera and raises right forearm to mid-chest/shoulder level, gently waving open hand in an elegant, friendly greeting arc (2 natural side-to-side waves). "
        "Left arm remains naturally relaxed at side. Right hand then lowers smoothly back to resting position. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "45": (
        "[wave-45°] 4-second seamless loop GENTLE WELCOMING HAND WAVE (45° three-quarter view). "
        "Character stands poised at 45° angle facing bottom-left, gracefully raises visible forearm to wave hand in a soft, polite greeting motion. "
        "Body stays anchored at 45° perspective. Hand lowers smoothly back to side. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "90": (
        "[wave-90°] 4-second seamless loop GENTLE WELCOMING HAND WAVE (90° side profile). "
        "Character stands in pure side profile facing 9 o'clock, visible arm raises slightly forward and waves hand gracefully in gentle greeting. "
        "Torso and feet remain strictly anchored in 90° silhouette. Hand returns to resting pose. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "135": (
        "[wave-135°] 4-second seamless loop GENTLE FAREWELL / OVER-SHOULDER WAVE (135° back-left view). "
        "Character viewed from behind at 135° angle, gracefully raises left forearm and waves hand outward in a gentle parting/greeting gesture visible from behind. "
        "Body stays anchored at 135° angle. Hand returns smoothly to rest. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "180": (
        "[wave-180°] 4-second seamless loop OVER-SHOULDER / REAR WAVE (180° rear view). "
        "Character stands facing 100% away from camera, gently raises one hand beside shoulder to wave farewell in an elegant gesture visible from behind. "
        "Hand returns to resting position at side. Bilateral symmetry maintained. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
}

# ─── 2. BOW / HÀNH LỄ / CÚI CHÀO ────────────────────────────────────
BOW_PROMPT_TEMPLATES = {
    "0": (
        "[bow-0°] 4-second seamless loop REVERENT TRADITIONAL ETIQUETTE BOW / SALUTE (0° front view). "
        "Character brings both hands together in front of chest in a traditional respectful cupped-hand clasp salute (formal bow / bao quan le), "
        "upper torso inclines forward 15-20 degrees in dignified homage, then gracefully straightens upright, hands separating and returning to resting position at sides. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "45": (
        "[bow-45°] 4-second seamless loop REVERENT TRADITIONAL ETIQUETTE BOW / SALUTE (45° three-quarter view). "
        "Character clasps hands together at chest and performs a dignified, reverent 20-degree forward bow along the 45-degree axis, then straightens back to initial poised standing posture. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "90": (
        "[bow-90°] 4-second seamless loop REVERENT TRADITIONAL ETIQUETTE BOW (90° side profile). "
        "Character in pure side profile raises hands to chest and gracefully bows upper torso forward in respectful homage, then smoothly returns to upright standing pose. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "135": (
        "[bow-135°] 4-second seamless loop REVERENT TRADITIONAL ETIQUETTE BOW (135° back-left view). "
        "Character viewed from behind at 135° angle, shoulders and back curve forward gently in a formal bow, then rise gracefully back to upright posture. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "180": (
        "[bow-180°] 4-second seamless loop REVERENT TRADITIONAL ETIQUETTE BOW (180° rear view). "
        "Character facing directly away from camera, shoulders incline forward in formal respectful bow, spine straightens smoothly back to vertical poised stance. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
}

# ─── 3. COVER MOUTH LAUGH / CHE MIỆNG CƯỜI ────────────────────────────
COVER_MOUTH_LAUGH_PROMPT_TEMPLATES = {
    "0": (
        "[cover-mouth-0°] 4-second seamless loop DEMURE ELEGANT CHUCKLE / COVER MOUTH LAUGH (0° front view). "
        "Character brings delicate hand / wide sleeve up to gently conceal lower face / smile in a demure, charming chuckle; "
        "shoulders and chest shake very softly with subtle amusement (delicate suppressed laughter), then hand smoothly lowers back to side. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "45": (
        "[cover-mouth-45°] 4-second seamless loop DEMURE ELEGANT CHUCKLE (45° three-quarter view). "
        "Character at 45° angle raises graceful hand with flowing sleeve to cover lower face in a coy, amused chuckle; delicate rhythmic shoulder vibration, then hand lowers back to rest. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "90": (
        "[cover-mouth-90°] 4-second seamless loop DEMURE ELEGANT CHUCKLE (90° side profile). "
        "Character in side profile raises graceful hand to mouth in quiet demure giggle; subtle soft upper body amusement motion, then hand returns naturally to side. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "135": (
        "[cover-mouth-135°] 4-second seamless loop DEMURE AMUSED GIGGLE (135° back-left view). "
        "Character viewed from behind at 135° angle turns head slightly inward, hand raises toward face, shoulders bob softly in subtle quiet laughter, then body relaxes back to poised stillness. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "180": (
        "[cover-mouth-180°] 4-second seamless loop SUPPRESSED GIGGLE (180° rear view). "
        "Character facing away from camera, shoulders shake delicately with restrained amused chuckle, elbow rises slightly as hand conceals face, then posture settles back to calm rest. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
}

# ─── 4. TALKING / NÓI CHUYỆN & DIỄN GIẢI ──────────────────────────────
TALKING_PROMPT_TEMPLATES = {
    "0": (
        "[talking-0°] 4-second seamless loop CONVERSATIONAL DIALOGUE & EXPLANATORY GESTURES (0° front view). "
        "Character engages in lively yet poised dialogue facing camera, head nods gently in natural cadence, "
        "one or both hands rise to lower-chest level gesturing politely with open palms (rhythmic natural speech gestures), then return calmly to resting posture. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "45": (
        "[talking-45°] 4-second seamless loop CONVERSATIONAL DIALOGUE & EXPLANATORY GESTURES (45° three-quarter view). "
        "Character speaks expressively at 45° angle, natural subtle head gestures and gentle communicative hand gestures explaining a point, then settling back to resting stance. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "90": (
        "[talking-90°] 4-second seamless loop CONVERSATIONAL DIALOGUE (90° side profile). "
        "Character in side profile speaks with subtle natural head tilt and rhythmic hand emphasis in front of chest, then rests arm back down. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "135": (
        "[talking-135°] 4-second seamless loop CONVERSATIONAL DIALOGUE (135° back-left view). "
        "Character viewed from behind at 135° angle, head tilts naturally in conversation, visible left hand gestures outward in dialogue rhythm, returning to rest. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "180": (
        "[talking-180°] 4-second seamless loop CONVERSATIONAL DIALOGUE (180° rear view). "
        "Character facing away, subtle head cadence and elbow/forearm conversational movement visible from behind, returning smoothly to symmetrical resting posture. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
}

# ─── 5. THINK / SUY NGHĨ / ĐĂM CHIÊU ──────────────────────────────────
THINK_PROMPT_TEMPLATES = {
    "0": (
        "[think-0°] 4-second seamless loop PENSIVE CONTEMPLATION / HAND ON CHIN (0° front view). "
        "Character brings right hand up to lightly touch/support chin in deep thought, head tilts slightly to one side in pensive contemplation, "
        "holds thoughtful pose briefly, then gently lowers hand back to side and centers head. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "45": (
        "[think-45°] 4-second seamless loop PENSIVE CONTEMPLATION (45° three-quarter view). "
        "Character at 45° angle raises hand to chin, tilts head pensively pondering a puzzle, then smoothly returns to initial standing pose. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "90": (
        "[think-90°] 4-second seamless loop PENSIVE CONTEMPLATION (90° side profile). "
        "Character in side profile touches chin with fingers in thoughtful pondering gesture, then returns arm down to resting pose. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "135": (
        "[think-135°] 4-second seamless loop PENSIVE CONTEMPLATION (135° back-left view). "
        "Character viewed from behind at 135° angle, elbow bends as hand reaches chin in contemplation, head tilts thoughtfully, then settles back. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "180": (
        "[think-180°] 4-second seamless loop PENSIVE CONTEMPLATION (180° rear view). "
        "Character facing away, head tilts slightly with elbow raised in pondering gesture, then posture relaxes back to vertical stillness. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
}

# ─── 6. SURPRISE / KINH NGẠC / BẤT NGỜ ───────────────────────────────
SURPRISE_PROMPT_TEMPLATES = {
    "0": (
        "[surprise-0°] 4-second seamless loop SUDDEN STARTLED SURPRISE / GASP (0° front view). "
        "Character startles with a subtle sudden gasp of surprise: shoulders rise, upper body recoils half a step back in wonder, "
        "both hands raise lightly to chest level in instinctive surprise, then character composes composure and returns to poised initial stance. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "45": (
        "[surprise-45°] 4-second seamless loop SUDDEN STARTLED SURPRISE (45° three-quarter view). "
        "Character at 45° angle reacts in startled surprise, body recoils slightly along diagonal, hands lift toward chest, then returns to calm balance. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "90": (
        "[surprise-90°] 4-second seamless loop SUDDEN STARTLED SURPRISE (90° side profile). "
        "Character in side profile flinches back subtly in surprise with hands rising, then relaxes smoothly back to initial stance. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "135": (
        "[surprise-135°] 4-second seamless loop SUDDEN STARTLED SURPRISE (135° back-left view). "
        "Character viewed from behind at 135° angle startles back with shoulders hitching, then regains calm poised posture. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "180": (
        "[surprise-180°] 4-second seamless loop SUDDEN STARTLED SURPRISE (180° rear view). "
        "Character facing away jumps back a fraction of an inch in sudden startle, shoulders tensing then relaxing back to neutral stillness. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
}

# ─── 7. NOD / GẬT ĐẦU ĐỒNG Ý ─────────────────────────────────────────
NOD_PROMPT_TEMPLATES = {
    "0": (
        "[nod-0°] 4-second seamless loop AFFIRMATIVE RESPECTFUL NOD (0° front view). "
        "Character stands poised facing camera and gives two slow, dignified, affirmative nods of approval and agreement, hands remaining calm at sides, "
        "then head returns to level eye-line. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "45": (
        "[nod-45°] 4-second seamless loop AFFIRMATIVE RESPECTFUL NOD (45° three-quarter view). "
        "Character at 45° angle nods gracefully twice in dignified agreement, body anchored rock-steady, head returning to neutral level. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "90": (
        "[nod-90°] 4-second seamless loop AFFIRMATIVE RESPECTFUL NOD (90° side profile). "
        "Character in side profile nods clearly and gracefully twice in approval, then holds level posture. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "135": (
        "[nod-135°] 4-second seamless loop AFFIRMATIVE RESPECTFUL NOD (135° back-left view). "
        "Character viewed from behind at 135° angle, head tilts forward in two gentle affirmative nods, returning to rest. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "180": (
        "[nod-180°] 4-second seamless loop AFFIRMATIVE RESPECTFUL NOD (180° rear view). "
        "Character facing away nods head down and up twice in clear agreement, hair swaying gently with the motion, returning to level rest. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
}

# ─── 8. CHEER / REO HÒ / ĂN MỪNG ─────────────────────────────────────
CHEER_PROMPT_TEMPLATES = {
    "0": (
        "[cheer-0°] 4-second seamless loop JOYFUL CELEBRATION / CHEER (0° front view). "
        "Character performs an enthusiastic joyful celebration: pumping both fists up to shoulder level with energetic delight and slight joyful hop in place, "
        "radiating victory and happiness, then brings arms smoothly back down to poised standing pose. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "45": (
        "[cheer-45°] 4-second seamless loop JOYFUL CELEBRATION (45° three-quarter view). "
        "Character at 45° angle pumps fists with joyful energy in celebration, then lowers arms smoothly back to resting posture. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "90": (
        "[cheer-90°] 4-second seamless loop JOYFUL CELEBRATION (90° side profile). "
        "Character in side profile raises arms in energetic cheer of victory, then returns smoothly to initial standing poise. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "135": (
        "[cheer-135°] 4-second seamless loop JOYFUL CELEBRATION (135° back-left view). "
        "Character viewed from behind at 135° angle raises arms high in energetic celebration, then lowers hands back to sides. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "180": (
        "[cheer-180°] 4-second seamless loop JOYFUL CELEBRATION (180° rear view). "
        "Character facing away raises both arms in joyful triumph, robe sleeves fluttering happily, returning to symmetrical resting stance. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
}

# ─── 9. SAD / BUỒN BÃ / THỞ DÀI ──────────────────────────────────────
SAD_PROMPT_TEMPLATES = {
    "0": (
        "[sad-0°] 4-second seamless loop MELANCHOLY SIGH / SAD SLUMP (0° front view). "
        "Character sags with gentle melancholy: head lowers slowly looking down, shoulders droop in a heavy dejected sigh, "
        "hands resting limply at sides, before softly lifting head back up to initial poise. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "45": (
        "[sad-45°] 4-second seamless loop MELANCHOLY SIGH (45° three-quarter view). "
        "Character at 45° angle droops head and shoulders in a sorrowful sigh, then slowly composes upright posture. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "90": (
        "[sad-90°] 4-second seamless loop MELANCHOLY SIGH (90° side profile). "
        "Character in side profile bows head down sorrowfully in a heavy breath, then straightens back to standing pose. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "135": (
        "[sad-135°] 4-second seamless loop MELANCHOLY SIGH (135° back-left view). "
        "Character viewed from behind at 135° angle slumps shoulders in sadness, then slowly stands tall again. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "180": (
        "[sad-180°] 4-second seamless loop MELANCHOLY SIGH (180° rear view). "
        "Character facing away drops head and slumps back dejectedly in a sigh, then rises back to symmetrical poise. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
}

# ─── 10. ANGRY / TỨC GIẬN / DẬM CHÂN DỖI ────────────────────────────
ANGRY_PROMPT_TEMPLATES = {
    "0": (
        "[angry-0°] 4-second seamless loop INDIGNANT POUT / ANGRY STOMP (0° front view). "
        "Character crosses arms firmly over chest in indignation, turns head slightly aside in a huff with a firm petulant foot tap, "
        "then uncrosses arms and returns to neutral standing posture. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "45": (
        "[angry-45°] 4-second seamless loop INDIGNANT POUT (45° three-quarter view). "
        "Character at 45° angle crosses arms and turns head huffily in angry pout, then returns arms to sides. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "90": (
        "[angry-90°] 4-second seamless loop INDIGNANT POUT (90° side profile). "
        "Character in side profile crosses arms tightly and jerks chin upward indignantly, then settles back down. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "135": (
        "[angry-135°] 4-second seamless loop INDIGNANT POUT (135° back-left view). "
        "Character viewed from behind turns away indignantly with arms crossed, then returns to poised posture. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "180": (
        "[angry-180°] 4-second seamless loop INDIGNANT POUT (180° rear view). "
        "Character facing away tenses shoulders and stamps foot petulantly, elbows jutting as arms fold, returning to stillness. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
}

# ─── 11. HURT / TRÚNG ĐÒN / LẢO ĐẢO ──────────────────────────────────
HURT_PROMPT_TEMPLATES = {
    "0": (
        "[hurt-0°] 4-second seamless loop STAGGER RECOIL FROM IMPACT (0° front view). "
        "Character reels backward from an invisible impact: torso jerks back, one hand clenches at abdomen/chest in pain while other hand braces balance, "
        "staggers one step back, then bravely recovers footing and regains poised combat stance. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "45": (
        "[hurt-45°] 4-second seamless loop STAGGER RECOIL FROM IMPACT (45° three-quarter view). "
        "Character reels back along the 45° line from impact, braces balance, then steps back firmly into initial standing posture. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "90": (
        "[hurt-90°] 4-second seamless loop STAGGER RECOIL (90° side profile). "
        "Character in side profile staggers back under impact, clutches chest, then regains upright balance. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "135": (
        "[hurt-135°] 4-second seamless loop STAGGER RECOIL (135° back-left view). "
        "Character viewed from behind jolts forward-left from impact, stumbles, then regains poised stance. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
    "180": (
        "[hurt-180°] 4-second seamless loop STAGGER RECOIL (180° rear view). "
        "Character facing away recoils and recovers balanced footing cleanly, robes settling back into stillness. "
        f"{GLOBAL_VIDEO_LOCK} "
        "Seamless loop: first frame = last frame identically. Camera static. Solid green {chromaBgHex} background."
    ),
}

ACTING_ACTION_TEMPLATES = {
    "wave": WAVE_PROMPT_TEMPLATES,
    "bow": BOW_PROMPT_TEMPLATES,
    "cover_mouth_laugh": COVER_MOUTH_LAUGH_PROMPT_TEMPLATES,
    "talking": TALKING_PROMPT_TEMPLATES,
    "think": THINK_PROMPT_TEMPLATES,
    "surprise": SURPRISE_PROMPT_TEMPLATES,
    "nod": NOD_PROMPT_TEMPLATES,
    "cheer": CHEER_PROMPT_TEMPLATES,
    "sad": SAD_PROMPT_TEMPLATES,
    "angry": ANGRY_PROMPT_TEMPLATES,
    "hurt": HURT_PROMPT_TEMPLATES,
}
