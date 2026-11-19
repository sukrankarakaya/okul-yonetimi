/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [react()],
  server: { port: 5173, strictPort: true },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    // resource hygiene: a single worker process, no watch mode (docs/testing.md).
    // 'forks' (process isolation, vitest default): one full run aborted a worker thread (exit 134, plan 0001 M-3).
    pool: 'forks',
    maxWorkers: 1,
    watch: false,
  },
});
