import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import {
  UserProfile,
  UpdateProfilePayload,
  ChangePasswordPayload
} from '../models/account.models';

@Injectable({ providedIn: 'root' })
export class AccountService {
  private api = `${environment.apiUrl}/account`;

  constructor(private http: HttpClient) {}

  getUserInfo() {
    return this.http.get<UserProfile>(`${this.api}/userInfo`);
  }

  updateUserInfo(body: UpdateProfilePayload) {
    return this.http.put<void>(`${this.api}/update-User-Info`, body);
  }

  changePassword(body: ChangePasswordPayload) {
    return this.http.put<void>(`${this.api}/change-password`, body);
  }

  uploadProfileImage(file: File) {
    const form = new FormData();
    form.append('image', file);
    return this.http.post<void>(`${this.api}/profile-image`, form);
  }
}
