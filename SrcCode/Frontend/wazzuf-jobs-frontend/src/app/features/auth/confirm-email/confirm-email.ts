import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-confirm-email',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './confirm-email.html',
  styleUrls: ['./confirm-email.scss']
})
export class ConfirmEmail implements OnInit {
  loading = true;
  success = false;
  error = '';

  constructor(
    private route: ActivatedRoute,
    private authService: AuthService
  ) {}

  ngOnInit() {
    this.route.queryParamMap.subscribe(params => {
      const userId = params.get('userId');
      const code   = this.readCodeParam(params.get('code'));

      if (!userId || !code) {
        this.error = 'This confirmation link is invalid. Check your email for the correct button.';
        this.loading = false;
        return;
      }

      this.loading = true;
      this.error = '';

      this.authService.confirmEmail(userId, code).subscribe({
        next: () => {
          this.success = true;
          this.loading = false;
        },
        error: err => {
          this.error = this.parseApiError(err);
          this.loading = false;
        }
      });
    });
  }

  private readCodeParam(raw: string | null): string {
    if (!raw) return '';
    const trimmed = raw.trim();
    if (trimmed.includes(' ') && !trimmed.includes('+')) {
      return trimmed.replace(/ /g, '+');
    }
    return trimmed;
  }

  private parseApiError(err: { error?: { errors?: string[]; detail?: string } }): string {
    const body = err.error;
    if (body?.errors?.length) return body.errors.join(' ');
    if (body?.detail) return body.detail;
    return 'Confirmation failed. The link may have expired — try signing in or register again.';
  }
}
