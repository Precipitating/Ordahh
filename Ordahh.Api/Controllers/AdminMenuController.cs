using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ordahh.Api.Data;
using Ordahh.Api.Dtos;
using Ordahh.Api.Models;

namespace Ordahh.Api.Controllers;

[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/admin")]
public class AdminMenuController : ControllerBase
{
    private readonly AppDBContext _db;

    public AdminMenuController(AppDBContext db)
    {
        _db = db;
    }
    private static AdminMenuItemResponse ToResponse(MenuItem m) => new AdminMenuItemResponse
    {
        Id = m.Id,
        Name = m.Name,
        Description = m.Description,
        Price = m.Price,
        CategoryId = m.CategoryId,
        CategoryName = m.Category?.Name ?? "",
        IsAvailable = m.IsAvailable,
        IsArchived = m.IsArchived
    };
    [HttpPost("menu-items")]
    public async Task<IActionResult> CreateMenuItem(MenuItemRequest request)
    {
        var name = request.Name.Trim();

        var category = await _db.Categories.FindAsync(request.CategoryId);
        if (category == null)
            return BadRequest("That category does not exist.");

        var duplicate = await _db.MenuItems.AnyAsync(m =>
            m.CategoryId == request.CategoryId &&
            !m.IsArchived &&
            m.Name.ToLower() == name.ToLower());

        if (duplicate)
            return Conflict($"'{name}' already exists in that category.");

        var item = new MenuItem
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Price = request.Price,
            CategoryId = request.CategoryId,
            IsAvailable = true,
            IsArchived = false
        };

        _db.MenuItems.Add(item);
        await _db.SaveChangesAsync();

        item.Category = category;
        return Ok(ToResponse(item));
    }

    [HttpGet("menu")]
    public async Task<IActionResult> GetMenu()
    {
        var items = await _db.MenuItems
            .Include(m => m.Category)
            .OrderBy(m => m.Category!.Name)
            .ThenBy(m => m.Name)
            .Select(m => new AdminMenuItemResponse
            {
                Id = m.Id,
                Name = m.Name,
                Description = m.Description,
                Price = m.Price,
                CategoryId = m.CategoryId,
                CategoryName = m.Category!.Name,
                IsAvailable = m.IsAvailable,
                IsArchived = m.IsArchived
            })
            .ToListAsync();

        return Ok(items);
    }
    [HttpPatch("menu-items/{id}/archive")]
    public async Task<IActionResult> ArchiveItem(int id, MenuItemArchiveRequest request)
    {
        var item = await _db.MenuItems
             .Include(m => m.Category)
             .FirstOrDefaultAsync(m => m.Id == id); 

        if (item == null)
        {
            return NotFound();
        }

        item.IsArchived = request.IsArchived;
        await _db.SaveChangesAsync();

        return Ok(ToResponse(item));
    }
    [HttpPatch("menu-items/{id}/availability")]
    public async Task<IActionResult> UpdateAvailability(int id, MenuItemAvailabilityRequest request)
    {
        var item = await _db.MenuItems
             .Include(m => m.Category)
             .FirstOrDefaultAsync(m => m.Id == id); 

        if (item == null)
        {
            return NotFound();
        }

        item.IsAvailable = request.IsAvailable;
        await _db.SaveChangesAsync();

        return Ok(ToResponse(item));
    }
}