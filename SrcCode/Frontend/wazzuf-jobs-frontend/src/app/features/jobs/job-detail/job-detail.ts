import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Navbar } from '../../../shared/components/navbar/navbar';
import { Footer } from '../../../shared/components/footer/footer';
import { LoadingSpinner } from '../../../shared/components/loading-spinner/loading-spinner';
import { JobService } from '../../../core/services/job.service';
import { ApplicationService } from '../../../core/services/application.service';
import { SavedJobsService } from '../../../core/services/saved-jobs.service';
import { CvService } from '../../../core/services/cv.service';
import { Job } from '../../../core/models/job.models';
import { statusClass } from '../../../core/models/enums';

@Component({
  selector: 'app-job-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, Navbar, Footer, LoadingSpinner],
  templateUrl: './job-detail.html',
  styleUrls: ['./job-detail.scss']
})
export class JobDetail implements OnInit {
  job: Job | null = null;
  loading = true;
  applying = false;
  isSaved = false;
  hasCv = false;
  error = '';
  success = '';
  applied = false;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private jobService: JobService,
    private applicationService: ApplicationService,
    private savedService: SavedJobsService,
    private cvService: CvService
  ) {}

  ngOnInit() {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!id) {
      this.router.navigate(['/jobs']);
      return;
    }
    this.loadJob(id);
    this.checkCv();
    this.checkSaved(id);
  }

  loadJob(id: number) {
    this.jobService.getById(id).subscribe({
      next: job => {
        this.job = job;
        this.loading = false;
      },
      error: err => {
        this.error = err.error?.detail || 'Job not found.';
        this.loading = false;
      }
    });
  }

  checkCv() {
    this.cvService.getMy().subscribe({
      next: () => this.hasCv = true,
      error: () => this.hasCv = false
    });
  }

  checkSaved(jobId: number) {
    this.savedService.getAll().subscribe({
      next: saved => this.isSaved = saved.some(s => s.jobId === jobId),
      error: () => {}
    });
  }

  toggleSave() {
    if (!this.job) return;
    if (this.isSaved) {
      this.savedService.remove(this.job.id).subscribe({
        next: () => this.isSaved = false
      });
    } else {
      this.savedService.save(this.job.id).subscribe({
        next: () => this.isSaved = true
      });
    }
  }

  apply() {
    if (!this.job || this.applied) return;

    if (!this.hasCv) {
      this.error = 'Please upload your CV in Profile before applying.';
      return;
    }

    this.applying = true;
    this.error = '';
    this.success = '';

    this.applicationService.apply(this.job.id).subscribe({
      next: () => {
        this.applied = true;
        this.success = 'Application submitted! AI will score your CV shortly.';
        this.applying = false;
      },
      error: err => {
        this.error = err.error?.detail || 'Could not apply. You may have already applied.';
        this.applying = false;
      }
    });
  }

  formatSalary(min: number | null, max: number | null): string {
    if (min == null && max == null) return 'Negotiable';
    if (min != null && max != null) return `$${min} – $${max}`;
    return min != null ? `From $${min}` : `Up to $${max}`;
  }

  statusCss(status: string): string {
    return statusClass(status);
  }
}
