import { z } from "zod";

export const EmailSchema = z.email({ message: "Enter a valid email address." });
