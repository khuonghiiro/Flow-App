import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'path';
import fs from 'fs';

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [
    react(),
    {
      name: 'serve-and-save-assets',
      configureServer(server) {
        // API: Save Assembled Character to assets/nhan_vat/_lap_rap/
        server.middlewares.use('/api/save-character', (req, res, next) => {
          if (req.method !== 'POST') return next();
          let body = '';
          req.on('data', (chunk) => { body += chunk; });
          req.on('end', () => {
            try {
              const data = JSON.parse(body);
              const name = (data.filename || data.name || 'nhan_vat_lap_rap')
                .replace(/[^a-zA-Z0-9_\u00C0-\u024F\u1E00-\u1EFF\-]/g, '_')
                .toLowerCase();
              const targetDir = path.join(__dirname, 'assets', 'nhan_vat', '_lap_rap');
              if (!fs.existsSync(targetDir)) {
                fs.mkdirSync(targetDir, { recursive: true });
              }

              const profileData = { ...(data.profileData || data) };
              const previewImg = data.previewImageBase64 || profileData.preview_image;

              // Write companion .png file if base64 provided
              if (previewImg && previewImg.includes('base64,')) {
                const base64Data = previewImg.split('base64,')[1];
                const imgPath = path.join(targetDir, `${name}.png`);
                fs.writeFileSync(imgPath, Buffer.from(base64Data, 'base64'));
              }

              // Store clean file path in JSON instead of heavy base64 string
              profileData.preview_image = `assets/nhan_vat/_lap_rap/${name}.png`;
              if (profileData.preview && profileData.preview.includes('base64,')) {
                profileData.preview = `assets/nhan_vat/_lap_rap/${name}.png`;
              }

              const jsonPath = path.join(targetDir, `${name}.json`);
              fs.writeFileSync(jsonPath, JSON.stringify(profileData, null, 2), 'utf-8');

              res.setHeader('Content-Type', 'application/json');
              res.end(JSON.stringify({
                success: true,
                savedPath: `assets/nhan_vat/_lap_rap/${name}.json`,
                previewPath: `assets/nhan_vat/_lap_rap/${name}.png`,
              }));
            } catch (err: any) {
              res.statusCode = 500;
              res.setHeader('Content-Type', 'application/json');
              res.end(JSON.stringify({ success: false, error: err?.message || 'Lỗi lưu nhân vật' }));
            }
          });
        });

        // API: Save Custom Map Preset to assets/ban_do/_custom_ban_do/
        server.middlewares.use('/api/save-map', (req, res, next) => {
          if (req.method !== 'POST') return next();
          let body = '';
          req.on('data', (chunk) => { body += chunk; });
          req.on('end', () => {
            try {
              const data = JSON.parse(body);
              const name = (data.filename || data.name || 'custom_map')
                .replace(/[^a-zA-Z0-9_\u00C0-\u024F\u1E00-\u1EFF\-]/g, '_')
                .toLowerCase();
              const targetDir = path.join(__dirname, 'assets', 'ban_do', '_custom_ban_do');
              if (!fs.existsSync(targetDir)) {
                fs.mkdirSync(targetDir, { recursive: true });
              }

              const mapData = { ...(data.mapData || data) };
              const previewImg = data.previewImageBase64 || mapData.preview_image;

              // Write companion .png file if base64 provided
              if (previewImg && previewImg.includes('base64,')) {
                const base64Data = previewImg.split('base64,')[1];
                const imgPath = path.join(targetDir, `${name}.png`);
                fs.writeFileSync(imgPath, Buffer.from(base64Data, 'base64'));
              }

              // Store clean file path in JSON instead of heavy base64 string
              mapData.preview_image = `assets/ban_do/_custom_ban_do/${name}.png`;

              const jsonPath = path.join(targetDir, `${name}.json`);
              fs.writeFileSync(jsonPath, JSON.stringify(mapData, null, 2), 'utf-8');

              res.setHeader('Content-Type', 'application/json');
              res.end(JSON.stringify({
                success: true,
                savedPath: `assets/ban_do/_custom_ban_do/${name}.json`,
                previewPath: `assets/ban_do/_custom_ban_do/${name}.png`,
              }));
            } catch (err: any) {
              res.statusCode = 500;
              res.setHeader('Content-Type', 'application/json');
              res.end(JSON.stringify({ success: false, error: err?.message || 'Lỗi lưu bản đồ' }));
            }
          });
        });

        // API: Save Assembled 2D Character to asset_2ds/nhan_vat/_lap_rap/
        server.middlewares.use('/api/save-2d-character', (req, res, next) => {
          if (req.method !== 'POST') return next();
          let body = '';
          req.on('data', (chunk) => { body += chunk; });
          req.on('end', () => {
            try {
              const data = JSON.parse(body);
              const name = (data.filename || data.name || 'nhan_vat_2d')
                .replace(/[^a-zA-Z0-9_\u00C0-\u024F\u1E00-\u1EFF\-]/g, '_')
                .toLowerCase();
              const targetDir = path.join(__dirname, 'asset_2ds', 'nhan_vat', '_lap_rap');
              if (!fs.existsSync(targetDir)) {
                fs.mkdirSync(targetDir, { recursive: true });
              }

              const profileData = { ...(data.profileData || data) };
              const previewImg = data.previewImageBase64 || profileData.preview_image;

              if (previewImg && previewImg.includes('base64,')) {
                const base64Data = previewImg.split('base64,')[1];
                const imgPath = path.join(targetDir, `${name}.png`);
                fs.writeFileSync(imgPath, Buffer.from(base64Data, 'base64'));
              }

              profileData.preview_image = `asset_2ds/nhan_vat/_lap_rap/${name}.png`;
              const jsonPath = path.join(targetDir, `${name}.json`);
              fs.writeFileSync(jsonPath, JSON.stringify(profileData, null, 2), 'utf-8');

              res.setHeader('Content-Type', 'application/json');
              res.end(JSON.stringify({
                success: true,
                savedPath: `asset_2ds/nhan_vat/_lap_rap/${name}.json`,
                previewPath: `asset_2ds/nhan_vat/_lap_rap/${name}.png`,
              }));
            } catch (err: any) {
              res.statusCode = 500;
              res.setHeader('Content-Type', 'application/json');
              res.end(JSON.stringify({ success: false, error: err?.message || 'Lỗi lưu nhân vật 2D' }));
            }
          });
        });

        // API: Save Custom 2D Map to asset_2ds/ban_do/_custom_ban_do/
        server.middlewares.use('/api/save-2d-map', (req, res, next) => {
          if (req.method !== 'POST') return next();
          let body = '';
          req.on('data', (chunk) => { body += chunk; });
          req.on('end', () => {
            try {
              const data = JSON.parse(body);
              const name = (data.filename || data.name || 'custom_map_2d')
                .replace(/[^a-zA-Z0-9_\u00C0-\u024F\u1E00-\u1EFF\-]/g, '_')
                .toLowerCase();
              const targetDir = path.join(__dirname, 'asset_2ds', 'ban_do', '_custom_ban_do');
              if (!fs.existsSync(targetDir)) {
                fs.mkdirSync(targetDir, { recursive: true });
              }

              const mapData = { ...(data.mapData || data) };
              const previewImg = data.previewImageBase64 || mapData.preview_image;

              if (previewImg && previewImg.includes('base64,')) {
                const base64Data = previewImg.split('base64,')[1];
                const imgPath = path.join(targetDir, `${name}.png`);
                fs.writeFileSync(imgPath, Buffer.from(base64Data, 'base64'));
              }

              mapData.preview_image = `asset_2ds/ban_do/_custom_ban_do/${name}.png`;
              const jsonPath = path.join(targetDir, `${name}.json`);
              fs.writeFileSync(jsonPath, JSON.stringify(mapData, null, 2), 'utf-8');

              res.setHeader('Content-Type', 'application/json');
              res.end(JSON.stringify({
                success: true,
                savedPath: `asset_2ds/ban_do/_custom_ban_do/${name}.json`,
                previewPath: `asset_2ds/ban_do/_custom_ban_do/${name}.png`,
              }));
            } catch (err: any) {
              res.statusCode = 500;
              res.setHeader('Content-Type', 'application/json');
              res.end(JSON.stringify({ success: false, error: err?.message || 'Lỗi lưu bản đồ 2D' }));
            }
          });
        });

        // API: List top-level folders in asset_2ds/ for auto-discovery
        server.middlewares.use('/api/list-2d-folders', (_req, res) => {
          try {
            const asset2dDir = path.join(__dirname, 'asset_2ds');
            const entries = fs.readdirSync(asset2dDir, { withFileTypes: true });
            const folders = entries
              .filter((e) => e.isDirectory() && !e.name.startsWith('.'))
              .map((e) => e.name);
            res.setHeader('Content-Type', 'application/json');
            res.end(JSON.stringify(folders));
          } catch (err: any) {
            res.statusCode = 500;
            res.setHeader('Content-Type', 'application/json');
            res.end(JSON.stringify({ error: err?.message }));
          }
        });

        // API: Live scan assets in a specific folder under asset_2ds/
        // Usage: /api/scan-2d-assets?folder=nhan_vat/mat  or  ?folder=ban_do/hau_canh
        // Returns items in real-time from filesystem — no need to run _scan_asset_2ds.bat
        server.middlewares.use('/api/scan-2d-assets', (req, res) => {
          try {
            const url = new URL(req.url || '', `http://${req.headers.host}`);
            const folderParam = url.searchParams.get('folder') || '';
            const asset2dDir = path.join(__dirname, 'asset_2ds');
            const targetDir = path.join(asset2dDir, folderParam);

            // Security: ensure resolved path is within asset_2ds
            const resolved = path.resolve(targetDir);
            if (!resolved.startsWith(path.resolve(asset2dDir))) {
              res.statusCode = 403;
              res.end(JSON.stringify({ error: 'Access denied' }));
              return;
            }

            if (!fs.existsSync(targetDir) || !fs.statSync(targetDir).isDirectory()) {
              res.setHeader('Content-Type', 'application/json');
              res.end(JSON.stringify([]));
              return;
            }

            const imageExts = ['.png', '.jpg', '.jpeg', '.webp', '.gif', '.svg'];
            const audioExts = ['.mp3', '.wav', '.ogg', '.m4a'];
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
                const isImage = imageExts.includes(ext);
                const isAudio = audioExts.includes(ext);
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
            res.setHeader('Content-Type', 'application/json');
            res.setHeader('Access-Control-Allow-Origin', '*');
            res.end(JSON.stringify(items));
          } catch (err: any) {
            res.statusCode = 500;
            res.setHeader('Content-Type', 'application/json');
            res.end(JSON.stringify({ error: err?.message }));
          }
        });

        // Static Asset_2Ds Serving Middleware
        server.middlewares.use('/asset_2ds', (req, res, next) => {
          const rawUrl = (req.url || '').split('?')[0];
          const decoded = decodeURIComponent(rawUrl).replace(/^\/+/, '');
          const asset2dDir = path.join(__dirname, 'asset_2ds');
          const resolvedPath = path.join(asset2dDir, decoded);

          if (fs.existsSync(resolvedPath) && fs.statSync(resolvedPath).isFile()) {
            if (req.url?.includes('?import')) return next();
            const ext = path.extname(resolvedPath).toLowerCase();
            const mimeTypes: Record<string, string> = {
              '.png': 'image/png',
              '.jpg': 'image/jpeg',
              '.jpeg': 'image/jpeg',
              '.webp': 'image/webp',
              '.gif': 'image/gif',
              '.svg': 'image/svg+xml',
              '.mp3': 'audio/mpeg',
              '.wav': 'audio/wav',
              '.json': 'application/json; charset=utf-8',
            };
            res.setHeader('Content-Type', mimeTypes[ext] || 'application/octet-stream');
            res.setHeader('Access-Control-Allow-Origin', '*');
            fs.createReadStream(resolvedPath).pipe(res);
            return;
          }
          next();
        });

        // Static Assets Serving with JSON and Media support & Smart Alias Resolution
        const findAssetFile = (reqPath: string): string | null => {
          const assetsDir = path.join(__dirname, 'assets');
          const direct = path.join(assetsDir, reqPath);
          if (fs.existsSync(direct) && fs.statSync(direct).isFile()) return direct;

          // Check known alias transformations
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

          // Check stripped leading underscores in basename (e.g. __đảo_chính.json -> đảo_chính.json)
          const dir = path.dirname(reqPath);
          const base = path.basename(reqPath);
          const cleanBase = base.replace(/^_+/, '');
          if (cleanBase !== base) {
            const cleanCandidate = path.join(assetsDir, dir, cleanBase);
            if (fs.existsSync(cleanCandidate) && fs.statSync(cleanCandidate).isFile()) return cleanCandidate;
          }

          // Check if file sits directly in assets root (e.g. doan_tau_sat.glb, xe_bus.glb)
          const rootCandidate = path.join(assetsDir, base);
          if (fs.existsSync(rootCandidate) && fs.statSync(rootCandidate).isFile()) return rootCandidate;
          const rootCleanCandidate = path.join(assetsDir, cleanBase);
          if (fs.existsSync(rootCleanCandidate) && fs.statSync(rootCleanCandidate).isFile()) return rootCleanCandidate;

          return null;
        };

        server.middlewares.use('/assets', (req, res, next) => {
          const rawUrl = (req.url || '').split('?')[0];
          const decoded = decodeURIComponent(rawUrl).replace(/^\/+/, '');
          const resolvedPath = findAssetFile(decoded);

          if (resolvedPath && fs.existsSync(resolvedPath) && fs.statSync(resolvedPath).isFile()) {
            // Only delegate to Vite if explicitly requested as an ES module import (?import)
            if (req.url?.includes('?import')) {
              return next();
            }

            const ext = path.extname(resolvedPath).toLowerCase();
            const mimeTypes: Record<string, string> = {
              '.vrm': 'model/gltf-binary',
              '.glb': 'model/gltf-binary',
              '.gltf': 'model/gltf+json',
              '.bin': 'application/octet-stream',
              '.mp3': 'audio/mpeg',
              '.wav': 'audio/wav',
              '.png': 'image/png',
              '.jpg': 'image/jpeg',
              '.jpeg': 'image/jpeg',
              '.webp': 'image/webp',
              '.json': 'application/json; charset=utf-8',
            };
            res.setHeader('Content-Type', mimeTypes[ext] || 'application/octet-stream');
            res.setHeader('Access-Control-Allow-Origin', '*');
            fs.createReadStream(resolvedPath).pipe(res);
            return;
          }
          next();
        });
      },
    },
  ],
  server: {
    port: 5173,
    host: true,
  },
});
