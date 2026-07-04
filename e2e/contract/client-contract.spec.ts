import { test, expect } from '@playwright/test';
import {
  ClientResponseSchema,
  ClientListSchema,
  CreateClientRequestObjectSchema,
} from '@/features/clients/schemas/client-schemas';

interface OpenApiDocument {
  components?: { schemas?: Record<string, { properties?: Record<string, unknown> }> };
}

async function propertyNamesOf(schemaName: string): Promise<string[]> {
  const response = await fetch('http://localhost:8080/openapi/v1.json');
  const openapi = (await response.json()) as OpenApiDocument;
  return Object.keys(openapi.components?.schemas?.[schemaName]?.properties ?? {}).sort();
}

test('ClientResponseSchema matches ClientResponseModel', async () => {
  expect(Object.keys(ClientResponseSchema.shape).sort()).toEqual(await propertyNamesOf('ClientResponseModel'));
});

test('CreateClientRequestObjectSchema matches CreateClientRequestModel', async () => {
  expect(Object.keys(CreateClientRequestObjectSchema.shape).sort()).toEqual(
    await propertyNamesOf('CreateClientRequestModel'),
  );
});

test('ClientListSchema matches ListResultModelOfClientResponseModel', async () => {
  expect(Object.keys(ClientListSchema.shape).sort()).toEqual(
    await propertyNamesOf('ListResultModelOfClientResponseModel'),
  );
});
