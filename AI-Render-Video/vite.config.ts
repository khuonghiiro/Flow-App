import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { serveAssetsPlugin } from './vite-plugins/serve-assets';

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [
    react(),
    serveAssetsPlugin(__dirname),
  ],

  // ─── Dev Server ───────────────────────────────────────────────────
  server: {
    port: 5173,
    host: true,
    // Pre-transform critical files on server start so they're ready
    // before the browser requests them (biggest dev-mode speedup)
    warmup: {
      clientFiles: [
        './src/main.tsx',
        './src/App.tsx',
        './src/styles/studio.css',
        './src/ui/StudioLayout.tsx',
        './src/ui/ViewportCanvas.tsx',
        './src/core/engine/**/*.ts',
        './src/core/camera/**/*.ts',
        './src/core/actors/**/*.ts',
        './src/core/timeline/**/*.ts',
        './src/core/weather/**/*.ts',
      ],
    },
  },

  // ─── Dependency Pre-bundling ──────────────────────────────────────
  // Pre-bundle heavy deps during dev so Vite doesn't re-transform them
  // each on first request. This is the #1 factor for dev page-load speed.
  optimizeDeps: {
    include: [
      'three',
      'three/examples/jsm/loaders/GLTFLoader.js',
      'three/examples/jsm/loaders/FBXLoader.js',
      'three/examples/jsm/loaders/OBJLoader.js',
      'three/examples/jsm/controls/OrbitControls.js',
      'three/examples/jsm/controls/TransformControls.js',
      'pixi.js',
      'react',
      'react-dom',
      'mp4-muxer',
      'lucide-react',
    ],
  },

  // ─── Build Optimization ───────────────────────────────────────────
  build: {
    // Target modern browsers only — skip unnecessary polyfills
    target: 'esnext',
    // Disable sourcemaps in production for faster builds
    sourcemap: false,
    // Increase chunk size warning to 800KB (Three.js alone is ~600KB)
    chunkSizeWarningLimit: 800,

    rollupOptions: {
      output: {
        // Split vendor libraries into separate cacheable chunks
        manualChunks: {
          'vendor-three': ['three'],
          'vendor-pixi': ['pixi.js'],
          'vendor-react': ['react', 'react-dom'],
          'vendor-media': ['mp4-muxer'],
          'vendor-icons': ['lucide-react'],
        },
      },
    },
  },
});
