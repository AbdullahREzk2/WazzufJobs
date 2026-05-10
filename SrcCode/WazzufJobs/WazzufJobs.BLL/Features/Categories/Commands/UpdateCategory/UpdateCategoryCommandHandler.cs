// WazzufJobs.BLL/Features/Categories/Commands/UpdateCategory/UpdateCategoryCommandHandler.cs
using MediatR;
using WazzufJobs.BLL.Abstractions;
using WazzufJobs.BLL.Errors;
using WazzufJobs.BLL.Helpers;
using WazzufJobs.BLL.Services;
using WazzufJobs.DAL.IRepository;

namespace WazzufJobs.BLL.Features.Categories.Commands.UpdateCategory;

public class UpdateCategoryCommandHandler(
    ICategoryRepository categoryRepository,
    ICloudinaryService cloudinaryService)
    : IRequestHandler<UpdateCategoryCommand, Result>
{
    private readonly ICategoryRepository _categoryRepository = categoryRepository;
    private readonly ICloudinaryService _cloudinaryService = cloudinaryService;

    public async Task<Result> Handle(
        UpdateCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken);

        if (category is null)
            return Result.Failure(CategoryErrors.NotFound);

        var existing = await _categoryRepository.GetBySlugAsync(
            SlugHelper.Generate(request.Request.Name), cancellationToken);

        if (existing is not null && existing.Id != request.Id)
            return Result.Failure(CategoryErrors.DuplicateName);

        category.Name = request.Request.Name;
        category.Slug = SlugHelper.Generate(request.Request.Name);

        // upload new icon if provided
        if (request.Request.IconFile is not null)
        {
            // delete old icon from Cloudinary first
            if (!string.IsNullOrEmpty(category.IconPublicId))
                await _cloudinaryService.DeleteAsync(category.IconPublicId, cancellationToken);

            var uploadResult = await _cloudinaryService.UploadImageAsync(
                request.Request.IconFile,
                "wazzuf-jobs/categories",
                cancellationToken);

            if (!uploadResult.IsSuccess)
                return Result.Failure(CategoryErrors.IconUploadFailed);

            category.IconUrl = uploadResult.Url;
            category.IconPublicId = uploadResult.PublicId;
        }

        await _categoryRepository.UpdateAsync(category);
        await _categoryRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}