// src/app/core/services/saved-jobs.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { SavedJob } from '../models/application.models';

@Injectable({ providedIn: 'root' })
export class SavedJobsService {
  private api = `${environment.apiUrl}/saved-jobs`;

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