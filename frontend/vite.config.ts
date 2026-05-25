import { defineConfig } from 'vitest/config'
import { fileURLToPath } from 'node:url'
import react from '@vitejs/plugin-react'
import { tanstackRouter } from '@tanstack/router-plugin/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    tanstackRouter({
      target: 'react',
      autoCodeSplitting: true,
      generatedRouteTree: './src/app/routeTree.gen.ts',
    }),
    react(),
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: process.env.VITE_BACKEND_PROXY_URL ?? 'http://localhost:5231',
        changeOrigin: true,
        secure: false,
      },
      '/hubs': {
        target: process.env.VITE_BACKEND_PROXY_URL ?? 'http://localhost:5231',
        changeOrigin: true,
        secure: false,
        ws: true,
      },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: './src/testing/setup.ts',
    globals: true,
    css: true,
    // The MSW server is a shared module-level singleton; running test files in
    // parallel within the same process causes per-test handler overrides
    // (server.use(...)) to race. Disable file-level parallelism to keep the
    // suite deterministic. Tests inside one file still run sequentially.
    fileParallelism: false,
    coverage: {
      provider: 'v8',
      reporter: ['text', 'html', 'json-summary', 'lcov'],
      // Exclude scaffolding, generated code, and entry shims from the
      // coverage denominator so the thresholds reflect testable surface.
      exclude: [
        'node_modules/**',
        'dist/**',
        'coverage/**',
        '**/*.config.*',
        'src/app/main.tsx',
        'src/app/provider.tsx',
        'src/app/router.tsx',
        'src/app/routeTree.gen.ts',
        'src/app/theme.ts',
        'src/testing/**',
        'src/**/*.unit.test.{ts,tsx}',
        'src/**/*.test.{ts,tsx}',
        'src/**/index.ts',
        'src/types/**',
      ],
      // Floors slightly below the current run so small fluctuations don't
      // fail CI but a real regression does. Raise these as coverage grows.
      thresholds: {
        statements: 48,
        branches: 42,
        functions: 46,
        lines: 50,
      },
    },
  },
})
