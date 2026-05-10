using WazzufJobs.BLL.Contracts.Categories;

namespace WazzufJobs.BLL.Features.Categories.Queries.GetAllCategories;
public class GetAllCategoriesQueryHandler(ICategoryRepository categoryRepository): IRequestHandler<GetAllCategoriesQuery, IEnumerable<CategoryResponse>>
{
    private readonly ICategoryRepository _categoryRepository = categoryRepository;

    public async Task<IEnumerable<CategoryResponse>> Handle(GetAllCategoriesQuery request,CancellationToken cancellationToken)
    {
        var categories = await _categoryRepository.GetAllAsync(cancellationToken);

        return categories.Select(c => new CategoryResponse(
            c.Id,
            c.Name,
            c.Slug,
            c.IconUrl));
    }
}
