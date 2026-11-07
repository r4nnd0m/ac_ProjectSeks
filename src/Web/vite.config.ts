import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Die Weboberfläche läuft im Dev-Modus auf Port 8080 (siehe Projektauftrag E1).
export default defineConfig({
  plugins: [react()],
  server: {
    port: 8080,
    strictPort: true,
  },
  preview: {
    port: 8080,
    strictPort: true,
  },
});
