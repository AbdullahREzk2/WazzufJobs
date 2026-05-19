import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { PasswordCriteria } from '../../../shared/components/password-criteria/password-criteria';
import {
  isPasswordValid,
  validatePasswordPair
} from '../../../core/utils/password.validator';

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, PasswordCriteria],
  templateUrl: './reset-password.html',
  styleUrls: ['./reset-password.scss']
})
export class ResetPassword implements OnInit {
  email = '';
  code = '';
  newPassword = '';
  confirmPassword = '';
  loading = false;
  success = false;
  error = '';
  showPass = false;
  showConfirmPass = false;
  linkValid = false;

  constructor(
    private route: ActivatedRoute,
    private authService: AuthService,
    private router: Router
  ) {}

  ngOnInit() {
    this.route.queryParamMap.subscribe(params => {
      this.email = params.get('email') ?? '';
      this.code  = this.readCodeParam(params.get('code'));
      this.linkValid = !!(this.email && this.code);
    });
  }

  /** Identity tokens may use + which can become space in query strings. */
  private readCodeParam(raw: string | null): string {
    if (!raw) return '';
    const trimmed = raw.trim();
    if (trimmed.includes(' ') && !trimmed.includes('+')) {
      return trimmed.replace(/ /g, '+');
    }
    return trimmed;
  }

  get canSubmit(): boolean {
    return (
      this.linkValid &&
      !this.loading &&
      isPasswordValid(this.newPassword) &&
      this.newPassword === this.confirmPassword
    );
  }

  reset() {
    if (!this.linkValid) {
      this.error = 'This reset link is invalid or incomplete. Request a new one.';
      return;
    }

    const validationError = validatePasswordPair(this.newPassword, this.confirmPassword);
    if (validationError) {
      this.error = validationError;
      return;
    }

    this.loading = true;
    this.error = '';

    this.authService.resetPassword(this.email, this.code, this.newPassword).subscribe({
      next: () => {
        this.success = true;
        this.loading = false;
        setTimeout(() => this.router.navigate(['/login']), 2500);
      },
      error: err => {
        this.error = this.parseApiError(err);
        this.loading = false;
      }
    });
  }

  private parseApiError(err: { error?: { errors?: string[]; detail?: string; title?: string }; status?: number }): string {
    const body = err.error;
    if (body?.errors?.length) return body.errors.join(' ');
    if (body?.detail) return body.detail;
    if (body?.title && body.title !== 'One or more validation errors occurred.') return body.title;
    if (err.status === 0) return 'Cannot reach the server. Try again later.';
    return 'Reset failed. The link may have expired — request a new reset email.';
  }
}
