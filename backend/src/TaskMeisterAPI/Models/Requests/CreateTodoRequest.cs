using System.ComponentModel.DataAnnotations;

namespace TaskMeisterAPI.Models.Requests;

public record CreateTodoRequest(
    [Required, MinLength(1), MaxLength(500)] string Title
);
