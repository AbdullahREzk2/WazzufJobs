namespace WazzufJobs.BLL.Features.Categories.Commands.DeleteCategory;

public class DeleteCategoryCommandHandler(ICategoryRepository categoryRepository, IAuditLog audit) : IRequestHandler<DeleteCategoryCommand, Result>
{
    private readonly ICategoryRepository _categoryRepository = categoryRepository;

    // the change and its audit entry are committed together (Phase 02, S5)
    public async Task<Result> Handle(DeleteCategoryCommand request, CancellationToken cancellationToken) =>
        await audit.RecordAsync(() => ApplyAsync(request, cancellationToken), r => r.IsSuccess ? AuditEvent.Success(AuditActions.CategoryDeleted).On(AuditTargets.Category, request.Id.ToString()) : null, cancellationToken);

    private async Task<Result> ApplyAsync(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken);

        if (category is null)
            return Result.Failure(CategoryErrors.NotFound);

        await _categoryRepository.DeleteAsync(category);
        await _categoryRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
