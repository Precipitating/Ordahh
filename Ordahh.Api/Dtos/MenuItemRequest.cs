using System.ComponentModel.DataAnnotations;

namespace Ordahh.Api.Dtos;

public class MenuItemRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Range(typeof(decimal), "0.01", "1000", ErrorMessage = "Price must be between 0.01 and 1000.")]
    public decimal Price { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Choose a category.")]
    public int CategoryId { get; set; }

    [Range(0, 100000, ErrorMessage = "Stock must be between 0 and 100000, or left blank.")]
    public int? Stock { get; set; }
}