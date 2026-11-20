using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Ordahh.Api.Hubs;

[Authorize(Roles = "Staff")]
public class OrdersHub : Hub
{
}