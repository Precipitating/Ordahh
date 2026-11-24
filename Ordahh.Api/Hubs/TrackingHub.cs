using Microsoft.AspNetCore.SignalR;

namespace Ordahh.Api.Hubs;

// open to customers: no [Authorize]
public class TrackingHub : Hub
{
    public async Task JoinOrder(Guid code)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(code));
    }

    public static string GroupName(Guid code) => $"order-{code}";
}