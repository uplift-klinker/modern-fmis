import { execFileSync } from 'node:child_process';
import { fileURLToPath, URL } from 'node:url';

const repoRoot = fileURLToPath(new URL('../', import.meta.url));
const apiUrl = 'http://localhost:8080';

async function waitForApi(timeoutMs: number): Promise<void> {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    try {
      const response = await fetch(`${apiUrl}/openapi/v1.json`);
      if (response.ok) {
        return;
      }
    } catch {
      // backend is not accepting connections yet
    }
    await new Promise((resolve) => setTimeout(resolve, 1000));
  }
  throw new Error(`Backend API did not become ready at ${apiUrl} within ${timeoutMs}ms`);
}

export async function setup(): Promise<void> {
  execFileSync('docker', ['compose', 'up', '-d', '--build', 'backend'], {
    cwd: repoRoot,
    stdio: 'inherit',
  });
  await waitForApi(120_000);
}

export async function teardown(): Promise<void> {
  execFileSync('docker', ['compose', 'down'], { cwd: repoRoot, stdio: 'inherit' });
}
