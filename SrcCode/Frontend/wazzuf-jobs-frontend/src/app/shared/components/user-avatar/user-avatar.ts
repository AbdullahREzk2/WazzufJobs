import { Component, computed, inject, input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../../core/services/auth.service';
import { ProfilePhotoService } from '../../../core/services/profile-photo.service';

@Component({
  selector: 'app-user-avatar',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './user-avatar.html',
  styleUrls: ['./user-avatar.scss']
})
export class UserAvatar implements OnInit {
  size = input<'sm' | 'md' | 'lg'>('md');
  photoUrlOverride = input<string | null | undefined>(undefined);
  initialsOverride = input<string | undefined>(undefined);

  private auth = inject(AuthService);
  private profilePhoto = inject(ProfilePhotoService);

  initials = computed(() => {
    const override = this.initialsOverride();
    if (override) return override;
    const u = this.auth.currentUser();
    if (!u) return '?';
    return `${u.firstName?.charAt(0) ?? ''}${u.lastName?.charAt(0) ?? ''}`.toUpperCase() || '?';
  });

  photoUrl = computed(() => {
    const override = this.photoUrlOverride();
    if (override !== undefined) return override || null;
    return this.profilePhoto.photoUrl();
  });

  ngOnInit() {
    if (this.photoUrlOverride() === undefined && this.auth.isLoggedIn()) {
      this.profilePhoto.refresh();
    }
  }
}
