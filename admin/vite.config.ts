import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// The API's development CORS policy allows http://localhost:5173, so the dev
// server is pinned to that port rather than falling back to the next free one.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: { port: 5173, strictPort: true },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: false,
    // Pinned here rather than read from .env, which is not committed.
    env: { VITE_API_URL: 'http://localhost:5181' },
  },
})
