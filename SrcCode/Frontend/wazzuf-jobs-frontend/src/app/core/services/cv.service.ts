// src/app/core/services/cv.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { CVResponse } from '../models/application.models';

@Injectable({ providedIn: 'root' })
export class CvService {
  private api = `${environment.apiUrl}/cv`;

  constructor(private http: HttpClient) {}

  getMy() {
    return this.http.get<CVResponse>(this.api);
  }

  upload(file: File) {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<CVResponse>(this.api, formData);
  }

  delete() {
    return this.http.delete<void>(this.api);
  }
}