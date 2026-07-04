export const API_TAGS = { Client: "Client" } as const;

export type ApiTag = (typeof API_TAGS)[keyof typeof API_TAGS];
