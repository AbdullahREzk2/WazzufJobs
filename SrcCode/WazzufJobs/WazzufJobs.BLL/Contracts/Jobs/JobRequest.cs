using WazzufJobs.DAL.Enums;

namespace WazzufJobs.BLL.Contracts.Jobs;
public record JobRequest(
    string Title,
    string Description,
    string Location,
    List<string> Skills,
    JobType JobType,
    WorkplaceType WorkplaceType,
    int CategoryId,
    decimal? SalaryMin,
    decimal? SalaryMax,
    DateTime? ExpiresAt
);
