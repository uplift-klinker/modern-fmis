import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import { Routes } from 'react-router-dom';
import { renderWithProviders } from '@/testing/render-with-providers';
import { appRoutes } from '@/app/router';

describe('app routes', () => {
  it('redirects / to /welcome when authenticated', async () => {
    renderWithProviders(<Routes>{appRoutes}</Routes>, { route: '/', auth: { isAuthenticated: true } });

    expect(await screen.findByRole('heading', { name: /welcome to modern-fmis/i })).toBeInTheDocument();
  });

  it('serves the public /unauthorized route when unauthenticated', () => {
    renderWithProviders(<Routes>{appRoutes}</Routes>, { route: '/unauthorized', auth: { isAuthenticated: false } });

    expect(screen.getByRole('heading', { name: /unauthorized/i })).toBeInTheDocument();
  });
});
