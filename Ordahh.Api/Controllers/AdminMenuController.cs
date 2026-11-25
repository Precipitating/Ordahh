using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Ordahh.Api.Data;

namespace Ordahh.Api.Controllers;

[ApiController]
[Route("api/admin/menu")]
[Authorize(Roles = "Staff")]
public class AdminMenuController : ControllerBase
{
    private readonly AppDBContext _db;

    public AdminMenuController(AppDBContext db)
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
                Items = c.Items.Select(i => new
                {
                    i.Id,
                    i.Name,
                    i.Description,
                    i.Price,
                    i.IsAvailable,
                    i.IsArchived
                })
            })
            .ToListAsync();

        return Ok(menu);
    }
}