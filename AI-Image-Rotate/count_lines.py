import os

print(f"{'File Path':<45} | {'Lines':<6}")
print("-" * 55)
for root, dirs, files in os.walk('.'):
    if any(k in root for k in ['venv', '.venv', '.git', 'outputs', '__pycache__', 'models_cache']):
        continue
    for f in sorted(files):
        if f.endswith(('.py', '.js', '.css', '.html', '.md', '.bat', '.txt')):
            path = os.path.relpath(os.path.join(root, f))
            try:
                with open(path, 'r', encoding='utf-8') as fp:
                    lines = len(fp.readlines())
                print(f"{path:<45} | {lines:<6}")
            except Exception:
                pass
