import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Navbar } from '../../../shared/components/navbar/navbar';
import { Footer } from '../../../shared/components/footer/footer';
import { LoadingSpinner } from '../../../shared/components/loading-spinner/loading-spinner';
import { AmbientBg } from '../../../shared/components/ambient-bg/ambient-bg';
import { JobService } from '../../../core/services/job.service';
import { SavedJobsService } from '../../../core/services/saved-jobs.service';
import { Category, JobSummary } from '../../../core/models/job.models';
import { JobType, WorkplaceType, JOB_TYPE_LABELS, WORKPLACE_TYPE_LABELS } from '../../../core/models/enums';

@Component({
  selector: 'app-job-list',
  standalone: true,
  imports: [CommonModule, FormsModule, Navbar, Footer, LoadingSpinner, AmbientBg],
  templateUrl: './job-list.html',
  styleUrls: ['./job-list.scss']
})
export class JobList implements OnInit {
  jobs: JobSummary[] = [];
  categories: Category[] = [];
  savedIds = new Set<number>();
  loading = true;
  error = '';

  keyword = '';
  location = '';
  categoryId: number | null = null;
  jobType: number | null = null;
  workplaceType: number | null = null;
  page = 1;
  pageSize = 9;
  totalPages = 1;

  jobTypes = Object.entries(JOB_TYPE_LABELS).map(([k, v]) => ({
    value: +k, label: v
  }));
  workplaceTypes = Object.entries(WORKPLACE_TYPE_LABELS).map(([k, v]) => ({
    value: +k, label: v
  }));

  constructor(
    private jobService: JobService,
    private savedService: SavedJobsService,
    private router: Router
  ) {}

  ngOnInit() {
    this.loadCategories();
    this.loadSavedIds();
    this.search();
  }

  loadCategories() {
    this.jobService.getCategories().subscribe({
      next: cats => this.categories = cats,
      error: () => {}
    });
  }

  loadSavedIds() {
    this.savedService.getAll().subscribe({
      next: saved => this.savedIds = new Set(saved.map(s => s.jobId)),
      error: () => {}
    });
  }

  search(page = 1) {
    this.page = page;
    this.loading = true;
    this.error = '';

    this.jobService.getAll({
      keyword: this.keyword || undefined,
      location: this.location || undefined,
      categoryId: this.categoryId ?? undefined,
      jobType: this.jobType ?? undefined,
      workplaceType: this.workplaceType ?? undefined,
      page: this.page,
      pageSize: this.pageSize
    }).subscribe({
      next: res => {
        this.jobs = res.items;
        this.totalPages = res.totalPages;
        this.loading = false;
      },
      error: err => {
        this.error = err.error?.detail || 'Failed to load jobs.';
        this.loading = false;
      }
    });
  }

  openJob(id: number) {
    this.router.navigate(['/jobs', id]);
  }

  toggleSave(event: Event, jobId: number) {
    event.stopPropagation();
    if (this.savedIds.has(jobId)) {
      this.savedService.remove(jobId).subscribe({
        next: () => this.savedIds.delete(jobId),
        error: () => {}
      });
    } else {
      this.savedService.save(jobId).subscribe({
        next: () => this.savedIds.add(jobId),
        error: () => {}
      });
    }
  }

  formatSalary(min: number | null, max: number | null): string {
    if (min == null && max == null) return 'Salary negotiable';
    if (min != null && max != null) return `$${min} – $${max}`;
    return min != null ? `From $${min}` : `Up to $${max}`;
  }

  prevPage() {
    if (this.page > 1) this.search(this.page - 1);
  }

  nextPage() {
    if (this.page < this.totalPages) this.search(this.page + 1);
  }
}
