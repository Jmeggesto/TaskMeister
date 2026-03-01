using Microsoft.EntityFrameworkCore;
using TaskMeisterAPI.Data;
using TaskMeisterAPI.Models.Entities;

namespace TaskMeisterAPI.Services;

public class TodoService : ITodoService
{
    private readonly AppDbContext _db;

    public TodoService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TodoItem>> GetAllForUserAsync(User user)
    {
        return await _db.Todos
            .Where(t => t.UserId == user.Id)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<TodoItem?> GetByIdForUserAsync(int id, User user)
    {
        return await _db.Todos
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == user.Id);
    }

    public async Task<TodoItem> CreateForUserAsync(string title, User user)
    {
        var item = new TodoItem
        {
            Title = title,
            Status = TodoStatus.NotStarted,
            CreatedAt = DateTime.UtcNow,
            UserId = user.Id,
        };

        _db.Todos.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    public async Task<TodoItem?> UpdateForUserAsync(int id, string title, TodoStatus status, User user)
    {
        var item = await _db.Todos
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == user.Id);
        if (item is null) return null;

        item.Title = title;
        item.Status = status;
        await _db.SaveChangesAsync();
        return item;
    }

    public async Task<bool> DeleteForUserAsync(int id, User user)
    {
        var item = await _db.Todos
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == user.Id);
        if (item is null) return false;

        _db.Todos.Remove(item);
        await _db.SaveChangesAsync();
        return true;
    }
}
