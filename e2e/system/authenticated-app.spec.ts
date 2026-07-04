import { test, expect } from "@playwright/test";
import { generateToken, seedAuthSession } from "../support/auth";

test("a generated token short-circuits login and renders the authenticated app", async ({
  page,
}) => {
  const token = await generateToken();
  await seedAuthSession(page, token);

  await page.goto("/welcome");

  await expect(page.getByRole("heading", { name: "Welcome to modern-fmis" })).toBeVisible();
  await expect(page.getByRole("button", { name: /@/ })).toBeVisible();
});
