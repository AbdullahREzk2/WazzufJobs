// Mirrors WazzufJobs.DAL.Enums
export enum JobType {
  FullTime = 0,
  PartTime = 1,
  Freelance = 2,
  Internship = 3,
  Contract = 4
}

export enum WorkplaceType {
  OnSite = 0,
  Remote = 1,
  Hybrid = 2
}

export enum ApplicationStatus {
  Pending = 0,
  UnderReview = 1,
  Shortlisted = 2,
  Rejected = 3,
  Accepted = 4
}

export enum CareerLevel {
  Student = 0,
  EntryLevel = 1,
  MidLevel = 2,
  Senior = 3,
  Manager = 4,
  Director = 5,
  Executive = 6
}

export const JOB_TYPE_LABELS: Record<JobType, string> = {
  [JobType.FullTime]: 'Full Time',
  [JobType.PartTime]: 'Part Time',
  [JobType.Freelance]: 'Freelance',
  [JobType.Internship]: 'Internship',
  [JobType.Contract]: 'Contract'
};

export const WORKPLACE_TYPE_LABELS: Record<WorkplaceType, string> = {
  [WorkplaceType.OnSite]: 'On Site',
  [WorkplaceType.Remote]: 'Remote',
  [WorkplaceType.Hybrid]: 'Hybrid'
};

export const CAREER_LEVEL_LABELS: Record<CareerLevel, string> = {
  [CareerLevel.Student]: 'Student',
  [CareerLevel.EntryLevel]: 'Entry Level',
  [CareerLevel.MidLevel]: 'Mid Level',
  [CareerLevel.Senior]: 'Senior',
  [CareerLevel.Manager]: 'Manager',
  [CareerLevel.Director]: 'Director',
  [CareerLevel.Executive]: 'Executive'
};

export const APPLICATION_STATUS_LABELS: Record<ApplicationStatus, string> = {
  [ApplicationStatus.Pending]: 'Pending',
  [ApplicationStatus.UnderReview]: 'Under Review',
  [ApplicationStatus.Shortlisted]: 'Shortlisted',
  [ApplicationStatus.Rejected]: 'Rejected',
  [ApplicationStatus.Accepted]: 'Accepted'
};

export function statusClass(status: string): string {
  return status.replace(/\s+/g, '').toLowerCase();
}
