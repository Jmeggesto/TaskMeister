using System.ComponentModel.DataAnnotations;

namespace TaskMeisterAPI.Models.Requests;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password
);
