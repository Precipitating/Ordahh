namespace Ordahh.Api.Dtos;
public class CreateLoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiryTime { get; set; }
}