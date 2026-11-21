import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

const backend = 'http://127.0.0.1:8081'

export default defineConfig({
  plugins: [vue()],
  // Keep each optional font subset out of the stylesheet so it loads only for matching text.
  build: { assetsInlineLimit: 0 },
  css: { postcss: { plugins: [] } },
  server: {
    proxy: {
      '/api': { target: backend },
      '/hubs': { target: backend, ws: true },
    },
  },
})
