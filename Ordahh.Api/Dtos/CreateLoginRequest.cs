using System.ComponentModel.DataAnnotations;

namespace Ordahh.Api.Dtos;

public class CreateLoginRequest
{
    [Required, MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Password { get; set; } = string.Empty;
}