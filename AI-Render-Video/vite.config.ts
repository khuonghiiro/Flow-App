import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'path';
import fs from 'fs';

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [
    react(),
    {
      name: 'serve-assets',
      configureServer(server) {
        server.middlewares.use('/assets', (req, res, next) => {
          const rawUrl = (req.url || '').split('?')[0];
          const filePath = path.join(__dirname, 'assets', decodeURIComponent(rawUrl));
          if (fs.existsSync(filePath) && fs.statSync(filePath).isFile()) {
            const ext = path.extname(filePath).toLowerCase();
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
              '.json': 'application/json',
            };
            res.setHeader('Content-Type', mimeTypes[ext] || 'application/octet-stream');
            res.setHeader('Access-Control-Allow-Origin', '*');
            fs.createReadStream(filePath).pipe(res);
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
