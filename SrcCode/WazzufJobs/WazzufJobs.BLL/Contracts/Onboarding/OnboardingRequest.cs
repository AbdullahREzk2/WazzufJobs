using WazzufJobs.DAL.Enums;

namespace WazzufJobs.BLL.Contracts.Onboarding;
public record OnboardingRequest(
    int ExperienceYears,
    CareerLevel CareerLevel,
    List<JobType> PreferredJobTypes,
    List<WorkplaceType> PreferredWorkplaceTypes,
    List<int> InterestedCategoryIds,
    List<string> InterestedJobTitles,
    decimal? MinSalary,
    bool ShowSalary
);