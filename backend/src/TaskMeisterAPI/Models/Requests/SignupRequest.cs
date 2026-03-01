using System.ComponentModel.DataAnnotations;

namespace TaskMeisterAPI.Models.Requests;

public record SignupRequest(
    [Required, MinLength(2), MaxLength(50)] string Name,
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MinLength(8), MaxLength(100)] string Password
);
