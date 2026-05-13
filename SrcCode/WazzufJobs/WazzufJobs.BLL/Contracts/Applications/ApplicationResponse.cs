namespace WazzufJobs.BLL.Contracts.Applications;
public record ApplicationResponse(
    int Id,
    string ApplicantName,
    string ApplicantEmail,
    string Status,
    float? AIScore,
    string? AIFeedback,
    bool IsAIScored,
    DateTime AppliedAt
);
