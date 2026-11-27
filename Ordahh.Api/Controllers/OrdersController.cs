using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ordahh.Api.Data;
using Ordahh.Api.Dtos;
using Ordahh.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Ordahh.Api.Hubs;
using Microsoft.AspNetCore.Authorization;

namespace Ordahh.Api.Controllers;


[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly AppDBContext _db;
    private readonly IHubContext<OrdersHub> _hub;
    private readonly IHubContext<TrackingHub> _trackingHub;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        AppDBContext db,
        IHubContext<OrdersHub> hub,
        IHubContext<TrackingHub> trackingHub,
        ILogger<OrdersController> logger)
    {
        _db = db;
        _hub = hub;
        _trackingHub = trackingHub;
        _logger = logger;
    }

    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedMoves = new()
    {
        [OrderStatus.Received] = new[] { OrderStatus.Preparing, OrderStatus.Cancelled },
        [OrderStatus.Preparing] = new[] { OrderStatus.Completed },
        [OrderStatus.Completed] = Array.Empty<OrderStatus>(),
        [OrderStatus.Cancelled] = Array.Empty<OrderStatus>(),
    };


    private static OrderResponse ToResponse(Order o) => new OrderResponse
    {
        Id = o.Id,
        CreatedAt = DateTime.SpecifyKind(o.CreatedAt, DateTimeKind.Utc),
        Status = o.Status,
        Total = o.Total,
        TrackingCode = o.TrackingCode,
        Items = o.Items.Select(oi => new OrderItemResponse
        {
            Name = oi.MenuItem?.Name ?? "Unknown",
            MenuItemId = oi.MenuItemId,
            Quantity = oi.Quantity,
            UnitPrice = oi.UnitPrice
        }).ToList()
    };
    private static TrackingResponse ToTracking(Order o) => new TrackingResponse
    {
        Status = o.Status,
        CreatedAt = DateTime.SpecifyKind(o.CreatedAt, DateTimeKind.Utc),
        Total = o.Total,
        Items = o.Items.Select(oi => new OrderItemResponse
        {
            Name = oi.MenuItem?.Name ?? "Unknown",
            MenuItemId = oi.MenuItemId,
            Quantity = oi.Quantity,
            UnitPrice = oi.UnitPrice
        }).ToList()
    };

    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Staff")]
    public async Task<IActionResult> UpdateStatus(int id, UpdateOrderStatusRequest request)
    {
        if (!Enum.IsDefined(request.Status))
            return BadRequest("Unknown status.");

        var order = await _db.Orders
            .Include(o => o.Items)
            .ThenInclude(oi => oi.MenuItem)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound();

        if (!AllowedMoves[order.Status].Contains(request.Status))
            return Conflict($"Cannot move an order from {order.Status} to {request.Status}.");

        order.Status = request.Status;
        await _db.SaveChangesAsync();

        var response = ToResponse(order);
        try
        {
            // 1. staff: every staff board gets the full order
            await _hub.Clients.All.SendAsync("StatusUpdated", response);

            // 2. customer: only the tracking page(s) that joined this order's group
            await _trackingHub.Clients
                .Group(TrackingHub.GroupName(order.TrackingCode))
                .SendAsync("OrderStatusChanged", ToTracking(order));
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Push failed for order {order.Id}");
        }

        return Ok(ToResponse(order));
    }
    [HttpGet("track/{code:guid}")]
    public async Task<IActionResult> Track(Guid code)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .ThenInclude(oi => oi.MenuItem)
            .FirstOrDefaultAsync(o => o.TrackingCode == code);

        if (order == null)
            return NotFound();

        return Ok(ToTracking(order));
    }


    [HttpPost]
    public async Task<IActionResult> CreateOrder(CreateOrderRequest request)
    {
        // 1. Merge duplicate lines: one entry per menu item, quantities added
        var lines = request.Items
            .GroupBy(i => i.MenuItemId)
            .Select(g => new { MenuItemId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToList();

        // The DTO only capped each original line, so re-check the merged quantity
        if (lines.Any(l => l.Quantity > 10))
            return BadRequest("You can order at most 10 of any one item.");

        // 2. Load the requested menu items in ONE query
        var ids = lines.Select(l => l.MenuItemId).ToList();
        var menuItems = await _db.MenuItems
            .Where(mi => ids.Contains(mi.Id))
            .ToListAsync();

        // 3. Reject ids that don't exist
        var missing = ids.Except(menuItems.Select(m => m.Id)).ToList();
        if (missing.Count > 0)
            return BadRequest($"Menu items not found: {string.Join(", ", missing)}");

        // 3.1 Reject items that are archived
        var archived = menuItems
            .Where(m => m.IsArchived)
            .Select(m => m.Name)
            .ToList();

        if (archived.Count > 0)
            return BadRequest($"These items are no longer on the menu: {string.Join(", ", archived)}");

        // 3.2 Reject items that are unavailable
        var unavailable = menuItems.Where(m => !m.IsAvailable).Select(m => m.Name).ToList();
        if (unavailable.Count > 0)
            return BadRequest($"Currently unavailable: {string.Join(", ", unavailable)}");

        // 3.3 Reject lines that ask for more than is left in stock
        //     (a friendly message; the atomic update below is the real guard)
        var shortLines = lines
            .Select(l => new { Item = menuItems.First(m => m.Id == l.MenuItemId), l.Quantity })
            .Where(x => x.Item.Stock != null && x.Item.Stock < x.Quantity)
            .Select(x => x.Item.Stock == 0
                ? $"{x.Item.Name} is sold out"
                : $"{x.Item.Name} (only {x.Item.Stock} left)")
            .ToList();

        if (shortLines.Count > 0)
            return BadRequest($"Not enough stock: {string.Join("; ", shortLines)}");

        // 4. Build the order from DATABASE prices
        var order = new Order
        {
            CreatedAt = DateTime.UtcNow,
            Status = OrderStatus.Received
        };

        foreach (var line in lines)
        {
            var menuItem = menuItems.First(mi => mi.Id == line.MenuItemId);
            order.Items.Add(new OrderItem
            {
                MenuItemId = menuItem.Id,
                Quantity = line.Quantity,
                UnitPrice = menuItem.Price
            });
        }

        order.Total = order.Items.Sum(oi => oi.UnitPrice * oi.Quantity);

        // 5. Take the stock and save the order in ONE transaction.
        //    All reads happen above; the first statement inside the transaction is a write.
        await using var tx = await _db.Database.BeginTransactionAsync();

        foreach (var line in lines)
        {
            var item = menuItems.First(m => m.Id == line.MenuItemId);
            if (item.Stock == null) continue;   // untracked items never run out

            var changed = await _db.MenuItems
                .Where(m => m.Id == item.Id && m.Stock != null && m.Stock >= line.Quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.Stock, m => m.Stock - line.Quantity));

            if (changed == 0)
            {
                // someone else took the stock first; leaving without Commit rolls everything back
                return Conflict($"Stock for {item.Name} changed while you were ordering. Please check your cart and try again.");
            }
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        // 6. Build the response
        var names = menuItems.ToDictionary(m => m.Id, m => m.Name);
        var response = new OrderResponse
        {
            Id = order.Id,
            CreatedAt = order.CreatedAt,
            Status = order.Status,
            Total = order.Total,
            TrackingCode = order.TrackingCode,
            Items = order.Items.Select(oi => new OrderItemResponse
            {
                Name = names[oi.MenuItemId],
                MenuItemId = oi.MenuItemId,
                Quantity = oi.Quantity,
                UnitPrice = oi.UnitPrice
            }).ToList()
        };

        // 7. Tell staff, only AFTER the commit. The order is saved, so a failed push must not become a 500.
        try
        {
            await _hub.Clients.All.SendAsync("OrderCreated", response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Push failed for order {OrderId}", order.Id);
        }

        return Ok(response);
    }
    [HttpGet]
    [Authorize(Roles = "Staff")]
    public async Task<IActionResult> GetOrders()
    {
        var orders = await _db.Orders
            .Include(o => o.Items)
            .ThenInclude(oi => oi.MenuItem)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var mapping = orders.Select(ToResponse).ToList();

        return Ok(mapping);

    }
}