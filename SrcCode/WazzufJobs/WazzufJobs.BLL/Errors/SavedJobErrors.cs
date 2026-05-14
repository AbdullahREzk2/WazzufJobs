namespace WazzufJobs.BLL.Errors;

public static class SavedJobErrors
{
    public static readonly Error NotFound = new(
        "SavedJob.NotFound",
        "This job is not in your saved list.",
        StatusCodes.Status404NotFound);

    public static readonly Error AlreadySaved = new(
        "SavedJob.AlreadySaved",
        "You have already saved this job.",
        StatusCodes.Status409Conflict);

    public static readonly Error JobNotFound = new(
        "SavedJob.JobNotFound",
        "Job not found.",
        StatusCodes.Status404NotFound);
}