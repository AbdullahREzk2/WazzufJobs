import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LoadingSpinner } from '../../../shared/components/loading-spinner/loading-spinner';
import { JobService } from '../../../core/services/job.service';
import { Category, JobSummary, JobRequest } from '../../../core/models/job.models';
import { JobType, WorkplaceType, JOB_TYPE_LABELS, WORKPLACE_TYPE_LABELS } from '../../../core/models/enums';

@Component({
  selector: 'app-manage-jobs',
  standalone: true,
  imports: [CommonModule, FormsModule, LoadingSpinner],
  templateUrl: './manage-jobs.html',
  styleUrls: ['./manage-jobs.scss']
})
export class ManageJobs implements OnInit {
  jobs: JobSummary[] = [];
  categories: Category[] = [];
  loading = true;
  showModal = false;
  editingId: number | null = null;
  error = '';
  skillsInput = '';

  form: JobRequest = this.emptyForm();

  jobTypes = Object.entries(JOB_TYPE_LABELS).map(([k, v]) => ({ value: +k, label: v }));
  workplaceTypes = Object.entries(WORKPLACE_TYPE_LABELS).map(([k, v]) => ({ value: +k, label: v }));

  constructor(private jobService: JobService) {}

  ngOnInit() {
    this.jobService.getCategories().subscribe({
      next: cats => this.categories = cats
    });
    this.loadJobs();
  }

  emptyForm(): JobRequest {
    return {
      title: '',
      description: '',
      location: '',
      skills: [],
      jobType: JobType.FullTime,
      workplaceType: WorkplaceType.Hybrid,
      categoryId: 0,
      salaryMin: null,
      salaryMax: null,
      expiresAt: null
    };
  }

  loadJobs() {
    this.loading = true;
    this.jobService.getAll({ page: 1, pageSize: 50 }).subscribe({
      next: res => {
        this.jobs = res.items;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  openCreate() {
    this.editingId = null;
    this.form = this.emptyForm();
    if (this.categories.length) this.form.categoryId = this.categories[0].id;
    this.skillsInput = '';
    this.showModal = true;
    this.error = '';
  }

  openEdit(job: JobSummary) {
    this.editingId = job.id;
    this.jobService.getById(job.id).subscribe({
      next: full => {
        this.form = {
          title: full.title,
          description: full.description,
          location: full.location,
          skills: full.skills,
          jobType: this.parseJobType(full.jobType),
          workplaceType: this.parseWorkplace(full.workplaceType),
          categoryId: this.categories.find(c => c.name === full.categoryName)?.id ?? 0,
          salaryMin: full.salaryMin,
          salaryMax: full.salaryMax,
          expiresAt: full.expiresAt
        };
        this.skillsInput = full.skills.join(', ');
        this.showModal = true;
      }
    });
  }

  parseJobType(label: string): number {
    const entry = Object.entries(JOB_TYPE_LABELS).find(([, v]) => v === label);
    return entry ? +entry[0] : JobType.FullTime;
  }

  parseWorkplace(label: string): number {
    const entry = Object.entries(WORKPLACE_TYPE_LABELS).find(([, v]) => v === label);
    return entry ? +entry[0] : WorkplaceType.Hybrid;
  }

  save() {
    this.form.skills = this.skillsInput.split(',').map(s => s.trim()).filter(Boolean);
    if (!this.form.title || !this.form.description || !this.form.categoryId) {
      this.error = 'Fill required fields.';
      return;
    }

    const req = { ...this.form };
    const onSuccess = () => {
      this.showModal = false;
      this.loadJobs();
    };
    const onError = (err: { error?: { detail?: string } }) => {
      this.error = err.error?.detail || 'Save failed.';
    };

    if (this.editingId) {
      this.jobService.update(this.editingId, req).subscribe({
        next: onSuccess,
        error: onError
      });
    } else {
      this.jobService.create(req).subscribe({
        next: onSuccess,
        error: onError
      });
    }
  }

  toggleStatus(id: number) {
    this.jobService.toggleStatus(id).subscribe({
      next: () => this.loadJobs()
    });
  }

  deleteJob(id: number) {
    if (!confirm('Delete this job?')) return;
    this.jobService.delete(id).subscribe({
      next: () => this.loadJobs()
    });
  }

  closeModal() {
    this.showModal = false;
    this.error = '';
  }
}
