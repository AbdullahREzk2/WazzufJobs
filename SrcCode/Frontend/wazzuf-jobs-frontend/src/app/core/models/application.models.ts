// src/app/core/models/application.models.ts
// Candidate-facing: the API never returns AI assessment data for the applicant.
export interface MyApplication {
  id: number;
  jobTitle: string;
  jobLocation: string;
  categoryName: string;
  status: string;
  appliedAt: string;
}

export interface ApplicationResponse {
  id: number;
  applicantName: string;
  applicantEmail: string;
  status: string;
  aiScore: number | null;
  aiFeedback: string | null;
  isAIScored: boolean;
  appliedAt: string;
}

export interface ApplicationDetail {
  id: number;
  applicantUserId: string;
  applicantName: string;
  applicantEmail: string;
  applicantProfilePhotoUrl?: string;
  hasCv: boolean;
  jobTitle: string;
  status: string;
  aiScore: number | null;
  aiFeedback: string | null;
  isAIScored: boolean;
  appliedAt: string;
  desiredSalary?: number | null;
  showSalary?: boolean;
}

// CV files are downloaded through the API only; no storage URL is exposed.
export interface CVResponse {
  id: number;
  fileName: string;
  uploadedAt: string;
}

export interface SavedJob {
  jobId: number;
  title: string;
  location: string;
  jobType: string;
  workplaceType: string;
  categoryName: string;
  salaryMin: number | null;
  salaryMax: number | null;
  status: string;
  savedAt: string;
}