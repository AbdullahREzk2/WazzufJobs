import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LoadingSpinner } from '../../../shared/components/loading-spinner/loading-spinner';
import { JobService } from '../../../core/services/job.service';
import { ApplicationService } from '../../../core/services/application.service';
import { JobSummary } from '../../../core/models/job.models';
import {
  ApplicationResponse,
  ApplicationDetail
} from '../../../core/models/application.models';
import {
  ApplicationStatus,
  APPLICATION_STATUS_LABELS,
  statusClass
} from '../../../core/models/enums';

@Component({
  selector: 'app-view-applications',
  standalone: true,
  imports: [CommonModule, FormsModule, LoadingSpinner],
  templateUrl: './view-applications.html',
  styleUrls: ['./view-applications.scss']
})
export class ViewApplications implements OnInit {
  jobs: JobSummary[] = [];
  selectedJobId: number | null = null;
  applications: ApplicationResponse[] = [];
  detail: ApplicationDetail | null = null;
  loadingJobs = true;
  loadingApps = false;
  statusOptions = Object.entries(APPLICATION_STATUS_LABELS).map(([k, v]) => ({
    value: +k, label: v
  }));

  constructor(
    private jobService: JobService,
    private applicationService: ApplicationService
  ) {}

  ngOnInit() {
    this.jobService.getAll({ page: 1, pageSize: 100 }).subscribe({
      next: res => {
        this.jobs = res.items;
        this.loadingJobs = false;
        if (res.items.length) {
          this.selectJob(res.items[0].id);
        }
      },
      error: () => this.loadingJobs = false
    });
  }

  selectJob(jobId: number) {
    this.selectedJobId = jobId;
    this.detail = null;
    this.loadingApps = true;
    this.applicationService.getByJob(jobId, 1, 50).subscribe({
      next: res => {
        this.applications = res.items;
        this.loadingApps = false;
      },
      error: () => this.loadingApps = false
    });
  }

  viewDetail(id: number) {
    this.applicationService.getDetail(id).subscribe({
      next: d => this.detail = d
    });
  }

  updateStatus(app: ApplicationResponse, status: ApplicationStatus) {
    this.applicationService.updateStatus(app.id, status).subscribe({
      next: () => {
        if (this.selectedJobId) this.selectJob(this.selectedJobId);
        if (this.detail?.id === app.id) this.viewDetail(app.id);
      }
    });
  }

  statusCss(status: string): string {
    return statusClass(status);
  }

  parseStatus(status: string): ApplicationStatus {
    const entry = Object.entries(APPLICATION_STATUS_LABELS)
      .find(([, v]) => v === status);
    return entry ? +entry[0] as ApplicationStatus : ApplicationStatus.Pending;
  }
}
