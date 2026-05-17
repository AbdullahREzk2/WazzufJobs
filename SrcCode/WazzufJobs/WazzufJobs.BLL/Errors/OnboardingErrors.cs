namespace WazzufJobs.BLL.Errors;

public static class OnboardingErrors
{
    public static readonly Error AlreadyCompleted = new(
        "Onboarding.AlreadyCompleted",
        "Profile is already completed.",
        StatusCodes.Status409Conflict);

    public static readonly Error InvalidCategory = new(
        "Onboarding.InvalidCategory",
        "One or more selected categories are invalid.",
        StatusCodes.Status400BadRequest);
}