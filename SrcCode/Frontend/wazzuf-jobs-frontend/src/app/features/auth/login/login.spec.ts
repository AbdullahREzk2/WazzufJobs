import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { Login } from './login';
import { AuthService } from '../../../core/services/auth.service';
import { isGoogleSignInEnabled } from '../../../core/utils/google-sign-in';

describe('Login', () => {
  let component: Login;
  let fixture: ComponentFixture<Login>;
  let http: HttpTestingController;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();

    fixture = TestBed.createComponent(Login);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('Google sign-in while no client id is configured (C0)', () => {
    it('treats empty, whitespace or missing client ids as disabled', () => {
      expect(isGoogleSignInEnabled('')).toBe(false);
      expect(isGoogleSignInEnabled('   ')).toBe(false);
      expect(isGoogleSignInEnabled(undefined)).toBe(false);
      expect(isGoogleSignInEnabled(null)).toBe(false);
      expect(isGoogleSignInEnabled('123.apps.googleusercontent.com')).toBe(true);
    });

    it('does not render the Google button or its divider', () => {
      const el: HTMLElement = fixture.nativeElement;
      expect(component.googleEnabled).toBe(false);
      expect(el.querySelector('#google-signin-btn')).toBeNull();
      expect(el.textContent).not.toContain('or continue with');
    });

    it('never loads the Google Identity script', () => {
      expect(document.querySelector('script[src*="accounts.google.com"]')).toBeNull();
    });

    it('refuses Google sign-in in the auth service without calling the API', async () => {
      const auth = TestBed.inject(AuthService);
      let message = '';
      try {
        await firstValueFrom(auth.googleLogin('any-token'));
      } catch (e) {
        message = (e as Error).message;
      }
      expect(message).toBe('Google sign-in is not configured.');
      http.expectNone(req => req.url.includes('/auth/google'));
    });
  });

  it('email/password sign-in still calls the login endpoint', () => {
    const auth = TestBed.inject(AuthService);
    auth.login({ email: 'candidate@example.test', password: 'Secret#123' }).subscribe();
    const req = http.expectOne(r => r.url.endsWith('/auth/login') && r.method === 'POST');
    expect(req.request.body).toEqual({ email: 'candidate@example.test', password: 'Secret#123' });
    req.flush({});
  });
});
