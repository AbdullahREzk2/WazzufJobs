import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { Navbar } from '../../shared/components/navbar/navbar';
import { Footer } from '../../shared/components/footer/footer';
import { LoadingSpinner } from '../../shared/components/loading-spinner/loading-spinner';
import { SavedJobsService } from '../../core/services/saved-jobs.service';
import { SavedJob } from '../../core/models/application.models';

@Component({
  selector: 'app-saved-jobs',
  standalone: true,
  imports: [CommonModule, Navbar, Footer, LoadingSpinner],
  templateUrl: './saved-jobs.html',
  styleUrls: ['./saved-jobs.scss']
})
export class SavedJobs implements OnInit {
  jobs: SavedJob[] = [];
  loading = true;

  constructor(
    private savedService: SavedJobsService,
    private router: Router
  ) {}

  ngOnInit() {
    this.savedService.getAll().subscribe({
      next: jobs => {
        this.jobs = jobs;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  openJob(jobId: number) {
    this.router.navigate(['/jobs', jobId]);
  }

  remove(event: Event, jobId: number) {
    event.stopPropagation();
    this.savedService.remove(jobId).subscribe({
      next: () => this.jobs = this.jobs.filter(j => j.jobId !== jobId)
    });
  }

  formatSalary(min: number | null, max: number | null): string {
    if (min == null && max == null) return 'Negotiable';
    if (min != null && max != null) return `$${min} – $${max}`;
    return min != null ? `From $${min}` : `Up to $${max}`;
  }
}
