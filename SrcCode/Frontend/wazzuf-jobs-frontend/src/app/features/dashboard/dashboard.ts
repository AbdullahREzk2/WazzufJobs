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
import {
  ApplicationStatus,
  APPLICATION_STATUS_LABELS,
  statusClass
} from '../../core/models/enums';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, Navbar, Footer, LoadingSpinner, AmbientBg],
  templateUrl: './dashboard.html',
  styleUrls: ['./dashboard.scss']
})
export class Dashboard implements OnInit {
  applications: MyApplication[] = [];
  filteredApplications: MyApplication[] = [];
  selectedStatus: ApplicationStatus | null = null;
  statusFilters = Object.entries(APPLICATION_STATUS_LABELS).map(
    ([key, label]) => ({ value: +key as ApplicationStatus, label })
  );
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
        this.filteredApplications = res.items;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  statusCss(status: string): string {
    return statusClass(status);
  }

  setStatusFilter(status: ApplicationStatus | null) {
    this.selectedStatus = status;
    if (status === null) {
      this.filteredApplications = [...this.applications];
      return;
    }
    this.filteredApplications = this.applications.filter(app => app.status === APPLICATION_STATUS_LABELS[status]);
  }

  inProgressCount(): number {
    const closed = ['Rejected', 'Accepted'];
    return this.applications.filter(a => !closed.includes(a.status)).length;
  }
}
