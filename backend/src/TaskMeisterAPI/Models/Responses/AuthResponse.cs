namespace TaskMeisterAPI.Models.Responses;

public record AuthResponse(string Token, string Name, DateTime ExpiresAt);
