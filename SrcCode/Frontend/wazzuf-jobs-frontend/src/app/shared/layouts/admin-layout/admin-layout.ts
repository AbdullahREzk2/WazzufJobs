import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './admin-layout.html',
  styleUrls: ['./admin-layout.scss']
})
export class AdminLayout {
  nav = [
    { path: '/admin', label: 'Overview', icon: '📊', exact: true },
    { path: '/admin/jobs', label: 'Jobs', icon: '💼', exact: false },
    { path: '/admin/categories', label: 'Categories', icon: '📂', exact: false },
    { path: '/admin/applications', label: 'Applications', icon: '📋', exact: false },
    { path: '/admin/users', label: 'Users', icon: '👥', exact: false },
    { path: '/admin/roles', label: 'Roles', icon: '🔐', exact: false }
  ];

  constructor(
    public auth: AuthService,
    private router: Router
  ) {}

  logout() {
    this.auth.logout();
  }

  goHome() {
    this.router.navigate(['/admin']);
  }
}
