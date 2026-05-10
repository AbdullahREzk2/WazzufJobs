namespace WazzufJobs.BLL.Contracts.Categories;
public record CategoryResponse
(
     int Id,
    string Name,
    string Slug,
    string? IconUrl
    );
