namespace Ordahh.Api.Models;

public class MenuItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }

    // In stock and available for ordering
    public bool IsAvailable { get; set; } = true;

    // Not shown on menu, archived for future purposes
    public bool IsArchived { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }
}