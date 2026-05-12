namespace WazzufJobs.BLL.Contracts.Jobs;
public record JobResponse(
    int Id,
    string Title,
    string Description,
    string Location,
    List<string> Skills,
    string JobType,
    string WorkplaceType,
    string CategoryName,
    string PostedBy,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string Status,
    DateTime CreatedAt,
    DateTime? ExpiresAt
);
