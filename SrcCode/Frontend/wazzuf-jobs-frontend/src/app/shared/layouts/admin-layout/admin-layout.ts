import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { ProfilePhotoService } from '../../../core/services/profile-photo.service';
import { UserAvatar } from '../../components/user-avatar/user-avatar';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, UserAvatar],
  templateUrl: './admin-layout.html',
  styleUrls: ['./admin-layout.scss']
})
export class AdminLayout implements OnInit {
  nav = [
    { path: '/admin', label: 'Overview', icon: '📊', exact: true },
    { path: '/admin/jobs', label: 'Jobs', icon: '💼', exact: false },
    { path: '/admin/categories', label: 'Categories', icon: '📂', exact: false },
    { path: '/admin/applications', label: 'Applications', icon: '📋', exact: false },
    { path: '/admin/users', label: 'Users', icon: '👥', exact: false },
    { path: '/admin/roles', label: 'Roles', icon: '🔐', exact: false },
    { path: '/admin/account-security', label: 'Account security', icon: '🛡️', exact: false }
  ];

  constructor(
    public auth: AuthService,
    private profilePhoto: ProfilePhotoService,
    private router: Router
  ) {}

  ngOnInit() {
    if (this.auth.isLoggedIn()) {
      this.profilePhoto.refresh();
    }
  }

  logout() {
    this.auth.logout();
  }

  goHome() {
    this.router.navigate(['/admin']);
  }
}
