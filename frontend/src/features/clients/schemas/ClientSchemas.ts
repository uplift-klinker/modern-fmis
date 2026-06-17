import { z } from 'zod';
import { createListSchema } from '@/shared/schemas/listSchema';

export const ClientResponseSchema = z.object({
  id: z.string(),
  name: z.string(),
  email: z.string().nullable(),
  phoneNumber: z.string().nullable(),
});

export const ClientListSchema = createListSchema(ClientResponseSchema);

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

export type ClientResponse = z.infer<typeof ClientResponseSchema>;
export type ClientList = z.infer<typeof ClientListSchema>;
export type CreateClientRequest = z.infer<typeof CreateClientRequestSchema>;
