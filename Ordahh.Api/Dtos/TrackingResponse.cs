using Ordahh.Api.Models;

namespace Ordahh.Api.Dtos;

public class TrackingResponse
{
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal Total { get; set; }
    public List<OrderItemResponse> Items { get; set; } = new();
}