import { z } from "zod";
import { createListSchema } from "@/shared/schemas/list-schema";

export const ClientResponseSchema = z.object({
  id: z.string(),
  name: z.string(),
  email: z.string().nullable(),
  phoneNumber: z.string().nullable(),
});

export const ClientListSchema = createListSchema(ClientResponseSchema);

const optionalEmail = z
  .string()
  .nullable()
  .refine((value) => !value?.trim() || z.email().safeParse(value).success, {
    message: "Enter a valid email address.",
  });

const optionalPhoneNumber = z
  .string()
  .nullable()
  .refine(
    (value) => {
      if (!value?.trim()) {
        return true;
      }
      const digitCount = value.replace(/\D/g, "").length;
      return digitCount >= 10 && digitCount <= 15;
    },
    { message: "Enter a valid phone number." },
  );

export const CreateClientRequestObjectSchema = z.object({
  name: z.string().min(1),
  email: optionalEmail,
  phoneNumber: optionalPhoneNumber,
});

export const CreateClientRequestSchema = CreateClientRequestObjectSchema.refine(
  (value) => Boolean(value.email?.trim()) || Boolean(value.phoneNumber?.trim()),
  { message: "Enter an email or a phone number.", path: ["contact"] },
);

export type ClientResponse = z.infer<typeof ClientResponseSchema>;
export type ClientList = z.infer<typeof ClientListSchema>;
export type CreateClientRequest = z.infer<typeof CreateClientRequestSchema>;
