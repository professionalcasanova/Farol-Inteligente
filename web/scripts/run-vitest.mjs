import { spawnSync } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const rootDir = path.resolve(__dirname, "..");
const vitestEntry = path.resolve(rootDir, "node_modules", "vitest", "vitest.mjs");

const testFiles = [
  "lib/auth.test.ts",
  "lib/use-protected-session.test.ts",
  "app/page.test.tsx",
  "app/login/page.test.tsx",
  "app/dashboard/page.test.tsx",
  "app/imports/page.test.tsx",
];

for (const testFile of testFiles) {
  console.log(`\n[farol-web-tests] Running ${testFile}\n`);

  const result = spawnSync(process.execPath, [vitestEntry, "run", testFile], {
    cwd: rootDir,
    stdio: "inherit",
    env: process.env,
  });

  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}
