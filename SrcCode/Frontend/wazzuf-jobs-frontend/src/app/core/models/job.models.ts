// src/app/core/models/job.models.ts
export interface Job {
  id: number;
  title: string;
  description: string;
  location: string;
  skills: string[];
  jobType: string;
  workplaceType: string;
  categoryName: string;
  postedBy: string;
  salaryMin: number | null;
  salaryMax: number | null;
  status: string;
  createdAt: string;
  expiresAt: string | null;
}

export interface JobSummary {
  id: number;
  title: string;
  location: string;
  jobType: string;
  workplaceType: string;
  categoryName: string;
  salaryMin: number | null;
  salaryMax: number | null;
  status: string;
  createdAt: string;
}

export interface JobFilter {
  keyword?: string;
  location?: string;
  categoryId?: number;
  jobType?: number;
  workplaceType?: number;
  page: number;
  pageSize: number;
}

export interface JobRequest {
  title: string;
  description: string;
  location: string;
  skills: string[];
  jobType: number;
  workplaceType: number;
  categoryId: number;
  salaryMin: number | null;
  salaryMax: number | null;
  expiresAt: string | null;
}

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPrevPage: boolean;
}

export interface Category {
  id: number;
  name: string;
  slug: string;
  iconUrl: string | null;
}