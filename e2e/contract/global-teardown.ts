import { execFileSync } from "node:child_process";
import { fileURLToPath, URL } from "node:url";

const repoRoot = fileURLToPath(new URL("../../", import.meta.url));

export default async function globalTeardown(): Promise<void> {
  execFileSync("docker", ["compose", "down"], { cwd: repoRoot, stdio: "inherit" });
}
