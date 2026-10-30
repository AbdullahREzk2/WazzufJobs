// src/app/features/auth/login/login.ts
import { Component, AfterViewInit, NgZone } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { ProfilePhotoService } from '../../../core/services/profile-photo.service';
import { environment } from '../../../../environments/environment';
import { isGoogleSignInEnabled } from '../../../core/utils/google-sign-in';

declare global {
  interface Window {
    google?: {
      accounts: {
        id: {
          initialize: (config: Record<string, unknown>) => void;
          renderButton: (element: HTMLElement, config: Record<string, unknown>) => void;
        };
      };
    };
  }
}

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './login.html',
  styleUrls: ['./login.scss']
})
export class Login implements AfterViewInit {
  email    = '';
  password = '';
  loading  = false;
  error    = '';
  showPass = false;
  // false while no Google client id is configured: no button is rendered and Google's script is never loaded
  googleEnabled = isGoogleSignInEnabled();

  constructor(
    private authService: AuthService,
    private profilePhoto: ProfilePhotoService,
    private router: Router,
    private zone: NgZone
  ) {}

  ngAfterViewInit() {
    if (!this.googleEnabled) return;

    const initGoogle = () => {
      if (!window.google?.accounts?.id) return;
      window.google.accounts.id.initialize({
        client_id: environment.googleClientId,
        callback: (response: { credential: string }) => this.handleGoogleCredential(response.credential)
      });
      const container = document.getElementById('google-signin-btn');
      if (container) {
        window.google.accounts.id.renderButton(container, {
          theme: 'outline',
          size: 'large',
          width: container.offsetWidth || 360,
          text: 'signin_with'
        });
      }
    };

    if (window.google?.accounts?.id) {
      initGoogle();
      return;
    }

    const script = document.createElement('script');
    script.src = 'https://accounts.google.com/gsi/client';
    script.async = true;
    script.defer = true;
    script.onload = () => initGoogle();
    document.head.appendChild(script);
  }

  login() {
    if (!this.email || !this.password) {
      this.error = 'Please fill in all fields.';
      return;
    }

    this.loading = true;
    this.error   = '';

    this.authService.login({ email: this.email, password: this.password })
      .subscribe({
        next: () => this.onAuthSuccess(),
        error: (err) => {
          const body = err.error;
          if (body?.errors?.length) {
            this.error = body.errors.join(' ');
          } else if (body?.detail) {
            this.error = body.detail;
          } else if (body?.title) {
            this.error = body.title;
          } else if (err.status === 0) {
            this.error = 'Cannot reach the API. Is the backend running?';
          } else {
            this.error = 'Invalid email or password.';
          }
          this.loading = false;
        }
      });
  }

  private handleGoogleCredential(credential: string) {
    this.zone.run(() => {
      this.loading = true;
      this.error = '';
      this.authService.googleLogin(credential).subscribe({
        next: () => this.onAuthSuccess(),
        error: err => {
          this.error = err.error?.detail || 'Google sign-in failed.';
          this.loading = false;
        }
      });
    });
  }

  private onAuthSuccess() {
    this.loading = false;
    this.profilePhoto.refresh();
    if (this.authService.isAdmin()) {
      this.router.navigate(['/admin']);
    } else {
      this.router.navigate(['/dashboard']);
    }
  }
}
