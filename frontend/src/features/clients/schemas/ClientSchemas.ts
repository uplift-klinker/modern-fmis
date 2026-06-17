import { z } from 'zod';

export const ClientResponseSchema = z.object({
  id: z.string(),
  name: z.string(),
  email: z.string().nullable(),
  phoneNumber: z.string().nullable(),
});

export const ClientListSchema = z.object({
  items: z.array(ClientResponseSchema),
  totalCount: z.number(),
});

export const CreateClientRequestSchema = z
  .object({
    name: z.string().min(1),
    email: z.string().nullable(),
    phoneNumber: z.string().nullable(),
  })
  .refine(
    (value) => Boolean(value.email?.trim()) || Boolean(value.phoneNumber?.trim()),
    { message: 'Enter an email or a phone number.', path: ['contact'] },
  );
