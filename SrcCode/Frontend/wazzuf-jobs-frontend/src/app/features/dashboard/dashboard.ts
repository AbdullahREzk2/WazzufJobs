import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { Navbar } from '../../shared/components/navbar/navbar';
import { Footer } from '../../shared/components/footer/footer';
import { LoadingSpinner } from '../../shared/components/loading-spinner/loading-spinner';
import { AmbientBg } from '../../shared/components/ambient-bg/ambient-bg';
import { ApplicationService } from '../../core/services/application.service';
import { OnboardingService } from '../../core/services/onboarding.service';
import { AuthService } from '../../core/services/auth.service';
import { MyApplication } from '../../core/models/application.models';
import { statusClass } from '../../core/models/enums';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, Navbar, Footer, LoadingSpinner, AmbientBg],
  templateUrl: './dashboard.html',
  styleUrls: ['./dashboard.scss']
})
export class Dashboard implements OnInit {
  applications: MyApplication[] = [];
  loading = true;
  needsOnboarding = false;
  userName = '';

  constructor(
    private applicationService: ApplicationService,
    private onboardingService: OnboardingService,
    private auth: AuthService,
    private router: Router
  ) {}

  ngOnInit() {
    const user = this.auth.currentUser();
    this.userName = user?.firstName || 'there';

    this.onboardingService.getStatus().subscribe({
      next: status => {
        this.needsOnboarding = !status.isProfileComplete;
      },
      error: () => {}
    });

    this.applicationService.getMyApplications(1, 8).subscribe({
      next: res => {
        this.applications = res.items;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  statusCss(status: string): string {
    return statusClass(status);
  }

  avgScore(): string {
    const scored = this.applications.filter(a => a.isAIScored && a.aiScore != null);
    if (!scored.length) return '—';
    const avg = scored.reduce((s, a) => s + (a.aiScore ?? 0), 0) / scored.length;
    return `${Math.round(avg)}%`;
  }
}
