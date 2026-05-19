import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LoadingSpinner } from '../../../shared/components/loading-spinner/loading-spinner';
import { RolesAdminService } from '../../../core/services/roles-admin.service';
import { RoleSummary } from '../../../core/models/account.models';
import { ALL_PERMISSION_KEYS } from '../../../core/constants/permission-catalog';

@Component({
  selector: 'app-manage-roles',
  standalone: true,
  imports: [CommonModule, FormsModule, LoadingSpinner],
  templateUrl: './manage-roles.html',
  styleUrls: ['./manage-roles.scss']
})
export class ManageRoles implements OnInit {
  roles: RoleSummary[] = [];
  loading = true;
  showModal = false;
  editingId: string | null = null;
  modalLoading = false;
  error = '';

  formName = '';
  selectedPerms = new Set<string>();
  readonly permCatalog = ALL_PERMISSION_KEYS;

  constructor(private rolesApi: RolesAdminService) {}

  ngOnInit() {
    this.loadRoles();
  }

  loadRoles() {
    this.loading = true;
    this.rolesApi.getAll().subscribe({
      next: r => {
        this.roles = r;
        this.loading = false;
      },
      error: () => (this.loading = false)
    });
  }

  openCreate() {
    this.editingId = null;
    this.modalLoading = false;
    this.formName = '';
    this.selectedPerms = new Set();
    this.showModal = true;
    this.error = '';
  }

  openEdit(r: RoleSummary) {
    this.editingId = r.id;
    this.formName = r.name;
    this.selectedPerms = new Set();
    this.showModal = true;
    this.modalLoading = true;
    this.error = '';
    this.rolesApi.getDetail(r.id).subscribe({
      next: d => {
        this.formName = d.name;
        this.selectedPerms = new Set(d.permissions ?? []);
        this.modalLoading = false;
      },
      error: err => {
        this.modalLoading = false;
        this.error = err.error?.detail || 'Could not load role.';
      }
    });
  }

  closeModal() {
    this.showModal = false;
    this.editingId = null;
    this.modalLoading = false;
    this.error = '';
  }

  togglePerm(id: string) {
    if (this.selectedPerms.has(id)) this.selectedPerms.delete(id);
    else this.selectedPerms.add(id);
  }

  permChecked(id: string): boolean {
    return this.selectedPerms.has(id);
  }

  save() {
    if (this.modalLoading) return;
    const name = this.formName.trim();
    if (!name) {
      this.error = 'Role name is required.';
      return;
    }
    const permissions = [...this.selectedPerms];
    if (!permissions.length) {
      this.error = 'Select at least one permission.';
      return;
    }
    const body = { name, permissions };
    const onErr = (err: { error?: { detail?: string } }) => {
      this.error = err.error?.detail || 'Save failed.';
    };
    if (this.editingId) {
      this.rolesApi.update(this.editingId, body).subscribe({
        next: () => {
          this.closeModal();
          this.loadRoles();
        },
        error: onErr
      });
    } else {
      this.rolesApi.create(body).subscribe({
        next: () => {
          this.closeModal();
          this.loadRoles();
        },
        error: onErr
      });
    }
  }

  toggleStatus(r: RoleSummary) {
    const msg = r.isDeleted ? 'Restore this role?' : 'Disable this role?';
    if (!confirm(msg)) return;
    this.rolesApi.toggleStatus(r.id).subscribe({
      next: () => this.loadRoles()
    });
  }
}
