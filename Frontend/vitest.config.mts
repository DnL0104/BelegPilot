import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tsconfigPaths from 'vite-tsconfig-paths'
import { fileURLToPath } from 'node:url'

export default defineConfig({
  plugins: [tsconfigPaths(), react()],
  resolve: {
    // Explicit @ alias so vi.mock('@/...') paths resolve reliably. The
    // tsconfig-paths plugin alone does not cover vi.mock() hoisted paths,
    // especially once test files are excluded from tsconfig (see
    // tsconfig.json `exclude`, added to keep `next build` type-check clean).
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./vitest.setup.ts'],
    globals: true,
    exclude: ['**/node_modules/**', '**/e2e/**'],
  },
})
