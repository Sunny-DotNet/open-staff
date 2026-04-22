import { fileURLToPath, URL } from 'node:url';

import tailwindcss from '@tailwindcss/vite';
import vue from '@vitejs/plugin-vue';
import { defineConfig, loadEnv } from 'vite';

import { viteTailwindReferencePlugin } from '../../internal/vite-config/src/plugins/tailwind-reference';

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  const vitePort = Number(
    env.VITE_PORT ??
      process.env.VITE_PORT ??
      '5173',
  );
  const proxyTarget =
    env.VITE_OPENSTAFF_PROXY_TARGET ??
    process.env.VITE_OPENSTAFF_PROXY_TARGET ??
    env.VITE_OPENSTAFF_BASE_URL ??
    process.env.VITE_OPENSTAFF_BASE_URL ??
    'http://127.0.0.1:5079';

  return {
    plugins: [viteTailwindReferencePlugin(), vue(), tailwindcss()],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    server: {
      host: '0.0.0.0',
      port: Number.isNaN(vitePort) ? 5173 : vitePort,
      strictPort: true,
      proxy: {
        '/api': {
          target: proxyTarget,
          changeOrigin: true,
        },
        '/hubs': {
          target: proxyTarget,
          changeOrigin: true,
          ws: true,
        },
        '/openapi': {
          target: proxyTarget,
          changeOrigin: true,
        },
      },
    },
  };
});
