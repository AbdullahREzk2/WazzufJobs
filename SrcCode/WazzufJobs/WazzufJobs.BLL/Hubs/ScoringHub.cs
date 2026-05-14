using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace WazzufJobs.BLL.Hubs;

[Authorize]
public class ScoringHub : Hub
{
    // clients connect to this hub
    // server pushes "ApplicationScored" event to the user
}