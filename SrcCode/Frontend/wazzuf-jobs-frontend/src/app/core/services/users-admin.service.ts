import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AdminCreateUserPayload, AdminUpdateUserPayload, AdminUserRow } from '../models/account.models';

@Injectable({ providedIn: 'root' })
export class UsersAdminService {
  private api = `${environment.apiUrl}/users`;

  constructor(private http: HttpClient) {}

  getAll() {
    return this.http.get<AdminUserRow[]>(this.api);
  }

  getById(userId: string) {
    return this.http.get<AdminUserRow>(`${this.api}/${userId}`);
  }

  update(userId: string, body: AdminUpdateUserPayload) {
    return this.http.put<void>(`${this.api}/${userId}`, body);
  }

  create(body: AdminCreateUserPayload) {
    return this.http.post<void>(this.api, body);
  }

  toggleStatus(userId: string) {
    return this.http.put<void>(`${this.api}/${userId}/toggle-status`, {});
  }

  unlock(userId: string) {
    return this.http.put<void>(`${this.api}/${userId}/unlock`, {});
  }
}
