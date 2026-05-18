// src/app/core/models/application.models.ts
export interface MyApplication {
  id: number;
  jobTitle: string;
  jobLocation: string;
  categoryName: string;
  status: string;
  aiScore: number | null;
  aiFeedback: string | null;
  isAIScored: boolean;
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

export interface CVResponse {
  id: number;
  url: string;
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