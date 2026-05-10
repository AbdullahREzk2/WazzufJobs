// WazzufJobs.BLL/Features/Categories/Commands/CreateCategory/CreateCategoryCommandHandler.cs
using MediatR;
using WazzufJobs.BLL.Abstractions;
using WazzufJobs.BLL.Contracts.Categories;
using WazzufJobs.BLL.Errors;
using WazzufJobs.BLL.Helpers;
using WazzufJobs.BLL.Services;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.IRepository;

namespace WazzufJobs.BLL.Features.Categories.Commands.CreateCategory;

public class CreateCategoryCommandHandler(
    ICategoryRepository categoryRepository,
    ICloudinaryService cloudinaryService)
    : IRequestHandler<CreateCategoryCommand, Result<CategoryResponse>>
{
    private readonly ICategoryRepository _categoryRepository = categoryRepository;
    private readonly ICloudinaryService _cloudinaryService = cloudinaryService;

    public async Task<Result<CategoryResponse>> Handle(
        CreateCategoryCommand request,
        CancellationToken cancellationToken)
    {
        if (await _categoryRepository.ExistsAsync(request.Request.Name, cancellationToken))
            return Result.Failure<CategoryResponse>(CategoryErrors.DuplicateName);

        var category = new JobCategory
        {
            Name = request.Request.Name,
            Slug = SlugHelper.Generate(request.Request.Name)
        };

        // upload icon if provided
        if (request.Request.IconFile is not null)
        {
            var uploadResult = await _cloudinaryService.UploadImageAsync(
                request.Request.IconFile,
                "wazzuf-jobs/categories",
                cancellationToken);

            if (!uploadResult.IsSuccess)
                return Result.Failure<CategoryResponse>(CategoryErrors.IconUploadFailed);

            category.IconUrl = uploadResult.Url;
            category.IconPublicId = uploadResult.PublicId;
        }

        await _categoryRepository.AddAsync(category, cancellationToken);
        await _categoryRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(new CategoryResponse(
            category.Id,
            category.Name,
            category.Slug,
            category.IconUrl));
    }
}