namespace WazzufJobs.BLL.Contracts.Categories;
public record CategoryRequest
(
    string Name,
    IFormFile? IconFile = null  
    );