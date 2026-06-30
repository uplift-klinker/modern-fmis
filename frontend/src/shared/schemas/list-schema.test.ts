import { describe, it, expect } from 'vitest';
import { z } from 'zod';
import { createListSchema } from '@/shared/schemas/list-schema';

describe('createListSchema', () => {
  it('builds a list schema with items and a total count', () => {
    const StringListSchema = createListSchema(z.string());

    const result = StringListSchema.safeParse({ items: ['a', 'b'], totalCount: 2 });

    expect(result.success).toBe(true);
  });

  it('rejects items that are not the item type', () => {
    const StringListSchema = createListSchema(z.string());

    const result = StringListSchema.safeParse({ items: [1], totalCount: 1 });

    expect(result.success).toBe(false);
  });

  it('rejects a missing total count', () => {
    const StringListSchema = createListSchema(z.string());

    const result = StringListSchema.safeParse({ items: [] });

    expect(result.success).toBe(false);
  });
});
