import { describe, it, expect } from 'vitest';
import { authReducer, setAccessToken } from '@/shared/auth/authSlice';

describe('authSlice', () => {
  it('starts with no token', () => {
    expect(authReducer(undefined, { type: '@@init' }).accessToken).toBeNull();
  });

  it('stores a token', () => {
    const state = authReducer(undefined, setAccessToken('token-123'));

    expect(state.accessToken).toBe('token-123');
  });

  it('clears the token', () => {
    const state = authReducer({ accessToken: 'old' }, setAccessToken(null));

    expect(state.accessToken).toBeNull();
  });
});
