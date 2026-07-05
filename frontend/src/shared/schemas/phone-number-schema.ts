import { z } from "zod";

export const PhoneNumberSchema = z.string().refine(
  (value) => {
    const trimmed = value.trim();
    if (!trimmed.startsWith("+")) {
      return false;
    }
    const digitCount = trimmed.replace(/\D/g, "").length;
    return digitCount >= 10 && digitCount <= 15;
  },
  { message: "Enter a valid phone number with a country code, e.g. +1 555 555 0100." },
);
