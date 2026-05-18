// src/app/core/services/job.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import {
  JobSummary, Job, JobFilter,
  JobRequest, Category, PaginatedResponse
} from '../models/job.models';

@Injectable({ providedIn: 'root' })
export class JobService {
  private api = `${environment.apiUrl}/jobs`;

  constructor(private http: HttpClient) {}

  getAll(filter: JobFilter) {
    let params = new HttpParams()
      .set('page',     filter.page.toString())
      .set('pageSize', filter.pageSize.toString());

    if (filter.keyword)      params = params.set('keyword',      filter.keyword);
    if (filter.location)     params = params.set('location',     filter.location);
    if (filter.categoryId)   params = params.set('categoryId',   filter.categoryId.toString());
    if (filter.jobType != null) params = params.set('jobType',   filter.jobType.toString());
    if (filter.workplaceType != null) params = params.set('workplaceType', filter.workplaceType.toString());

    return this.http.get<PaginatedResponse<JobSummary>>(this.api, { params });
  }

  getById(id: number) {
    return this.http.get<Job>(`${this.api}/${id}`);
  }

  create(request: JobRequest) {
    return this.http.post<JobSummary>(this.api, request);
  }

  update(id: number, request: JobRequest) {
    return this.http.put<void>(`${this.api}/${id}`, request);
  }

  delete(id: number) {
    return this.http.delete<void>(`${this.api}/${id}`);
  }

  toggleStatus(id: number) {
    return this.http.put<void>(`${this.api}/${id}/toggle-status`, {});
  }

  getCategories() {
    return this.http.get<Category[]>(`${environment.apiUrl}/categories`);
  }
}