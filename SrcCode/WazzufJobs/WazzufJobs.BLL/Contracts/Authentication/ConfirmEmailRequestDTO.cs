namespace WazzufJobs.BLL.Contracts.Authentication;
public record ConfirmEmailRequestDTO(

    string UserId,
    string Code
 );
