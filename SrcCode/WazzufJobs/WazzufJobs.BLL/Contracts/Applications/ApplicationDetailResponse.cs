namespace WazzufJobs.BLL.Contracts.Applications;
public record ApplicationDetailResponse(
    int Id,
    string ApplicantName,
    string ApplicantEmail,
    string CVUrl,
    string JobTitle,
    string Status,
    float? AIScore,
    string? AIFeedback,
    bool IsAIScored,
    DateTime AppliedAt
);