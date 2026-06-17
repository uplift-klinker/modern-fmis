import { describe, it, expect, beforeAll } from 'vitest';
import {
  ClientResponseSchema,
  ClientListSchema,
  CreateClientRequestObjectSchema,
} from '@/features/clients/schemas/ClientSchemas';

const apiUrl = 'http://localhost:8080';

interface OpenApiDocument {
  components?: { schemas?: Record<string, { properties?: Record<string, unknown> }> };
}

let openapi: OpenApiDocument;

beforeAll(async () => {
  const response = await fetch(`${apiUrl}/openapi/v1.json`);
  openapi = (await response.json()) as OpenApiDocument;
});

function propertyNamesOf(schemaName: string): string[] {
  return Object.keys(openapi.components?.schemas?.[schemaName]?.properties ?? {}).sort();
}

describe('client contract matches the live backend OpenAPI document', () => {
  it('ClientResponseSchema matches ClientResponseModel', () => {
    expect(Object.keys(ClientResponseSchema.shape).sort()).toEqual(propertyNamesOf('ClientResponseModel'));
  });

  it('CreateClientRequestObjectSchema matches CreateClientRequestModel', () => {
    expect(Object.keys(CreateClientRequestObjectSchema.shape).sort()).toEqual(
      propertyNamesOf('CreateClientRequestModel'),
    );
  });

  it('ClientListSchema matches ListResultModelOfClientResponseModel', () => {
    expect(Object.keys(ClientListSchema.shape).sort()).toEqual(
      propertyNamesOf('ListResultModelOfClientResponseModel'),
    );
  });
});
