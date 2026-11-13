using Ordahh.Api.Models;

namespace Ordahh.Api.Dtos;

public class UpdateOrderStatusRequest
{
    public OrderStatus Status { get; set; }
}