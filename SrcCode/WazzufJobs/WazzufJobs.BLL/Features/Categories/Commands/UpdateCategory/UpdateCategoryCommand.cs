using WazzufJobs.BLL.Contracts.Categories;

namespace WazzufJobs.BLL.Features.Categories.Commands.UpdateCategory;
public record UpdateCategoryCommand(int Id, CategoryRequest Request) : IRequest<Result>;

