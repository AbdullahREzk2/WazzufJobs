namespace WazzufJobs.BLL.Mapping;
public class MappingConfigurations : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<RegisterRequestDTO, AppUser>()
            .Map(dest => dest.UserName, src => src.Email);

        config.NewConfig<(AppUser user, IList<string> roles), UserResponse>()
        .Map(dest => dest.Roles, src => src.roles)
        .Map(dest => dest, src => src.user)
          .ConstructUsing(src => new UserResponse(
          src.user.Id,
          src.user.FirstName,
          src.user.LastName,
          src.user.Email!,
          src.user.IsDisabled,
          src.roles
         ));
    }
}
