namespace WazzufJobs.BLL.Errors;
public static class CVErrors
{
    public static readonly Error NotFound = new(
        "CV.NotFound",
        "No CV found for this user.",
        StatusCodes.Status404NotFound);

    public static readonly Error UploadFailed = new(
        "CV.UploadFailed",
        "Failed to upload CV. Please try again.",
        StatusCodes.Status500InternalServerError);

    public static readonly Error InvalidFileType = new(
        "CV.InvalidFileType",
        "Only PDF files are allowed.",
        StatusCodes.Status400BadRequest);

    public static readonly Error FileTooLarge = new(
        "CV.FileTooLarge",
        "CV file size cannot exceed 5MB.",
        StatusCodes.Status400BadRequest);

    public static readonly Error ScannedPDF = new(
    "CV.ScannedPDF",
    "Your CV appears to be a scanned image. Please upload a text-based PDF exported from Word or Google Docs.",
    StatusCodes.Status400BadRequest);
}