using System.ComponentModel.DataAnnotations;
using TaskMeisterAPI.Models.Entities;

namespace TaskMeisterAPI.Models.Requests;

public record UpdateTodoRequest(
    [Required, MinLength(1), MaxLength(500)] string Title,
    [Required] TodoStatus Status
);
