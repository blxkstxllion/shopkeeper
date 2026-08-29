/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { VitePWA } from 'vite-plugin-pwa'
import path from 'node:path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    VitePWA({
      devOptions: {
        // Lets the service worker register under `vite dev` too, not just a production
        // build+preview - needed to actually exercise offline behavior against the
        // Docker dev stack rather than only trusting a prod build works.
        enabled: true,
        type: 'module',
      },
      // 'prompt', not 'autoUpdate': autoUpdate force-reloads the page the moment a new
      // service worker activates, which would blow away an in-progress, not-yet-synced
      // cart on a POS terminal. The app decides when it's safe to reload (see
      // src/offline/useServiceWorkerUpdate.ts).
      registerType: 'prompt',
      // App-shell only: this service worker makes the SPA itself load offline.
      // Product/customer/sale *data* offline support is a separate IndexedDB layer
      // (see src/offline/), deliberately not workbox runtime-caching of API
      // responses - one source of truth for offline data, not two caches that can
      // disagree.
      workbox: {
        globPatterns: ['**/*.{js,css,html,svg,png,ico,woff,woff2}'],
        // /downloads/* are static binaries served straight from the VPS (see
        // docker/Caddyfile), not part of the SPA - without this, a browser navigation to
        // a download link (a top-level <a href> click) gets caught by the SPA fallback
        // and silently redirected back to index.html instead of downloading the file.
        navigateFallbackDenylist: [/^\/api\//, /^\/downloads\//],
        runtimeCaching: [
          {
            // API calls always go to the network or fail explicitly - never served
            // stale from an HTTP cache. Offline reads are answered by IndexedDB
            // (src/offline/), not by intercepting these requests here.
            urlPattern: ({ url }) => url.pathname.startsWith('/api/'),
            handler: 'NetworkOnly',
          },
        ],
      },
      includeAssets: ['favicon.png', 'apple-touch-icon.png'],
      manifest: {
        name: 'The Shop Keeper',
        short_name: 'Shop Keeper',
        description: 'Point of sale and inventory management, built to keep working offline.',
        theme_color: '#1a7a3c',
        background_color: '#1a7a3c',
        display: 'standalone',
        start_url: '/',
        icons: [
          { src: 'pwa-192x192.png', sizes: '192x192', type: 'image/png' },
          { src: 'pwa-512x512.png', sizes: '512x512', type: 'image/png' },
          { src: 'pwa-maskable-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
        ],
      },
    }),
  ],
  resolve: {
    alias: {
      '@': path.resolve(import.meta.dirname, './src'),
    },
  },
  server: {
    port: 5173,
    // Docker Desktop on Windows doesn't reliably propagate inotify events for files
    // edited on the host through a bind mount into the container - chokidar's default
    // watcher then silently stops picking up changes after a while, serving stale
    // modules with no error. Polling is slower but actually notices edits.
    watch: {
      usePolling: true,
      interval: 300,
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    globals: true,
  },
})
