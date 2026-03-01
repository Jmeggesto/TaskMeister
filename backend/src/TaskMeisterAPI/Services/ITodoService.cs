using TaskMeisterAPI.Models.Entities;

namespace TaskMeisterAPI.Services;

public interface ITodoService
{
    Task<IReadOnlyList<TodoItem>> GetAllForUserAsync(User user);
    Task<TodoItem?> GetByIdForUserAsync(int id, User user);
    Task<TodoItem> CreateForUserAsync(string title, User user);
    Task<TodoItem?> UpdateForUserAsync(int id, string title, TodoStatus status, User user);
    Task<bool> DeleteForUserAsync(int id, User user);
}
