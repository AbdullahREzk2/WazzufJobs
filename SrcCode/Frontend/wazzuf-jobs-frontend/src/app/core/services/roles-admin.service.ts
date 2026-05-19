import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { RoleDetail, RoleSummary, RoleUpsertPayload } from '../models/account.models';

@Injectable({ providedIn: 'root' })
export class RolesAdminService {
  private api = `${environment.apiUrl}/roles`;

  constructor(private http: HttpClient) {}

  getAll() {
    return this.http.get<RoleSummary[]>(this.api);
  }

  getDetail(id: string) {
    return this.http.get<RoleDetail>(`${this.api}/${id}`);
  }

  create(body: RoleUpsertPayload) {
    return this.http.post<RoleDetail>(this.api, body);
  }

  update(roleId: string, body: RoleUpsertPayload) {
    return this.http.put<void>(`${this.api}/${roleId}`, body);
  }

  toggleStatus(roleId: string) {
    return this.http.put<void>(`${this.api}/${roleId}/toggle-status`, {});
  }
}
