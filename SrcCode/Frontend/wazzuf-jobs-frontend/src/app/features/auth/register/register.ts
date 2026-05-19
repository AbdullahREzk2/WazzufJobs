// src/app/features/auth/register/register.ts
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { PasswordCriteria } from '../../../shared/components/password-criteria/password-criteria';
import { validatePasswordPair } from '../../../core/utils/password.validator';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, PasswordCriteria],
  templateUrl: './register.html',
  styleUrls: ['./register.scss']
})
export class Register {
  firstName = '';
  lastName  = '';
  email     = '';
  password  = '';
  confirmPassword = '';
  loading   = false;
  error     = '';
  success   = false;
  showPass  = false;

  constructor(
    private authService: AuthService,
    private router: Router) {}

  register() {
    if (!this.firstName || !this.lastName || !this.email ||
        !this.password || !this.confirmPassword) {
      this.error = 'Please fill in all fields.';
      return;
    }

    const passwordError = validatePasswordPair(this.password, this.confirmPassword);
    if (passwordError) {
      this.error = passwordError;
      return;
    }

    this.loading = true;
    this.error   = '';

    this.authService.register({
      firstName:       this.firstName,
      lastName:        this.lastName,
      email:           this.email,
      password:        this.password,
      confirmPassword: this.confirmPassword
    }).subscribe({
      next: () => {
        this.success = true;
        this.loading = false;
      },
      error: (err) => {
        this.error   = err.error?.detail || 'Registration failed. Try again.';
        this.loading = false;
      }
    });
  }
}