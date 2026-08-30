import type { Plugin } from 'vite';
import path from 'path';
import fs from 'fs';

/**
 * Vite plugin that provides:
 * - API endpoints for saving/scanning 2D/3D assets
 * - Static file serving for assets/ and asset_2ds/ directories
 */
export function serveAssetsPlugin(rootDir: string): Plugin {
  return {
    name: 'serve-and-save-assets',
    configureServer(server) {
      // ─── API: Save Assembled Character ───
      server.middlewares.use('/api/save-character', (req, res, next) => {
        if (req.method !== 'POST') return next();
        let body = '';
        req.on('data', (chunk) => { body += chunk; });
        req.on('end', () => {
          try {
            const data = JSON.parse(body);
            const name = sanitizeName(data.filename || data.name || 'nhan_vat_lap_rap');
            const targetDir = path.join(rootDir, 'assets', 'nhan_vat', '_lap_rap');
            ensureDir(targetDir);

            const profileData = { ...(data.profileData || data) };
            const previewImg = data.previewImageBase64 || profileData.preview_image;

            if (previewImg && previewImg.includes('base64,')) {
              const base64Data = previewImg.split('base64,')[1];
              fs.writeFileSync(path.join(targetDir, `${name}.png`), Buffer.from(base64Data, 'base64'));
            }

            profileData.preview_image = `assets/nhan_vat/_lap_rap/${name}.png`;
            if (profileData.preview && profileData.preview.includes('base64,')) {
              profileData.preview = `assets/nhan_vat/_lap_rap/${name}.png`;
            }

            fs.writeFileSync(path.join(targetDir, `${name}.json`), JSON.stringify(profileData, null, 2), 'utf-8');

            sendJson(res, {
              success: true,
              savedPath: `assets/nhan_vat/_lap_rap/${name}.json`,
              previewPath: `assets/nhan_vat/_lap_rap/${name}.png`,
            });
          } catch (err: any) {
            sendJsonError(res, err?.message || 'Lỗi lưu nhân vật');
          }
        });
      });

      // ─── API: Save Custom Map Preset ───
      server.middlewares.use('/api/save-map', (req, res, next) => {
        if (req.method !== 'POST') return next();
        let body = '';
        req.on('data', (chunk) => { body += chunk; });
        req.on('end', () => {
          try {
            const data = JSON.parse(body);
            const name = sanitizeName(data.filename || data.name || 'custom_map');
            const targetDir = path.join(rootDir, 'assets', 'ban_do', '_custom_ban_do');
            ensureDir(targetDir);

            const mapData = { ...(data.mapData || data) };
            const previewImg = data.previewImageBase64 || mapData.preview_image;

            if (previewImg && previewImg.includes('base64,')) {
              const base64Data = previewImg.split('base64,')[1];
              fs.writeFileSync(path.join(targetDir, `${name}.png`), Buffer.from(base64Data, 'base64'));
            }

            mapData.preview_image = `assets/ban_do/_custom_ban_do/${name}.png`;
            fs.writeFileSync(path.join(targetDir, `${name}.json`), JSON.stringify(mapData, null, 2), 'utf-8');

            sendJson(res, {
              success: true,
              savedPath: `assets/ban_do/_custom_ban_do/${name}.json`,
              previewPath: `assets/ban_do/_custom_ban_do/${name}.png`,
            });
          } catch (err: any) {
            sendJsonError(res, err?.message || 'Lỗi lưu bản đồ');
          }
        });
      });

      // ─── API: Save Assembled 2D Character ───
      server.middlewares.use('/api/save-2d-character', (req, res, next) => {
        if (req.method !== 'POST') return next();
        let body = '';
        req.on('data', (chunk) => { body += chunk; });
        req.on('end', () => {
          try {
            const data = JSON.parse(body);
            const name = sanitizeName(data.filename || data.name || 'nhan_vat_2d');
            const targetDir = path.join(rootDir, 'asset_2ds', 'nhan_vat', '_lap_rap');
            ensureDir(targetDir);

            const profileData = { ...(data.profileData || data) };
            const previewImg = data.previewImageBase64 || profileData.preview_image;

            if (previewImg && previewImg.includes('base64,')) {
              const base64Data = previewImg.split('base64,')[1];
              fs.writeFileSync(path.join(targetDir, `${name}.png`), Buffer.from(base64Data, 'base64'));
            }

            profileData.preview_image = `asset_2ds/nhan_vat/_lap_rap/${name}.png`;
            fs.writeFileSync(path.join(targetDir, `${name}.json`), JSON.stringify(profileData, null, 2), 'utf-8');

            sendJson(res, {
              success: true,
              savedPath: `asset_2ds/nhan_vat/_lap_rap/${name}.json`,
              previewPath: `asset_2ds/nhan_vat/_lap_rap/${name}.png`,
            });
          } catch (err: any) {
            sendJsonError(res, err?.message || 'Lỗi lưu nhân vật 2D');
          }
        });
      });

      // ─── API: Save Custom 2D Map ───
      server.middlewares.use('/api/save-2d-map', (req, res, next) => {
        if (req.method !== 'POST') return next();
        let body = '';
        req.on('data', (chunk) => { body += chunk; });
        req.on('end', () => {
          try {
            const data = JSON.parse(body);
            const name = sanitizeName(data.filename || data.name || 'custom_map_2d');
            const targetDir = path.join(rootDir, 'asset_2ds', 'ban_do', '_custom_ban_do');
            ensureDir(targetDir);

            const mapData = { ...(data.mapData || data) };
            const previewImg = data.previewImageBase64 || mapData.preview_image;

            if (previewImg && previewImg.includes('base64,')) {
              const base64Data = previewImg.split('base64,')[1];
              fs.writeFileSync(path.join(targetDir, `${name}.png`), Buffer.from(base64Data, 'base64'));
            }

            mapData.preview_image = `asset_2ds/ban_do/_custom_ban_do/${name}.png`;
            fs.writeFileSync(path.join(targetDir, `${name}.json`), JSON.stringify(mapData, null, 2), 'utf-8');

            sendJson(res, {
              success: true,
              savedPath: `asset_2ds/ban_do/_custom_ban_do/${name}.json`,
              previewPath: `asset_2ds/ban_do/_custom_ban_do/${name}.png`,
            });
          } catch (err: any) {
            sendJsonError(res, err?.message || 'Lỗi lưu bản đồ 2D');
          }
        });
      });

      // ─── API: List 2D Characters with Action Sequences ───
      server.middlewares.use('/api/list-2d-characters', (_req, res) => {
        try {
          const nhanVatDir = path.join(rootDir, 'asset_2ds', 'nhan_vat');
          ensureDir(nhanVatDir);
          const entries = fs.readdirSync(nhanVatDir, { withFileTypes: true });
          const bodyParts = new Set([
            'ban_tay', 'cang_chan', 'cang_tay', 'canh_tay', 'dau', 'dui',
            'khuon_mat', 'long_may', 'mat', 'mieng', 'mui', 'than_co_ban',
            'toc_sau', 'toc_truoc', 'trang_phuc', 'vu_khi', '_lap_rap',
          ]);
          const characters = entries
            .filter((e) => e.isDirectory() && !e.name.startsWith('.') && !bodyParts.has(e.name))
            .map((e) => e.name);
          
          // If empty, supply default character
          if (!characters.includes('nhan_vat_chinh')) {
            characters.unshift('nhan_vat_chinh');
          }
          sendJson(res, characters);
        } catch (err: any) {
          sendJsonError(res, err?.message);
        }
      });

      // ─── API: List 2D Actions for a Character ───
      server.middlewares.use('/api/list-2d-actions', (req, res) => {
        try {
          const url = new URL(req.url || '', `http://${req.headers.host}`);
          const charParam = sanitizeName(url.searchParams.get('character') || 'nhan_vat_chinh');
          const actionsDir = path.join(rootDir, 'asset_2ds', 'nhan_vat', charParam, 'hanh_dong');
          if (!fs.existsSync(actionsDir) || !fs.statSync(actionsDir).isDirectory()) {
            sendJson(res, ['chem_kiem', 'di_bo', 'tung_chuong', 'dung_yen']);
            return;
          }
          const entries = fs.readdirSync(actionsDir, { withFileTypes: true });
          const actions = entries
            .filter((e) => e.isDirectory() && !e.name.startsWith('.'))
            .map((e) => e.name);
          sendJson(res, actions.length > 0 ? actions : ['chem_kiem', 'di_bo', 'tung_chuong', 'dung_yen']);
        } catch (err: any) {
          sendJsonError(res, err?.message);
        }
      });

      // ─── API: Save 2D Animation Motion Sequence ───
      server.middlewares.use('/api/save-2d-animation-motion', (req, res, next) => {
        if (req.method !== 'POST') return next();
        let body = '';
        req.on('data', (chunk) => { body += chunk; });
        req.on('end', () => {
          try {
            const data = JSON.parse(body);
            const charSlug = sanitizeName(data.character || 'nhan_vat_chinh');
            const actionSlug = sanitizeName(data.actionName || 'chem_kiem');
            const angleSlug = sanitizeName(data.angleSlug || `goc_${data.angleDeg ?? 0}`);

            const targetDir = path.join(
              rootDir,
              'asset_2ds',
              'nhan_vat',
              charSlug,
              'hanh_dong',
              actionSlug,
              angleSlug
            );
            ensureDir(targetDir);

            const frames = Array.isArray(data.frames) ? data.frames : [];
            const savedFramesMeta: any[] = [];

            frames.forEach((f: any, idx: number) => {
              const frameNum = String(idx + 1).padStart(2, '0');
              const fileName = `frame_${frameNum}.png`;
              const filePath = path.join(targetDir, fileName);
              const imgUrl = f.transparentDataUrl || f.originalDataUrl || f.base64;

              if (imgUrl && imgUrl.includes('base64,')) {
                const base64Data = imgUrl.split('base64,')[1];
                fs.writeFileSync(filePath, Buffer.from(base64Data, 'base64'));
              }

              savedFramesMeta.push({
                index: idx,
                file: fileName,
                relPath: `asset_2ds/nhan_vat/${charSlug}/hanh_dong/${actionSlug}/${angleSlug}/${fileName}`,
                durationMs: f.durationMs || 500,
                offsetX: f.offsetX || 0,
                offsetY: f.offsetY || 0,
                scale: f.scale || 1.0,
                rotation: f.rotation || 0,
                flipX: !!f.flipX,
              });
            });

            // Write motion manifest metadata JSON
            const motionMeta = {
              character: charSlug,
              actionName: data.actionDisplayName || data.actionName || actionSlug,
              actionSlug,
              angleSlug,
              angleDeg: data.angleDeg ?? 0,
              fps: data.fps || 8,
              loopMode: data.loopMode || 'loop',
              totalDurationMs: frames.reduce((acc: number, f: any) => acc + (f.durationMs || 500), 0),
              frameCount: frames.length,
              frameOrder: data.frameOrder || frames.map((_: any, i: number) => i),
              frames: savedFramesMeta,
              savedAt: new Date().toISOString(),
            };

            fs.writeFileSync(
              path.join(targetDir, 'motion_meta.json'),
              JSON.stringify(motionMeta, null, 2),
              'utf-8'
            );

            sendJson(res, {
              success: true,
              character: charSlug,
              action: actionSlug,
              angle: angleSlug,
              targetDir: `asset_2ds/nhan_vat/${charSlug}/hanh_dong/${actionSlug}/${angleSlug}`,
              filesCount: frames.length,
              metaFile: `asset_2ds/nhan_vat/${charSlug}/hanh_dong/${actionSlug}/${angleSlug}/motion_meta.json`,
            });
          } catch (err: any) {
            sendJsonError(res, err?.message || 'Lỗi lưu hoạt ảnh 2D');
          }
        });
      });

      // ─── API: Save 2D Parts / Sprites to Disk ───
      server.middlewares.use('/api/save-2d-parts', (req, res, next) => {
        if (req.method !== 'POST') return next();
        let body = '';
        req.on('data', (chunk) => { body += chunk; });
        req.on('end', () => {
          try {
            const data = JSON.parse(body);
            const genre = sanitizeName(data.genre || 'nhan_vat');
            const character = sanitizeName(data.character || 'nhan_vat_chinh');
            const partCategory = sanitizeName(data.partCategory || 'linh_kien');
            const partDisplayName = data.partDisplayName || data.partCategory || partCategory;
            const angleSlug = data.angleSlug ? sanitizeName(data.angleSlug) : '';

            // Target directory: asset_2ds/nhan_vat/[character]/[partCategory]/[angleSlug] or asset_2ds/[genre]/[partCategory]
            let targetDir = path.join(rootDir, 'asset_2ds', genre);
            if (character && genre === 'nhan_vat') {
              targetDir = path.join(targetDir, character, partCategory);
            } else {
              targetDir = path.join(targetDir, partCategory);
            }
            if (angleSlug) {
              targetDir = path.join(targetDir, angleSlug);
            }
            ensureDir(targetDir);

            const items = Array.isArray(data.items) ? data.items : [];
            const savedFiles: string[] = [];

            items.forEach((item: any, idx: number) => {
              const rawName = item.name ? sanitizeName(item.name.replace(/\.[^.]+$/, '')) : `part_${idx + 1}`;
              const fileName = `${rawName}.png`;
              const filePath = path.join(targetDir, fileName);
              const imgUrl = item.transparentUrl || item.originalUrl || item.url || item.base64;

              if (imgUrl && imgUrl.includes('base64,')) {
                const base64Data = imgUrl.split('base64,')[1];
                fs.writeFileSync(filePath, Buffer.from(base64Data, 'base64'));
              }
              savedFiles.push(fileName);
            });

            // Write metadata JSON
            const meta = {
              genre,
              character,
              partCategory,
              partDisplayName,
              angleSlug,
              savedCount: items.length,
              savedFiles,
              savedAt: new Date().toISOString(),
            };
            fs.writeFileSync(path.join(targetDir, 'part_meta.json'), JSON.stringify(meta, null, 2), 'utf-8');

            sendJson(res, {
              success: true,
              savedCount: items.length,
              targetDir: path.relative(rootDir, targetDir).replace(/\\/g, '/'),
            });
          } catch (err: any) {
            sendJsonError(res, err?.message || 'Lỗi lưu linh kiện 2D');
          }
        });
      });

      // ─── API: List top-level folders in asset_2ds/ ───
      server.middlewares.use('/api/list-2d-folders', (_req, res) => {
        try {
          const asset2dDir = path.join(rootDir, 'asset_2ds');
          const entries = fs.readdirSync(asset2dDir, { withFileTypes: true });
          const folders = entries
            .filter((e) => e.isDirectory() && !e.name.startsWith('.'))
            .map((e) => e.name);
          sendJson(res, folders);
        } catch (err: any) {
          sendJsonError(res, err?.message);
        }
      });

      // ─── API: Live scan assets in asset_2ds/ subfolder ───
      server.middlewares.use('/api/scan-2d-assets', (req, res) => {
        try {
          const url = new URL(req.url || '', `http://${req.headers.host}`);
          const folderParam = url.searchParams.get('folder') || '';
          const asset2dDir = path.join(rootDir, 'asset_2ds');
          const targetDir = path.join(asset2dDir, folderParam);

          // Security: ensure resolved path is within asset_2ds
          const resolved = path.resolve(targetDir);
          if (!resolved.startsWith(path.resolve(asset2dDir))) {
            res.statusCode = 403;
            res.end(JSON.stringify({ error: 'Access denied' }));
            return;
          }

          if (!fs.existsSync(targetDir) || !fs.statSync(targetDir).isDirectory()) {
            sendJson(res, []);
            return;
          }

          const items = scanAssetDirectory(asset2dDir, targetDir);
          res.setHeader('Content-Type', 'application/json');
          res.setHeader('Access-Control-Allow-Origin', '*');
          res.end(JSON.stringify(items));
        } catch (err: any) {
          sendJsonError(res, err?.message);
        }
      });

      // ─── Static: asset_2ds/ file serving ───
      server.middlewares.use('/asset_2ds', (req, res, next) => {
        const rawUrl = (req.url || '').split('?')[0];
        const decoded = decodeURIComponent(rawUrl).replace(/^\/+/, '');
        const asset2dDir = path.join(rootDir, 'asset_2ds');
        const resolvedPath = path.join(asset2dDir, decoded);

        if (fs.existsSync(resolvedPath) && fs.statSync(resolvedPath).isFile()) {
          if (req.url?.includes('?import')) return next();
          serveStaticFile(res, resolvedPath);
          return;
        }
        next();
      });

      // ─── Static: assets/ file serving with smart alias resolution ───
      server.middlewares.use('/assets', (req, res, next) => {
        const rawUrl = (req.url || '').split('?')[0];
        const decoded = decodeURIComponent(rawUrl).replace(/^\/+/, '');
        const resolvedPath = findAssetFile(rootDir, decoded);

        if (resolvedPath && fs.existsSync(resolvedPath) && fs.statSync(resolvedPath).isFile()) {
          if (req.url?.includes('?import')) return next();
          serveStaticFile(res, resolvedPath);
          return;
        }
        next();
      });
    },
  };
}

// ─── Helpers ────────────────────────────────────────────────────────

const IMAGE_EXTS = ['.png', '.jpg', '.jpeg', '.webp', '.gif', '.svg'];
const AUDIO_EXTS = ['.mp3', '.wav', '.ogg', '.m4a'];

const MIME_TYPES: Record<string, string> = {
  '.vrm': 'model/gltf-binary',
  '.glb': 'model/gltf-binary',
  '.gltf': 'model/gltf+json',
  '.fbx': 'application/octet-stream',
  '.obj': 'text/plain',
  '.bin': 'application/octet-stream',
  '.mp3': 'audio/mpeg',
  '.wav': 'audio/wav',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.webp': 'image/webp',
  '.gif': 'image/gif',
  '.svg': 'image/svg+xml',
  '.json': 'application/json; charset=utf-8',
};

function sanitizeName(raw: string): string {
  return raw
    .replace(/[^a-zA-Z0-9_\u00C0-\u024F\u1E00-\u1EFF\-]/g, '_')
    .toLowerCase();
}

function ensureDir(dir: string): void {
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }
}

function sendJson(res: any, data: unknown): void {
  res.setHeader('Content-Type', 'application/json');
  res.end(JSON.stringify(data));
}

function sendJsonError(res: any, message: string): void {
  res.statusCode = 500;
  res.setHeader('Content-Type', 'application/json');
  res.end(JSON.stringify({ success: false, error: message }));
}

function serveStaticFile(res: any, filePath: string): void {
  const ext = path.extname(filePath).toLowerCase();
  res.setHeader('Content-Type', MIME_TYPES[ext] || 'application/octet-stream');
  res.setHeader('Access-Control-Allow-Origin', '*');
  fs.createReadStream(filePath).pipe(res);
}

/** Smart alias resolution for assets/ directory */
function findAssetFile(rootDir: string, reqPath: string): string | null {
  const assetsDir = path.join(rootDir, 'assets');
  const direct = path.join(assetsDir, reqPath);
  if (fs.existsSync(direct) && fs.statSync(direct).isFile()) return direct;

  const aliases: Array<[RegExp, string]> = [
    [/^\/?maps\//i, 'ban_do/'],
    [/^\/?skyboxs?\//i, 'bau_troi/'],
    [/^\/?characters\//i, 'nhan_vat/'],
    [/^\/?props\//i, 'dao_cu/'],
    [/^\/?props\//i, 'hieu_ung/bao_phu/'],
    [/^\/?vfx\//i, 'hieu_ung/'],
  ];

  for (const [pattern, replacement] of aliases) {
    const transformed = reqPath.replace(pattern, replacement);
    const candidate = path.join(assetsDir, transformed);
    if (fs.existsSync(candidate) && fs.statSync(candidate).isFile()) return candidate;
  }

  const dir = path.dirname(reqPath);
  const base = path.basename(reqPath);
  const cleanBase = base.replace(/^_+/, '');
  if (cleanBase !== base) {
    const cleanCandidate = path.join(assetsDir, dir, cleanBase);
    if (fs.existsSync(cleanCandidate) && fs.statSync(cleanCandidate).isFile()) return cleanCandidate;
  }

  // Check sibling textures/ or source/ directories (common in 3D model bundles)
  const siblingCandidates = [
    path.join(assetsDir, dir, '..', 'textures', base),
    path.join(assetsDir, dir, '..', 'source', base),
    path.join(assetsDir, dir, 'textures', base),
    path.join(assetsDir, dir, 'source', base),
  ];
  for (const cand of siblingCandidates) {
    if (fs.existsSync(cand) && fs.statSync(cand).isFile()) return cand;
  }

  const rootCandidate = path.join(assetsDir, base);
  if (fs.existsSync(rootCandidate) && fs.statSync(rootCandidate).isFile()) return rootCandidate;
  const rootCleanCandidate = path.join(assetsDir, cleanBase);
  if (fs.existsSync(rootCleanCandidate) && fs.statSync(rootCleanCandidate).isFile()) return rootCleanCandidate;

  return null;
}

/** Recursively scan an asset_2ds subfolder for images, audio, and JSON files */
function scanAssetDirectory(asset2dDir: string, targetDir: string): any[] {
  const items: any[] = [];

  const scanRecursive = (dir: string) => {
    const entries = fs.readdirSync(dir, { withFileTypes: true });
    for (const entry of entries) {
      if (entry.name.startsWith('.')) continue;
      const fullPath = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        scanRecursive(fullPath);
        continue;
      }
      const ext = path.extname(entry.name).toLowerCase();
      const isImage = IMAGE_EXTS.includes(ext);
      const isAudio = AUDIO_EXTS.includes(ext);
      const isJson = ext === '.json' && !entry.name.includes('manifest') && !entry.name.includes('structure');
      if (!isImage && !isAudio && !isJson) continue;
      if (entry.name.toLowerCase().startsWith('preview.') || entry.name.toLowerCase().startsWith('thumbnail.')) continue;

      const relPath = path.relative(asset2dDir, fullPath).replace(/\\/g, '/');
      const stats = fs.statSync(fullPath);
      const baseName = path.parse(entry.name).name;

      const item: any = {
        id: relPath.replace(/\.[^/.]+$/, '').replace(/[/\\ \-_]/g, '_').toLowerCase(),
        name: baseName.replace(/_/g, ' ').replace(/-/g, ' ').replace(/\b\w/g, (c: string) => c.toUpperCase()),
        filename: entry.name,
        relPath,
        path: `asset_2ds/${relPath}`,
        format: ext.replace('.', '').toUpperCase(),
        sizeMB: (stats.size / (1024 * 1024)).toFixed(2),
        type: isImage ? 'image' : isAudio ? 'audio' : 'data',
      };

      if (isImage) item.previewUrl = `asset_2ds/${relPath}`;

      if (isJson) {
        try {
          const jsonData = JSON.parse(fs.readFileSync(fullPath, 'utf-8'));
          item.name = jsonData.name || item.name;
          const pngPath = path.join(dir, `${baseName}.png`);
          if (fs.existsSync(pngPath)) {
            item.previewUrl = `asset_2ds/${path.relative(asset2dDir, pngPath).replace(/\\/g, '/')}`;
          } else if (jsonData.preview_image) {
            item.previewUrl = jsonData.preview_image;
          }
        } catch { /* ignore */ }
      }

      items.push(item);
    }
  };

  scanRecursive(targetDir);
  return items;
}
