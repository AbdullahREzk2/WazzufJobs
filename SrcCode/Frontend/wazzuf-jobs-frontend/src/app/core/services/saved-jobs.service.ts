// src/app/core/services/saved-jobs.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { SavedJob } from '../models/application.models';

@Injectable({ providedIn: 'root' })
export class SavedJobsService {
  /** ASP.NET route is `api/SavedJobs` — kebab-case `saved-jobs` does not match. */
  private api = `${environment.apiUrl}/SavedJobs`;

  constructor(private http: HttpClient) {}

  getAll() {
    return this.http.get<SavedJob[]>(this.api);
  }

  save(jobId: number) {
    return this.http.post<void>(`${this.api}/${jobId}`, {});
  }

  remove(jobId: number) {
    return this.http.delete<void>(`${this.api}/${jobId}`);
  }
}