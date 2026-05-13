namespace WazzufJobs.BLL.Errors;
public static class ApplicationErrors
{
    public static readonly Error NotFound = new(
        "Application.NotFound",
        "Application not found.",
        StatusCodes.Status404NotFound);

    public static readonly Error AlreadyApplied = new(
        "Application.AlreadyApplied",
        "You have already applied for this job.",
        StatusCodes.Status409Conflict);

    public static readonly Error JobNotActive = new(
        "Application.JobNotActive",
        "This job is no longer accepting applications.",
        StatusCodes.Status400BadRequest);

    public static readonly Error CVNotFound = new(
        "Application.CVNotFound",
        "Please upload your CV before applying.",
        StatusCodes.Status400BadRequest);
}
