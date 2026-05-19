import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Navbar } from '../../shared/components/navbar/navbar';
import { Footer } from '../../shared/components/footer/footer';
import { LoadingSpinner } from '../../shared/components/loading-spinner/loading-spinner';
import { PasswordCriteria } from '../../shared/components/password-criteria/password-criteria';
import { AuthService } from '../../core/services/auth.service';
import { CvService } from '../../core/services/cv.service';
import { AccountService } from '../../core/services/account.service';
import { CVResponse } from '../../core/models/application.models';
import { UserProfile } from '../../core/models/account.models';
import {
  isPasswordValid,
  PASSWORD_REQUIREMENTS_MESSAGE
} from '../../core/utils/password.validator';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    Navbar,
    Footer,
    LoadingSpinner,
    PasswordCriteria
  ],
  templateUrl: './profile.html',
  styleUrls: ['./profile.scss']
})
export class Profile implements OnInit {
  cv: CVResponse | null = null;
  profile: UserProfile | null = null;

  loadingAccount = true;
  loadingCv = true;
  uploadingCv = false;
  uploadingPhoto = false;

  error = '';
  success = '';

  profileFirst = '';
  profileLast = '';

  pwdCurrent = '';
  pwdNew = '';
  pwdConfirm = '';

  constructor(
    private cvService: CvService,
    private accountService: AccountService,
    public auth: AuthService
  ) {}

  ngOnInit() {
    this.loadAccount();
    this.loadCv();
  }

  get pageLoading(): boolean {
    return this.loadingAccount || this.loadingCv;
  }

  loadAccount() {
    this.loadingAccount = true;
    this.accountService.getUserInfo().subscribe({
      next: p => {
        this.profile = p;
        this.profileFirst = p.firstName ?? '';
        this.profileLast = p.lastName ?? '';
        this.loadingAccount = false;
      },
      error: () => {
        this.profile = null;
        const u = this.auth.currentUser();
        this.profileFirst = u?.firstName ?? '';
        this.profileLast = u?.lastName ?? '';
        this.loadingAccount = false;
        this.error = 'Could not load account details from the server.';
      }
    });
  }

  loadCv() {
    this.loadingCv = true;
    this.cvService.getMy().subscribe({
      next: cv => {
        this.cv = cv;
        this.loadingCv = false;
      },
      error: () => {
        this.cv = null;
        this.loadingCv = false;
      }
    });
  }

  saveProfile() {
    this.clearMessages();
    const first = this.profileFirst.trim();
    const last = this.profileLast.trim();
    if (!first || !last) {
      this.error = 'First and last name are required.';
      return;
    }
    this.accountService.updateUserInfo({ firstName: first, lastName: last }).subscribe({
      next: () => {
        this.auth.patchStoredUser({ firstName: first, lastName: last });
        if (this.profile) {
          this.profile = { ...this.profile, firstName: first, lastName: last };
        }
        this.success = 'Profile updated.';
      },
      error: err => {
        this.error = err.error?.detail || 'Could not update profile.';
      }
    });
  }

  changePassword() {
    this.clearMessages();
    if (!this.pwdCurrent) {
      this.error = 'Enter your current password.';
      return;
    }
    if (!isPasswordValid(this.pwdNew)) {
      this.error = PASSWORD_REQUIREMENTS_MESSAGE;
      return;
    }
    if (this.pwdNew !== this.pwdConfirm) {
      this.error = 'New passwords do not match.';
      return;
    }
    this.accountService
      .changePassword({
        currentPassword: this.pwdCurrent,
        newPassword: this.pwdNew
      })
      .subscribe({
        next: () => {
          this.pwdCurrent = '';
          this.pwdNew = '';
          this.pwdConfirm = '';
          this.success = 'Password changed.';
        },
        error: err => {
          this.error = err.error?.detail || 'Could not change password.';
        }
      });
  }

  onPhotoSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    if (!file.type.startsWith('image/')) {
      this.error = 'Please choose an image file.';
      return;
    }
    this.clearMessages();
    this.uploadingPhoto = true;
    this.accountService.uploadProfileImage(file).subscribe({
      next: () => {
        this.uploadingPhoto = false;
        input.value = '';
        this.accountService.getUserInfo().subscribe({
          next: p => {
            this.profile = p;
            this.success = 'Profile photo updated.';
          },
          error: () => {
            this.success = 'Photo uploaded. Refresh the page to see it.';
          }
        });
      },
      error: err => {
        this.uploadingPhoto = false;
        this.error = err.error?.detail || 'Photo upload failed.';
      }
    });
  }

  onCvSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    if (!file.name.match(/\.(pdf|doc|docx)$/i)) {
      this.error = 'Please upload a PDF or Word document.';
      return;
    }

    this.uploadingCv = true;
    this.clearMessages();

    this.cvService.upload(file).subscribe({
      next: cv => {
        this.cv = cv;
        this.uploadingCv = false;
        this.success = 'CV uploaded successfully!';
        input.value = '';
      },
      error: err => {
        this.error = err.error?.detail || 'Upload failed.';
        this.uploadingCv = false;
      }
    });
  }

  deleteCv() {
    if (!confirm('Delete your CV?')) return;
    this.clearMessages();
    this.cvService.delete().subscribe({
      next: () => {
        this.cv = null;
        this.success = 'CV deleted.';
      },
      error: err => {
        this.error = err.error?.detail || 'Delete failed.';
      }
    });
  }

  private clearMessages() {
    this.error = '';
    this.success = '';
  }
}
