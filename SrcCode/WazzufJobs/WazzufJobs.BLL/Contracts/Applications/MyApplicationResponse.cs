namespace WazzufJobs.BLL.Contracts.Applications;
// Candidate-facing: AI assessment data (score/feedback) must never be added to this contract.
public record MyApplicationResponse(
    int Id,
    string JobTitle,
    string JobLocation,
    string CategoryName,
    string Status,
    DateTime AppliedAt
);
