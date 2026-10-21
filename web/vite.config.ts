import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'node:path';

// Dev proxy lets the SPA call /api/* and /hubs/* on the same origin as Vite (:5173),
// forwarding to the API running either on the host (dotnet run, :5102) or inside compose (:8080).
// Set VITE_API_TARGET in .env / .env.local to point at either. SignalR WS upgrade is enabled below.
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  const target = env.VITE_API_TARGET ?? 'http://localhost:8080';

  return {
    plugins: [react()],
    resolve: {
      alias: { '@': path.resolve(process.cwd(), 'src') },
    },
    server: {
      port: 5173,
      proxy: {
        '/api': { target, changeOrigin: true },
        '/hubs': { target, ws: true, changeOrigin: true },
        '/health': { target, changeOrigin: true },
      },
    },
  };
});
