namespace Ordahh.Api.Models;

public class Order
{
    public int Id { get; set; }
    public List<OrderItem> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public OrderStatus Status { get; set; } = OrderStatus.Received;
    public decimal Total { get; set; }

}
public enum OrderStatus
{
    Received,
    Preparing,
    Ready,
    Completed,
    Cancelled
}