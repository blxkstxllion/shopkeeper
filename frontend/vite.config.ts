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
        navigateFallbackDenylist: [/^\/api\//],
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
      includeAssets: ['favicon.svg', 'apple-touch-icon.png'],
      manifest: {
        name: 'The Shop Keeper',
        short_name: 'Shop Keeper',
        description: 'Point of sale and inventory management, built to keep working offline.',
        theme_color: '#1a0533',
        background_color: '#1a0533',
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
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    globals: true,
  },
})
