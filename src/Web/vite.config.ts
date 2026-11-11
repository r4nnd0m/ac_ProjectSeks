import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Entwicklungsmodus auf Port 8080 (E1). Der Proxy leitet die API-Aufrufe
// wie nginx im Container weiter, damit dieselben relativen Pfade aus
// public/config.js funktionieren. Ziele = Ports aus docker-compose.yml.
const proxy = {
  '/api/auth': { target: 'http://127.0.0.1:8081', rewrite: (p: string) => p.replace(/^\/api\/auth/, '') },
  '/api/scan': { target: 'http://127.0.0.1:8082', rewrite: (p: string) => p.replace(/^\/api\/scan/, '') },
  '/api/news': { target: 'http://127.0.0.1:8083', rewrite: (p: string) => p.replace(/^\/api\/news/, '') },
};

export default defineConfig({
  plugins: [react()],
  server: {
    port: 8080,
    strictPort: true,
    proxy,
  },
  preview: {
    port: 8080,
    strictPort: true,
    proxy,
  },
});
