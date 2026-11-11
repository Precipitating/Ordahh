using Ordahh.Api.Models;
namespace Ordahh.Api.Dtos;

public class OrderResponse    
{
    public List<OrderItemResponse> Items { get; set; } = new();
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal Total { get; set; }

    public OrderStatus Status { get; set; }


}


public class OrderItemResponse
{
    public string Name { get; set; } = string.Empty;
    public int MenuItemId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
