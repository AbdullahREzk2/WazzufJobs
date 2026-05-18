// src/app/core/services/auth.service.ts
import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AuthResponse,
  LoginRequest,
  RegisterRequest,
  JwtPayload
} from '../models/auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly TOKEN_KEY    = 'wazzuf_token';
  private readonly REFRESH_KEY  = 'wazzuf_refresh';
  private readonly USER_KEY     = 'wazzuf_user';

  // reactive signals — components auto-update when these change
  isLoggedIn  = signal<boolean>(this.hasValidToken());
  currentUser = signal<AuthResponse | null>(this.getStoredUser());

  constructor(private http: HttpClient, private router: Router) {}

  // ── Auth endpoints ──────────────────────────────────

  login(request: LoginRequest) {
    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/auth/login`, request)
      .pipe(tap(response => this.storeAuth(response)));
  }

  register(request: RegisterRequest) {
    return this.http
      .post<void>(`${environment.apiUrl}/auth/register`, request);
  }

  confirmEmail(userId: string, code: string) {
    return this.http
      .get<void>(`${environment.apiUrl}/auth/confirm-email`, {
        params: { userId, code }
      });
  }

  forgetPassword(email: string) {
    return this.http
      .post<void>(`${environment.apiUrl}/auth/forget-password`, { email });
  }

  resetPassword(email: string, code: string, newPassword: string, confirmPassword: string) {
    return this.http
      .post<void>(`${environment.apiUrl}/auth/reset-password`, {
        email, code, newPassword, confirmPassword
      });
  }

  refreshToken() {
    const token        = this.getToken();
    const refreshToken = this.getRefreshToken();

    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/auth/refresh-token`, {
        token, refreshToken
      })
      .pipe(tap(response => this.storeAuth(response)));
  }

  logout() {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.REFRESH_KEY);
    localStorage.removeItem(this.USER_KEY);
    this.isLoggedIn.set(false);
    this.currentUser.set(null);
    this.router.navigate(['/login']);
  }

  // ── Token helpers ───────────────────────────────────

  getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(this.REFRESH_KEY);
  }

  isAdmin(): boolean {
    const payload = this.decodeToken();
    if (!payload) return false;
    const role = payload.role;
    return Array.isArray(role)
      ? role.includes('Admin')
      : role === 'Admin';
  }

  isUser(): boolean {
    const payload = this.decodeToken();
    if (!payload) return false;
    const role = payload.role;
    return Array.isArray(role)
      ? role.includes('User')
      : role === 'User';
  }

  getUserId(): string | null {
    return this.decodeToken()?.sub ?? null;
  }

  decodeToken(): JwtPayload | null {
    const token = this.getToken();
    if (!token) return null;
    try {
      const payload = token.split('.')[1];
      return JSON.parse(atob(payload)) as JwtPayload;
    } catch {
      return null;
    }
  }

  // ── Private ─────────────────────────────────────────

  private storeAuth(response: AuthResponse) {
    localStorage.setItem(this.TOKEN_KEY,   response.token);
    localStorage.setItem(this.REFRESH_KEY, response.refreshToken);
    localStorage.setItem(this.USER_KEY,    JSON.stringify(response));
    this.isLoggedIn.set(true);
    this.currentUser.set(response);
  }

  private hasValidToken(): boolean {
    const token = localStorage.getItem(this.TOKEN_KEY);
    if (!token) return false;
    try {
      const payload = JSON.parse(atob(token.split('.')[1])) as JwtPayload;
      return payload.exp * 1000 > Date.now();
    } catch {
      return false;
    }
  }

  private getStoredUser(): AuthResponse | null {
    const stored = localStorage.getItem(this.USER_KEY);
    return stored ? JSON.parse(stored) : null;
  }
}