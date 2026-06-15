import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// The API runs on http://localhost:5080 (see README). We proxy /api to avoid CORS in dev.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
    },
  },
});
