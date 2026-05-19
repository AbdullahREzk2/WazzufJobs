import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Category } from '../models/job.models';

@Injectable({ providedIn: 'root' })
export class CategoryService {
  private api = `${environment.apiUrl}/categories`;

  constructor(private http: HttpClient) {}

  getAll() {
    return this.http.get<Category[]>(this.api);
  }

  create(name: string, iconFile?: File) {
    const form = new FormData();
    form.append('name', name);
    if (iconFile) form.append('iconFile', iconFile);
    return this.http.post<Category>(this.api, form);
  }

  update(id: number, name: string, iconFile?: File) {
    const form = new FormData();
    form.append('name', name);
    if (iconFile) form.append('iconFile', iconFile);
    return this.http.put<void>(`${this.api}/${id}`, form);
  }

  delete(id: number) {
    return this.http.delete<void>(`${this.api}/${id}`);
  }
}
