using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ordahh.Api.Data;

namespace Ordahh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MenuController : ControllerBase
{
    private readonly AppDBContext _db;

    public MenuController(AppDBContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetMenu()
    {
        var menu = await _db.Categories
            .Select(c => new
            {
                c.Id,
                c.Name,
                Items = c.Items
                .Where(i => !i.IsArchived)
                .Select(i => new
                {
                    i.Id,
                    i.Name,
                    i.Description,
                    i.Price,
                    IsAvailable = i.IsAvailable && (i.Stock == null || i.Stock > 0),
                    MaxPerOrder = (i.Stock == null || i.Stock >= 10) ? 10 : i.Stock!.Value,
                    LowStock = i.Stock != null && i.Stock > 0 && i.Stock <= 5
                })

            })
            .Where(c => c.Items.Any())
            .ToListAsync();

        return Ok(menu);
    }
}