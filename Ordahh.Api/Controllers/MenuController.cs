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
                Items = c.Items.Select(i => new
                {
                    i.Id,
                    i.Name,
                    i.Description,
                    i.Price,
                    i.IsAvailable
                })
            })
            .ToListAsync();

        return Ok(menu);
    }
}