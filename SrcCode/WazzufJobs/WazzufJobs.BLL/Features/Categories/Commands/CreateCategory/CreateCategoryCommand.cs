using WazzufJobs.BLL.Contracts.Categories;

namespace WazzufJobs.BLL.Features.Categories.Commands.CreateCategory;
public record CreateCategoryCommand(CategoryRequest Request) : IRequest<Result<CategoryResponse>>;
