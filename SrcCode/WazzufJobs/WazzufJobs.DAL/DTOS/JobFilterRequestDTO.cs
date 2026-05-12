using WazzufJobs.DAL.Enums;

namespace WazzufJobs.DAL.DTOS;
public record JobFilterRequestDTO(
    string? Keyword = null,
    string? Location = null,
    int? CategoryId = null,
    JobType? JobType = null,
    WorkplaceType? WorkplaceType = null,
    int Page = 1,
    int PageSize = 10
);
