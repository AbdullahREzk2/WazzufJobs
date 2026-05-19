// src/app/core/services/application.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import {
  MyApplication,
  ApplicationResponse,
  ApplicationDetail
} from '../models/application.models';
import { ApplicationStatus } from '../models/enums';
import { PaginatedResponse } from '../models/job.models';

@Injectable({ providedIn: 'root' })
export class ApplicationService {
  private api = `${environment.apiUrl}/applications`;

  constructor(private http: HttpClient) {}

  apply(jobId: number) {
    return this.http.post<void>(`${this.api}/job/${jobId}/apply`, {});
  }

  getMyApplications(page = 1, pageSize = 10) {
    return this.http.get<PaginatedResponse<MyApplication>>(
      `${this.api}/my-applications`,
      { params: { page, pageSize } }
    );
  }

  getByJob(jobId: number, page = 1, pageSize = 10) {
    return this.http.get<PaginatedResponse<ApplicationResponse>>(
      `${this.api}/job/${jobId}`,
      { params: { page, pageSize } }
    );
  }

  getDetail(id: number) {
    return this.http.get<ApplicationDetail>(`${this.api}/${id}`);
  }

  updateStatus(id: number, status: ApplicationStatus) {
    return this.http.put<void>(`${this.api}/${id}/status`, { status });
  }
}