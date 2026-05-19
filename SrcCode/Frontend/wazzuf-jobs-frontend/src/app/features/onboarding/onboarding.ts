import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Navbar } from '../../shared/components/navbar/navbar';
import { Footer } from '../../shared/components/footer/footer';
import { LoadingSpinner } from '../../shared/components/loading-spinner/loading-spinner';
import { OnboardingService } from '../../core/services/onboarding.service';
import { JobService } from '../../core/services/job.service';
import { Category } from '../../core/models/job.models';
import {
  JobType, WorkplaceType, CareerLevel,
  JOB_TYPE_LABELS, WORKPLACE_TYPE_LABELS, CAREER_LEVEL_LABELS
} from '../../core/models/enums';

@Component({
  selector: 'app-onboarding',
  standalone: true,
  imports: [CommonModule, FormsModule, Navbar, Footer, LoadingSpinner],
  templateUrl: './onboarding.html',
  styleUrls: ['./onboarding.scss']
})
export class Onboarding implements OnInit {
  step = 1;
  loading = true;
  saving = false;
  error = '';
  categories: Category[] = [];

  experienceYears = 0;
  careerLevel = CareerLevel.EntryLevel;
  preferredJobTypes: number[] = [];
  preferredWorkplaceTypes: number[] = [];
  interestedCategoryIds: number[] = [];
  jobTitleInput = '';
  interestedJobTitles: string[] = [];
  minSalary: number | null = null;
  showSalary = true;

  jobTypes = Object.entries(JOB_TYPE_LABELS).map(([k, v]) => ({ value: +k, label: v }));
  workplaceTypes = Object.entries(WORKPLACE_TYPE_LABELS).map(([k, v]) => ({ value: +k, label: v }));
  careerLevels = Object.entries(CAREER_LEVEL_LABELS).map(([k, v]) => ({ value: +k, label: v }));

  constructor(
    private onboardingService: OnboardingService,
    private jobService: JobService,
    private router: Router
  ) {}

  ngOnInit() {
    this.jobService.getCategories().subscribe({
      next: cats => this.categories = cats
    });

    this.onboardingService.getStatus().subscribe({
      next: status => {
        if (status.isProfileComplete) {
          this.experienceYears = status.experienceYears;
          this.interestedCategoryIds = [...status.interestedCategoryIds];
          this.interestedJobTitles = [...status.interestedJobTitles];
          this.minSalary = status.minSalary;
          this.showSalary = status.showSalary;
        }
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  toggleJobType(value: number) {
    const i = this.preferredJobTypes.indexOf(value);
    if (i >= 0) this.preferredJobTypes.splice(i, 1);
    else this.preferredJobTypes.push(value);
  }

  toggleWorkplace(value: number) {
    const i = this.preferredWorkplaceTypes.indexOf(value);
    if (i >= 0) this.preferredWorkplaceTypes.splice(i, 1);
    else this.preferredWorkplaceTypes.push(value);
  }

  toggleCategory(id: number) {
    const i = this.interestedCategoryIds.indexOf(id);
    if (i >= 0) this.interestedCategoryIds.splice(i, 1);
    else this.interestedCategoryIds.push(id);
  }

  addJobTitle() {
    const t = this.jobTitleInput.trim();
    if (t && !this.interestedJobTitles.includes(t)) {
      this.interestedJobTitles.push(t);
      this.jobTitleInput = '';
    }
  }

  removeTitle(title: string) {
    this.interestedJobTitles = this.interestedJobTitles.filter(t => t !== title);
  }

  next() { if (this.step < 3) this.step++; }
  back() { if (this.step > 1) this.step--; }

  submit() {
    if (!this.preferredJobTypes.length || !this.preferredWorkplaceTypes.length) {
      this.error = 'Select at least one job type and workplace preference.';
      return;
    }

    this.saving = true;
    this.error = '';

    this.onboardingService.complete({
      experienceYears: this.experienceYears,
      careerLevel: this.careerLevel,
      preferredJobTypes: this.preferredJobTypes,
      preferredWorkplaceTypes: this.preferredWorkplaceTypes,
      interestedCategoryIds: this.interestedCategoryIds,
      interestedJobTitles: this.interestedJobTitles,
      minSalary: this.minSalary,
      showSalary: this.showSalary
    }).subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: err => {
        this.error = err.error?.detail || 'Could not save profile.';
        this.saving = false;
      }
    });
  }

  isSelected(arr: number[], val: number): boolean {
    return arr.includes(val);
  }
}
