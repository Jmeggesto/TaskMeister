using TaskMeisterAPI.Models.Entities;

namespace TaskMeisterAPI.Services;

public interface ITodoService
{
    Task<IReadOnlyList<TodoItem>> GetAllAsync();
    Task<TodoItem?> GetByIdAsync(int id);
    Task<TodoItem> CreateAsync(string title);
    Task<TodoItem?> UpdateAsync(int id, string title, TodoStatus status);
    Task<bool> DeleteAsync(int id);
}
