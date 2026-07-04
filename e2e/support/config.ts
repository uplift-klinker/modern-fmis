export function requireEnv(name: string): string {
  const value = process.env[name];
  if (!value) {
    throw new Error(`Required environment variable ${name} is not set.`);
  }
  return value;
}

export interface E2eConfig {
  frontendUrl: string;
  backendUrl: string;
  authDomain: string;
  audience: string;
  clientId: string;
  e2eClientId: string;
  clientSecret: string;
  username: string;
  password: string;
}

export function e2eConfig(): E2eConfig {
  return {
    frontendUrl: requireEnv("E2E_FRONTEND_URL"),
    backendUrl: requireEnv("E2E_BACKEND_URL"),
    authDomain: requireEnv("E2E_AUTH_DOMAIN"),
    audience: requireEnv("E2E_AUTH_AUDIENCE"),
    clientId: requireEnv("E2E_SPA_CLIENT_ID"),
    e2eClientId: requireEnv("E2E_CLIENT_ID"),
    clientSecret: requireEnv("E2E_CLIENT_SECRET"),
    username: requireEnv("E2E_USERNAME"),
    password: requireEnv("E2E_PASSWORD"),
  };
}
