import { Injectable, signal } from '@angular/core';
import { AccountService } from './account.service';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class ProfilePhotoService {
  photoUrl = signal<string | null>(null);
  loading = signal(false);

  constructor(
    private accountService: AccountService,
    private auth: AuthService
  ) {}

  refresh() {
    if (!this.auth.isLoggedIn()) {
      this.photoUrl.set(null);
      return;
    }

    this.loading.set(true);
    this.accountService.getUserInfo().subscribe({
      next: profile => {
        this.photoUrl.set(profile.profilePhotoUrl || null);
        this.loading.set(false);
      },
      error: () => {
        this.photoUrl.set(null);
        this.loading.set(false);
      }
    });
  }

  setPhotoUrl(url: string | null) {
    this.photoUrl.set(url || null);
  }
}
