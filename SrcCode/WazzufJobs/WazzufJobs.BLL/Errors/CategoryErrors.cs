
namespace WazzufJobs.BLL.Errors;

public static class CategoryErrors
{
    public static readonly Error NotFound = new(
        "Category.NotFound",
        "Category not found.",
        StatusCodes.Status404NotFound);

    public static readonly Error DuplicateName = new(
        "Category.DuplicateName",
        "A category with this name already exists.",
        StatusCodes.Status409Conflict);

    public static readonly Error IconUploadFailed = new(
        "Category.IconUploadFailed",
        "Failed to upload category icon.",
        StatusCodes.Status500InternalServerError);
}