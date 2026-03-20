import path from "node:path";
import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "."),
    },
  },
  test: {
    environment: "jsdom",
    setupFiles: ["./vitest.setup.ts"],
    clearMocks: true,
    mockReset: true,
    restoreMocks: true,
    pool: "forks",
    fileParallelism: false,
    maxWorkers: 1,
    include: ["app/**/*.test.ts", "app/**/*.test.tsx", "lib/**/*.test.ts", "lib/**/*.test.tsx"],
    exclude: ["**/node_modules/**", "**/.next/**"],
  },
});
