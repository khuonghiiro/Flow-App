import cv2
import numpy as np

def math_color_dist(c1, c2):
    return ((float(c1[0])-float(c2[0]))**2 + (float(c1[1])-float(c2[1]))**2 + (float(c1[2])-float(c2[2]))**2)**0.5

def generate_svgai_hierarchical_vector(image_path, num_colors=10, simplify_eps=0.0035, tension=1.12):
    img = cv2.imread(image_path)
    if img is None:
        raise ValueError(f"Cannot read {image_path}")
    h, w = img.shape[:2]

    # 1. Edge-Preserving Mean-Shift / Bilateral Filter
    shifted = cv2.pyrMeanShiftFiltering(img, sp=12, sr=25, maxLevel=2)
    shifted_rgb = cv2.cvtColor(shifted, cv2.COLOR_BGR2RGB)

    # 2. Extract Color Palette via K-Means
    pixels = shifted_rgb.reshape(-1, 3).astype(np.float32)
    criteria = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 25, 0.2)
    _, labels, centers = cv2.kmeans(pixels, num_colors, None, criteria, 10, cv2.KMEANS_PP_CENTERS)
    centers = [[int(x) for x in c] for c in centers]
    quantized = np.array(centers, dtype=np.uint8)[labels.flatten()].reshape(shifted_rgb.shape)

    # Detect Background Color (corners of image)
    corners = [tuple(quantized[0, 0]), tuple(quantized[0, -1]), tuple(quantized[-1, 0]), tuple(quantized[-1, -1])]
    bg_color = corners[0] # Most frequent corner

    # Color layers sorted by area
    color_layers = []
    for c in centers:
        c_tup = tuple(c)
        mask = cv2.inRange(quantized, np.array(c, dtype=np.uint8), np.array(c, dtype=np.uint8))
        area = cv2.countNonZero(mask)
        color_layers.append((c_tup, area, mask))

    color_layers.sort(key=lambda x: -x[1])

    # 3. Separate Background vs Foreground Subject
    subject_mask = np.zeros((h, w), dtype=np.uint8)
    for c, area, mask in color_layers:
        if math_color_dist(c, bg_color) > 30:
            subject_mask = cv2.bitwise_or(subject_mask, mask)

    # Smooth subject mask
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5))
    subject_mask_clean = cv2.morphologyEx(subject_mask, cv2.MORPH_CLOSE, kernel)

    def catmull_rom_to_bezier(pts, tens=tension):
        n = len(pts)
        if n < 3:
            d_str = f"M {pts[0][0]} {pts[0][1]} "
            for p in pts[1:]:
                d_str += f"L {p[0]} {p[1]} "
            return d_str + "z"
        
        d_str = f"M {pts[0][0]:.2f} {pts[0][1]:.2f} "
        for i in range(n):
            p0 = pts[(i - 1 + n) % n]
            p1 = pts[i]
            p2 = pts[(i + 1) % n]
            p3 = pts[(i + 2) % n]
            
            c1x = p1[0] + (p2[0] - p0[0]) / (6.0 * tens)
            c1y = p1[1] + (p2[1] - p0[1]) / (6.0 * tens)
            c2x = p2[0] - (p3[0] - p1[0]) / (6.0 * tens)
            c2y = p2[1] - (p3[1] - p1[1]) / (6.0 * tens)
            
            d_str += f"C {c1x:.2f} {c1y:.2f} {c2x:.2f} {c2y:.2f} {p2[0]:.2f} {p2[1]:.2f} "
        return d_str + "z"

    svg_paths = []

    # 4. STEP A: Background Path (if dark background)
    if bg_color[0] + bg_color[1] + bg_color[2] < 60:
        svg_paths.append(f'<rect width="{w}" height="{h}" fill="rgb({bg_color[0]},{bg_color[1]},{bg_color[2]})" />')

    # 5. STEP B: Solid Subject Base Silhouette (Dominant Body Color)
    dom_color = None
    for c, area, _ in color_layers:
        if math_color_dist(c, bg_color) > 30:
            dom_color = c
            break
    if dom_color is None:
        dom_color = (253, 230, 222)

    subj_cnts, _ = cv2.findContours(subject_mask_clean, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_TC89_KCOS)
    for scnt in subj_cnts:
        if cv2.contourArea(scnt) > 200:
            eps = simplify_eps * 0.7 * cv2.arcLength(scnt, True)
            approx = cv2.approxPolyDP(scnt, eps, True).reshape(-1, 2)
            if len(approx) >= 3:
                d_base = catmull_rom_to_bezier(approx, tension)
                svg_paths.append(f'<path fill="rgb({dom_color[0]},{dom_color[1]},{dom_color[2]})" d="{d_base}" />')

    # 6. STEP C: Layer all internal shading, highlights, and creases on top
    for c, area, mask in color_layers:
        if math_color_dist(c, bg_color) < 20 or c == dom_color:
            continue
        if area < 30:
            continue

        mask_k = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (3, 3)))
        contours, _ = cv2.findContours(mask_k, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_TC89_KCOS)
        for cnt in contours:
            if cv2.contourArea(cnt) < 30:
                continue
            eps = simplify_eps * cv2.arcLength(cnt, True)
            approx = cv2.approxPolyDP(cnt, eps, True).reshape(-1, 2)
            if len(approx) < 3:
                continue
            d = catmull_rom_to_bezier(approx, tension)
            svg_paths.append(f'<path fill="rgb({c[0]},{c[1]},{c[2]})" d="{d}" />')

    svg_final = f'<svg version="1.1" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" width="{w}" height="{h}" style="display:block;">\n'
    svg_final += "\n".join(svg_paths)
    svg_final += "\n</svg>"
    return svg_final

if __name__ == "__main__":
    svg = generate_svgai_hierarchical_vector('public/demo_rig/hand_000_front.jpg', num_colors=10, simplify_eps=0.0035, tension=1.12)
    with open('test_hierarchical_svgai.svg', 'w', encoding='utf-8') as f:
        f.write(svg)
    print("Generated test_hierarchical_svgai.svg: Size =", round(len(svg)/1024, 1), "KB | Paths =", svg.count("<path"))
