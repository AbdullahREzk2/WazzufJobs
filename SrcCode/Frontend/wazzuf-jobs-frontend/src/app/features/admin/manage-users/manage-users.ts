import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LoadingSpinner } from '../../../shared/components/loading-spinner/loading-spinner';
import { UsersAdminService } from '../../../core/services/users-admin.service';
import { RolesAdminService } from '../../../core/services/roles-admin.service';
import { AdminCreateUserPayload, AdminUserRow, RoleSummary } from '../../../core/models/account.models';

@Component({
  selector: 'app-manage-users',
  standalone: true,
  imports: [CommonModule, FormsModule, LoadingSpinner],
  templateUrl: './manage-users.html',
  styleUrls: ['./manage-users.scss']
})
export class ManageUsers implements OnInit {
  users: AdminUserRow[] = [];
  roleOptions: RoleSummary[] = [];
  loading = true;
  showModal = false;
  editing: AdminUserRow | null = null;
  viewingUser: AdminUserRow | null = null;
  viewLoading = false;
  error = '';

  searchTerm = '';
  formFirst = '';
  formLast = '';
  formEmail = '';
  formPassword = '';
  selectedRoles: string[] = [];

  constructor(
    private usersApi: UsersAdminService,
    private rolesApi: RolesAdminService
  ) {}

  ngOnInit() {
    this.rolesApi.getAll().subscribe({
      next: r => (this.roleOptions = r.filter(x => !x.isDeleted)),
      error: () => (this.roleOptions = [])
    });
    this.loadUsers();
  }

  get filteredUsers(): AdminUserRow[] {
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return this.users;
    return this.users.filter(u =>
      `${u.firstName} ${u.lastName}`.toLowerCase().includes(term) ||
      u.email.toLowerCase().includes(term)
    );
  }

  loadUsers() {
    this.loading = true;
    this.usersApi.getAll().subscribe({
      next: rows => {
        this.users = rows;
        this.loading = false;
      },
      error: () => (this.loading = false)
    });
  }

  openCreate() {
    this.editing = null;
    this.viewingUser = null;
    this.formFirst = '';
    this.formLast = '';
    this.formEmail = '';
    this.formPassword = '';
    this.selectedRoles = [];
    this.showModal = true;
    this.error = '';
  }

  openEdit(u: AdminUserRow) {
    this.editing = u;
    this.viewingUser = null;
    this.formFirst = u.firstName ?? '';
    this.formLast = u.lastName ?? '';
    this.formEmail = u.email ?? '';
    this.formPassword = '';
    this.selectedRoles = [...(u.roles ?? [])];
    this.showModal = true;
    this.error = '';
  }

  openView(u: AdminUserRow) {
    this.viewLoading = true;
    this.showModal = true;
    this.editing = null;
    this.viewingUser = null;
    this.usersApi.getById(u.id).subscribe({
      next: detail => {
        this.viewingUser = detail;
        this.viewLoading = false;
      },
      error: () => {
        this.viewLoading = false;
        this.error = 'Unable to load user details.';
      }
    });
  }

  closeModal() {
    this.showModal = false;
    this.editing = null;
    this.viewingUser = null;
    this.error = '';
  }

  toggleRole(name: string) {
    const i = this.selectedRoles.indexOf(name);
    if (i >= 0) this.selectedRoles.splice(i, 1);
    else this.selectedRoles.push(name);
  }

  roleChecked(name: string): boolean {
    return this.selectedRoles.includes(name);
  }

  save() {
    const first = this.formFirst.trim();
    const last = this.formLast.trim();
    const email = this.formEmail.trim();
    if (!first || !last || !email) {
      this.error = 'First name, last name, and email are required.';
      return;
    }
    if (!this.selectedRoles.length) {
      this.error = 'Select at least one role.';
      return;
    }

    const payload: AdminCreateUserPayload = {
      firstName: first,
      lastName: last,
      email,
      roles: this.selectedRoles
    };

    if (!this.editing && this.formPassword.trim()) {
      payload.password = this.formPassword.trim();
    }

    const request = this.editing
      ? this.usersApi.update(this.editing.id, payload)
      : this.usersApi.create(payload);

    request.subscribe({
      next: () => {
        this.closeModal();
        this.loadUsers();
      },
      error: err => {
        this.error = err.error?.detail || 'Save failed.';
      }
    });
  }

  toggleStatus(u: AdminUserRow) {
    const msg = u.isDisabled ? 'Enable this user?' : 'Disable this user?';
    if (!confirm(msg)) return;
    this.usersApi.toggleStatus(u.id).subscribe({
      next: () => this.loadUsers()
    });
  }

  unlock(u: AdminUserRow) {
    if (!confirm(`Unlock sign-in for ${u.email}?`)) return;
    this.usersApi.unlock(u.id).subscribe({
      next: () => this.loadUsers()
    });
  }
}
