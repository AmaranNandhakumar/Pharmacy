export type UserRole = 'Admin' | 'Pharmacist' | 'Technician';

export const USER_ROLES: UserRole[] = ['Admin', 'Pharmacist', 'Technician'];

export interface User {
  id: number;
  email: string;
  fullName: string;
  role: UserRole;
  isActive: boolean;
  createdAt: string;
}

export interface AuthResponse {
  token: string;
  expiresAt: string;
  refreshToken: string;
  refreshExpiresAt: string;
  user: User;
}
