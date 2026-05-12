namespace WazzufJobs.BLL.Contracts.Jobs;
public record JobSummaryResponse(
    int Id,
    string Title,
    string Location,
    string JobType,
    string WorkplaceType,
    string CategoryName,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string Status,
    DateTime CreatedAt
);
