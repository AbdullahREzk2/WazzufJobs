// src/app/features/auth/login/login.ts
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './login.html',
  styleUrls: ['./login.scss']
})
export class Login {
  email    = '';
  password = '';
  loading  = false;
  error    = '';
  showPass = false;

  constructor(
    private authService: AuthService,
    private router: Router) {}

  login() {
    if (!this.email || !this.password) {
      this.error = 'Please fill in all fields.';
      return;
    }

    this.loading = true;
    this.error   = '';

    this.authService.login({ email: this.email, password: this.password })
      .subscribe({
        next: () => {
          this.loading = false;
          if (this.authService.isAdmin())
            this.router.navigate(['/admin']);
          else
            this.router.navigate(['/dashboard']);
        },
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
}