import { AuthResponse } from './models';

/** A login response for specs: access token valid 15 minutes, refresh token 7 days. */
export function authResponse(overrides: Partial<AuthResponse> = {}): AuthResponse {
  const inMinutes = (m: number) => new Date(Date.now() + m * 60_000).toISOString();
  return {
    token: 'access-1',
    expiresAt: inMinutes(15),
    refreshToken: 'refresh-1',
    refreshExpiresAt: inMinutes(7 * 24 * 60),
    user: { id: 7, email: 'priya@pharmacy.test', fullName: 'Priya', role: 'Pharmacist', isActive: true, createdAt: '2026-10-01' },
    ...overrides
  };
}
