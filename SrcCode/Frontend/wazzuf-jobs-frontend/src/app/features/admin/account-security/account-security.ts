import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AccountService } from '../../../core/services/account.service';
import { AuthService } from '../../../core/services/auth.service';
import { PasswordCriteria } from '../../../shared/components/password-criteria/password-criteria';
import { isPasswordValid, PASSWORD_REQUIREMENTS_MESSAGE } from '../../../core/utils/password.validator';
import { apiErrorMessage } from '../../../core/utils/api-error';

/** Lets administrators rotate their own password (the job-seeker profile page is not available to admins). */
@Component({
  selector: 'app-account-security',
  standalone: true,
  imports: [CommonModule, FormsModule, PasswordCriteria],
  templateUrl: './account-security.html',
  styleUrls: ['./account-security.scss']
})
export class AccountSecurity {
  currentPassword = '';
  newPassword = '';
  confirmPassword = '';
  saving = false;
  error = '';
  success = '';

  constructor(
    private accountService: AccountService,
    public auth: AuthService
  ) {}

  changePassword() {
    this.error = '';
    this.success = '';

    if (!this.currentPassword) {
      this.error = 'Enter your current password.';
      return;
    }
    if (!isPasswordValid(this.newPassword)) {
      this.error = PASSWORD_REQUIREMENTS_MESSAGE;
      return;
    }
    if (this.newPassword === this.currentPassword) {
      this.error = 'The new password must be different from the current password.';
      return;
    }
    if (this.newPassword !== this.confirmPassword) {
      this.error = 'New passwords do not match.';
      return;
    }

    this.saving = true;
    this.accountService
      .changePassword({ currentPassword: this.currentPassword, newPassword: this.newPassword })
      .subscribe({
        next: () => {
          this.saving = false;
          this.currentPassword = '';
          this.newPassword = '';
          this.confirmPassword = '';
          this.success = 'Password changed. All other sessions have been signed out. Sign out and sign in again to confirm the new password works.';
        },
        error: err => {
          this.saving = false;
          this.error = apiErrorMessage(err, 'Could not change password.');
        }
      });
  }
}
