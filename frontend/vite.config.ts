import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

const backend = 'http://127.0.0.1:8081'

export default defineConfig({
  plugins: [vue()],
  css: { postcss: { plugins: [] } },
  server: {
    proxy: {
      '/api': { target: backend },
      '/hubs': { target: backend, ws: true },
    },
  },
})
