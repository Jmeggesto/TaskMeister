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

    public async Task<IReadOnlyList<TodoItem>> GetAllAsync()
    {
        return await _db.Todos            
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<TodoItem?> GetByIdAsync(int id)
    {
        return await _db.Todos.FindAsync(id);
    }

    public async Task<TodoItem> CreateAsync(string title)
    {
        var item = new TodoItem
        {
            Title = title,
            Status = TodoStatus.NotStarted,
            CreatedAt = DateTime.UtcNow
        };

        _db.Todos.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    public async Task<TodoItem?> UpdateAsync(int id, string title, TodoStatus status)
    {
        var item = await _db.Todos.FindAsync(id);
        if (item is null) return null;

        item.Title = title;
        item.Status = status;
        await _db.SaveChangesAsync();
        return item;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var item = await _db.Todos.FindAsync(id);
        if (item is null) return false;

        _db.Todos.Remove(item);
        await _db.SaveChangesAsync();
        return true;
    }
}
