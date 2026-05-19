import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './admin-dashboard.html',
  styleUrls: ['./admin-dashboard.scss']
})
export class AdminDashboard {
  links = [
    { icon: '💼', title: 'Manage Jobs', desc: 'Create, edit and toggle job postings', route: '/admin/jobs' },
    { icon: '📂', title: 'Categories', desc: 'Manage job categories and icons', route: '/admin/categories' },
    { icon: '📋', title: 'Applications', desc: 'Review applicants and AI scores', route: '/admin/applications' },
    { icon: '👥', title: 'Users', desc: 'Accounts, roles, lock and unlock sign-in', route: '/admin/users' },
    { icon: '🔐', title: 'Roles', desc: 'Define roles and permission sets', route: '/admin/roles' }
  ];
}
