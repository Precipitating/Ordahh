using System.ComponentModel.DataAnnotations;

namespace Ordahh.Api.Dtos;

public class MenuItemStockRequest
{
    [Range(0, 100000, ErrorMessage = "Stock must be between 0 and 100000, or null to stop tracking.")]
    public int? Stock { get; set; }
}