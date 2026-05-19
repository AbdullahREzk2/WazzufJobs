export interface OnboardingStatus {
  isProfileComplete: boolean;
  experienceYears: number;
  careerLevel: string;
  preferredJobTypes: string[];
  preferredWorkplaceTypes: string[];
  interestedCategoryIds: number[];
  interestedJobTitles: string[];
  minSalary: number | null;
  showSalary: boolean;
}

export interface OnboardingRequest {
  experienceYears: number;
  careerLevel: number;
  preferredJobTypes: number[];
  preferredWorkplaceTypes: number[];
  interestedCategoryIds: number[];
  interestedJobTitles: string[];
  minSalary: number | null;
  showSalary: boolean;
}
