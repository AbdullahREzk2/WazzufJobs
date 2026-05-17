namespace WazzufJobs.BLL.Contracts.Onboarding;

public record OnboardingStatusResponse(
    bool IsProfileComplete,
    int ExperienceYears,
    string CareerLevel,
    List<string> PreferredJobTypes,
    List<string> PreferredWorkplaceTypes,
    List<int> InterestedCategoryIds,
    List<string> InterestedJobTitles,
    decimal? MinSalary,
    bool ShowSalary
);