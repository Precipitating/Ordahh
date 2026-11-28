using Microsoft.AspNetCore.SignalR;
using Ordahh.Api.Hubs;

namespace Ordahh.Api.Services;

public class MenuNotifier
{
    private readonly IHubContext<MenuHub> _hub;
    private readonly ILogger<MenuNotifier> _logger;

    public MenuNotifier(IHubContext<MenuHub> hub, ILogger<MenuNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    // the data change is already saved, so a failed push must never fail the request
    public async Task NotifyAsync()
    {
        try
        {
            await _hub.Clients.All.SendAsync("MenuChanged");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Menu push failed");
        }
    }
}