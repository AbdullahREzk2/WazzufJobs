namespace WazzufJobs.BLL.Errors;
public static class JobErrors
{
    public static readonly Error NotFound = new(
        "Job.NotFound",
        "Job not found.",
        StatusCodes.Status404NotFound);

    public static readonly Error CategoryNotFound = new(
        "Job.CategoryNotFound",
        "The specified category does not exist.",
        StatusCodes.Status404NotFound);

    public static readonly Error DuplicateTitle = new(
        "Job.DuplicateTitle",
        "A job with this title already exists.",
        StatusCodes.Status409Conflict);

    // fail closed: a job is never created without a verified owner (no default or guessed company)
    public static readonly Error PlatformCompanyMissing = new(
        "Job.PlatformCompanyMissing",
        "Jobs cannot be created right now.",
        StatusCodes.Status503ServiceUnavailable);
}
