import { request, type APIRequestContext } from "@playwright/test";
import { e2eConfig } from "./config";

export async function authorizedRequest(accessToken: string): Promise<APIRequestContext> {
  const config = e2eConfig();
  return request.newContext({
    baseURL: config.backendUrl,
    extraHTTPHeaders: { authorization: `Bearer ${accessToken}` },
  });
}
