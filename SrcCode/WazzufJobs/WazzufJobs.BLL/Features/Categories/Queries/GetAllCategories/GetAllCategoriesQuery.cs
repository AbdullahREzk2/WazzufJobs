using WazzufJobs.BLL.Contracts.Categories;

namespace WazzufJobs.BLL.Features.Categories.Queries.GetAllCategories;
public record GetAllCategoriesQuery : IRequest<IEnumerable<CategoryResponse>>;

