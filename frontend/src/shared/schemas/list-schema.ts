import { z } from 'zod';

export function createListSchema<TItem extends z.ZodType>(itemSchema: TItem) {
  return z.object({
    items: z.array(itemSchema),
    totalCount: z.number(),
  });
}
