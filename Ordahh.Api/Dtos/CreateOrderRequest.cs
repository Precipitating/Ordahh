using System.ComponentModel.DataAnnotations;

namespace Ordahh.Api.Dtos;

public class CreateOrderRequest
{
    [Required, MinLength(1)]
    public List<CreateOrderItemRequest> Items { get; set; } = new();
}

public class CreateOrderItemRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "MenuItemId must be a positive integer.")]
    public int MenuItemId { get; set; }

    [Range(1, 10, ErrorMessage = "Quantity must be between 1 and 10.")]
    public int Quantity { get; set; }
}