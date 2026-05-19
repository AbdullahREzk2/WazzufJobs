import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { OnboardingRequest, OnboardingStatus } from '../models/onboarding.models';

@Injectable({ providedIn: 'root' })
export class OnboardingService {
  private api = `${environment.apiUrl}/onboarding`;

  constructor(private http: HttpClient) {}

  getStatus() {
    return this.http.get<OnboardingStatus>(`${this.api}/status`);
  }

  complete(request: OnboardingRequest) {
    return this.http.post<void>(`${this.api}/complete`, request);
  }
}
