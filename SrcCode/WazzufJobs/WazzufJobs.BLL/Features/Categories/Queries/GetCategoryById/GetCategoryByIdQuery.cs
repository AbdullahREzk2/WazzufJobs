using WazzufJobs.BLL.Contracts.Categories;

namespace WazzufJobs.BLL.Features.Categories.Queries.GetCategoryById;
public record GetCategoryByIdQuery(int Id) : IRequest<Result<CategoryResponse>>;

