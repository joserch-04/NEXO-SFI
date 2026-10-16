import { readFileSync } from 'node:fs';
import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  const https = env.NEXO_HTTPS_CERT && env.NEXO_HTTPS_KEY
    ? { cert: readFileSync(env.NEXO_HTTPS_CERT), key: readFileSync(env.NEXO_HTTPS_KEY) } : undefined;
  return { plugins: [react(), tailwindcss()], server: { host: 'localhost', port: 5173, strictPort: true, https }, preview: { host: 'localhost', port: 5173, strictPort: true, https } };
});
