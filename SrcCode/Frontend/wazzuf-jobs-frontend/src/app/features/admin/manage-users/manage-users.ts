import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LoadingSpinner } from '../../../shared/components/loading-spinner/loading-spinner';
import { UsersAdminService } from '../../../core/services/users-admin.service';
import { RolesAdminService } from '../../../core/services/roles-admin.service';
import { AdminUserRow, RoleSummary } from '../../../core/models/account.models';

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
  error = '';

  formFirst = '';
  formLast = '';
  formEmail = '';
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

  openEdit(u: AdminUserRow) {
    this.editing = u;
    this.formFirst = u.firstName ?? '';
    this.formLast = u.lastName ?? '';
    this.formEmail = u.email ?? '';
    this.selectedRoles = [...(u.roles ?? [])];
    this.showModal = true;
    this.error = '';
  }

  closeModal() {
    this.showModal = false;
    this.editing = null;
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
    if (!this.editing) return;
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
    this.usersApi
      .update(this.editing.id, {
        firstName: first,
        lastName: last,
        email,
        roles: this.selectedRoles
      })
      .subscribe({
        next: () => {
          this.closeModal();
          this.loadUsers();
        },
        error: err => {
          this.error = err.error?.detail || 'Update failed.';
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
