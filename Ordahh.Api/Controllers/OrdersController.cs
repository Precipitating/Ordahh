using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ordahh.Api.Data;
using Ordahh.Api.Dtos;
using Ordahh.Api.Models;

namespace Ordahh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly AppDBContext _db;

    public OrdersController(AppDBContext db)
    {
        _db = db;
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
        Items = o.Items.Select(oi => new OrderItemResponse
        {
            Name = oi.MenuItem?.Name ?? "Unknown",
            MenuItemId = oi.MenuItemId,
            Quantity = oi.Quantity,
            UnitPrice = oi.UnitPrice
        }).ToList()
    };


    [HttpPatch("{id}/status")]
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

        return Ok(ToResponse(order));
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

        // 4. Reject items that are sold out
        var unavailable = menuItems.Where(m => !m.IsAvailable).Select(m => m.Name).ToList();
        if (unavailable.Count > 0)
            return BadRequest($"Currently unavailable: {string.Join(", ", unavailable)}");

        // 5. Build the order from DATABASE prices
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

        // 6. Save (order and lines go together in one transaction)
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // 7. Build the response
        var names = menuItems.ToDictionary(m => m.Id, m => m.Name);
        var response = new OrderResponse
        {
            Id = order.Id,
            CreatedAt = order.CreatedAt,
            Status = order.Status,
            Total = order.Total,
            Items = order.Items.Select(oi => new OrderItemResponse
            {
                Name = names[oi.MenuItemId],
                MenuItemId = oi.MenuItemId,
                Quantity = oi.Quantity,
                UnitPrice = oi.UnitPrice
            }).ToList()
        };

        return Ok(response);
    }
    [HttpGet]
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