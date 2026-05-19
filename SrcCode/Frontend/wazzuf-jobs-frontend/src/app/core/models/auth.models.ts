// src/app/core/models/auth.models.ts
export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  confirmPassword: string;
}

export interface AuthResponse {
  id: string;
  email: string;
  userName: string;
  firstName: string;
  lastName: string;
  token: string;
  expiresIn: number;
  refreshToken: string;
  refreshTokenExpiration: string;
}

export interface JwtPayload {
  sub: string;
  email: string;
  given_name: string;
  family_name: string;
  /** Backend claim name (JSON array in token) */
  roles?: string | string[];
  /** Legacy / alternate claim */
  role?: string | string[];
  permissions?: string | string[];
  exp: number;
}