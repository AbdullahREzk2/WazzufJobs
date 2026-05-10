namespace WazzufJobs.BLL.Features.Categories.Commands.DeleteCategory;

public class DeleteCategoryCommandHandler(ICategoryRepository categoryRepository): IRequestHandler<DeleteCategoryCommand, Result>
{
    private readonly ICategoryRepository _categoryRepository = categoryRepository;

    public async Task<Result> Handle(DeleteCategoryCommand request,CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken);

        if (category is null)
            return Result.Failure(CategoryErrors.NotFound);

        await _categoryRepository.DeleteAsync(category);
        await _categoryRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
