import path from 'node:path'
import { defineConfig, createLogger } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// Suppress harmless proxy socket reset/abort errors on browser tab reload
const HARMLESS_SOCKET_ERRORS = [
  'ECONNABORTED',
  'ECONNRESET',
  'EPIPE',
  'ETIMEDOUT',
  'ERR_STREAM_WRITE_AFTER_FIN',
  'This socket has been ended by the other party',
  'write after end',
  'writeAfterFIN',
]

const logger = createLogger()
const originalError = logger.error
logger.error = (msg, options) => {
  const combined = `${typeof msg === 'string' ? msg : ''} ${options?.error?.message || ''} ${options?.error?.code || ''} ${options?.error?.stack || ''}`
  if (HARMLESS_SOCKET_ERRORS.some((pattern) => combined.includes(pattern))) {
    return
  }
  originalError(msg, options)
}


export default defineConfig({
  customLogger: logger,
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://127.0.0.1:8100',
      '/ws': {
        target: 'ws://127.0.0.1:8100',
        ws: true,
        configure: (proxy) => {
          proxy.on('error', (err: any) => {
            if (['ECONNABORTED', 'ECONNRESET', 'EPIPE'].includes(err?.code)) return
          })
          proxy.on('proxyReqWs', (_proxyReq, _req, socket: any) => {
            socket.on('error', (err: any) => {
              if (['ECONNABORTED', 'ECONNRESET', 'EPIPE'].includes(err?.code)) return
            })
          })
        },
      },
      '/health': 'http://127.0.0.1:8100',
    }
  },
  build: { outDir: 'dist' }
})

