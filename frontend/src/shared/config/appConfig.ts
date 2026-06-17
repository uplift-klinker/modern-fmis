import { z } from 'zod';

export const AppConfigSchema = z.object({
  apiBaseUrl: z.string().min(1),
  auth: z.object({
    domain: z.string().min(1),
    clientId: z.string().min(1),
    audience: z.string().min(1),
  }),
});

export type AppConfig = z.infer<typeof AppConfigSchema>;

export async function loadAppConfig(): Promise<AppConfig> {
  const response = await fetch('/config.json', { cache: 'no-store' });
  if (!response.ok) {
    throw new Error(`Failed to load config.json: ${response.status}`);
  }
  return AppConfigSchema.parse(await response.json());
}
