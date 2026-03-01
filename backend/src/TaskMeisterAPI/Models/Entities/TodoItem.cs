using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskMeisterAPI.Models.Entities;

[Table("TodoItems")]
public class TodoItem
{
    [Key]
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public TodoStatus Status { get; set; } = TodoStatus.NotStarted;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
