// src/app/core/services/auth.service.ts
import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { map, tap } from 'rxjs';
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
  userRoles   = signal<string[]>(this.readRolesFromStorage());

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
      .get<void>(`${environment.apiUrl}/auth/Confirm-Email`, {
        params: { userId, code }
      });
  }

  forgetPassword(email: string) {
    return this.http
      .post<void>(`${environment.apiUrl}/auth/forget-Password`, { email });
  }

  resetPassword(email: string, code: string, newPassword: string) {
    return this.http
      .post<void>(`${environment.apiUrl}/auth/Reset-Password`, {
        email, code, newPassword
      });
  }

  refreshToken() {
    const token        = this.getToken();
    const refreshToken = this.getRefreshToken();

    return this.http
      .post<AuthResponse | { value: AuthResponse }>(
        `${environment.apiUrl}/auth/refresh`,
        { token, refreshToken }
      )
      .pipe(
        map(res => ('value' in res && res.value ? res.value : res as AuthResponse)),
        tap(response => this.storeAuth(response))
      );
  }

  /** Update cached user after profile changes (token unchanged). */
  patchStoredUser(
    partial: Partial<
      Pick<AuthResponse, 'firstName' | 'lastName' | 'email' | 'userName'>
    >
  ) {
    const u = this.currentUser();
    if (!u) return;
    const next = { ...u, ...partial };
    localStorage.setItem(this.USER_KEY, JSON.stringify(next));
    this.currentUser.set(next);
  }

  logout() {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.REFRESH_KEY);
    localStorage.removeItem(this.USER_KEY);
    this.isLoggedIn.set(false);
    this.currentUser.set(null);
    this.userRoles.set([]);
    this.router.navigate(['/login']);
  }

  // ── Token helpers ───────────────────────────────────

  getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(this.REFRESH_KEY);
  }

  getRoles(): string[] {
    const payload = this.decodeToken();
    if (!payload) return [];
    return this.normalizeRoles(payload.roles ?? payload.role);
  }

  isAdmin(): boolean {
    return this.getRoles().includes('Admin');
  }

  isUser(): boolean {
    const roles = this.getRoles();
    return roles.includes('User') || roles.length === 0;
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
    this.userRoles.set(this.getRoles());
  }

  private readRolesFromStorage(): string[] {
    return this.hasValidToken() ? this.getRoles() : [];
  }

  private normalizeRoles(raw: string | string[] | undefined): string[] {
    if (!raw) return [];
    if (Array.isArray(raw)) return raw;
    try {
      const parsed = JSON.parse(raw);
      return Array.isArray(parsed) ? parsed : [String(parsed)];
    } catch {
      return [raw];
    }
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