import { describe, it, expect, vi } from 'vitest';
import { render } from '@testing-library/react';
import { useAuth } from '@/shared/auth/auth-context';

function ReadAuth() {
  useAuth();
  return null;
}

describe('useAuth', () => {
  it('throws when used outside an AuthProvider', () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});

    expect(() => render(<ReadAuth />)).toThrow(/within an AuthProvider/);

    consoleError.mockRestore();
  });
});
