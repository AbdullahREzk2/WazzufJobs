namespace WazzufJobs.BLL.Contracts.SavedJobs;

public record SavedJobResponse(
    int JobId,
    string Title,
    string Location,
    string JobType,
    string WorkplaceType,
    string CategoryName,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string Status,
    DateTime SavedAt
);