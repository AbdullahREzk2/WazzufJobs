import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-forget-password',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './forget-password.html',
  styleUrls: ['./forget-password.scss']
})
export class ForgetPassword {
  email = '';
  loading = false;
  success = false;
  error = '';

  constructor(private authService: AuthService) {}

  submit() {
    if (!this.email) {
      this.error = 'Please enter your email.';
      return;
    }

    this.loading = true;
    this.error = '';

    this.authService.forgetPassword(this.email).subscribe({
      next: () => {
        this.success = true;
        this.loading = false;
      },
      error: err => {
        this.error = err.error?.detail || 'Could not send reset email.';
        this.loading = false;
      }
    });
  }
}
