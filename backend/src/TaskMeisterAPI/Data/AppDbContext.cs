using Microsoft.EntityFrameworkCore;
using TaskMeisterAPI.Models.Entities;

namespace TaskMeisterAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<TodoItem> Todos => Set<TodoItem>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Name).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
        });
    }
}
