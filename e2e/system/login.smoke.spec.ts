import { test, expect } from '@playwright/test';
import { interactiveLogin } from '../support/auth';
import { e2eConfig } from '../support/config';

test('interactive Auth0 login reaches the authenticated app @smoke', async ({ page }) => {
  await interactiveLogin(page);

  await expect(page.getByRole('heading', { name: 'Welcome to modern-fmis' })).toBeVisible();
  await expect(page.getByRole('button', { name: e2eConfig().username })).toBeVisible();
});
