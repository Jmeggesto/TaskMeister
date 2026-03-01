using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaskMeisterAPI.Data;
using TaskMeisterAPI.Models.Entities;
using TaskMeisterAPI.Services;
using Xunit;

namespace TaskMeisterAPI.Tests.Unit.Services;

public class TodoServiceTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static TodoService CreateService(AppDbContext db) => new(db);

    // -------------------------------------------------------------------------
    // GetAllAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyList_WhenNoTodosExist()
    {
        using var db = CreateDbContext();
        var svc = CreateService(db);

        var result = await svc.GetAllAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllTodos_OrderedByCreatedAtDescending()
    {
        using var db = CreateDbContext();
        var svc = CreateService(db);

        var oldest  = new TodoItem { Title = "Oldest",  CreatedAt = DateTime.UtcNow.AddHours(-2) };
        var middle  = new TodoItem { Title = "Middle",  CreatedAt = DateTime.UtcNow.AddHours(-1) };
        var newest  = new TodoItem { Title = "Newest",  CreatedAt = DateTime.UtcNow };
        db.Todos.AddRange(oldest, middle, newest);
        await db.SaveChangesAsync();

        var result = await svc.GetAllAsync();

        result.Should().HaveCount(3);
        result[0].Title.Should().Be("Newest");
        result[1].Title.Should().Be("Middle");
        result[2].Title.Should().Be("Oldest");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsReadOnlyList()
    {
        using var db = CreateDbContext();
        var svc = CreateService(db);
        db.Todos.Add(new TodoItem { Title = "Test" });
        await db.SaveChangesAsync();

        var result = await svc.GetAllAsync();

        result.Should().BeAssignableTo<IReadOnlyList<TodoItem>>();
    }

    // -------------------------------------------------------------------------
    // GetByIdAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_ReturnsTodo_WhenIdExists()
    {
        using var db = CreateDbContext();
        var svc = CreateService(db);

        var item = new TodoItem { Title = "Find me" };
        db.Todos.Add(item);
        await db.SaveChangesAsync();

        var result = await svc.GetByIdAsync(item.Id);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Find me");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenIdDoesNotExist()
    {
        using var db = CreateDbContext();
        var svc = CreateService(db);

        var result = await svc.GetByIdAsync(999);

        result.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // CreateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ReturnsNewTodo_WithNotStartedStatus()
    {
        using var db = CreateDbContext();
        var svc = CreateService(db);

        var result = await svc.CreateAsync("My new task");

        result.Title.Should().Be("My new task");
        result.Status.Should().Be(TodoStatus.NotStarted);
    }

    [Fact]
    public async Task CreateAsync_SetsCreatedAt_ToNearCurrentUtc()
    {
        using var db = CreateDbContext();
        var svc = CreateService(db);
        var before = DateTime.UtcNow;

        var result = await svc.CreateAsync("Timestamp test");

        result.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public async Task CreateAsync_PersistedToDatabase()
    {
        using var db = CreateDbContext();
        var svc = CreateService(db);

        var created = await svc.CreateAsync("Persist me");
        var fetched = await svc.GetByIdAsync(created.Id);

        fetched.Should().NotBeNull();
        fetched!.Title.Should().Be("Persist me");
    }

    [Fact]
    public async Task CreateAsync_AssignsUniqueIds_ForMultipleTodos()
    {
        using var db = CreateDbContext();
        var svc = CreateService(db);

        var first  = await svc.CreateAsync("First");
        var second = await svc.CreateAsync("Second");

        second.Id.Should().BeGreaterThan(first.Id);
    }

    // -------------------------------------------------------------------------
    // UpdateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_UpdatesTitleAndStatus_WhenIdExists()
    {
        using var db = CreateDbContext();
        var svc = CreateService(db);
        var item = await svc.CreateAsync("Original");

        var result = await svc.UpdateAsync(item.Id, "Updated", TodoStatus.Done);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Updated");
        result.Status.Should().Be(TodoStatus.Done);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenIdDoesNotExist()
    {
        using var db = CreateDbContext();
        var svc = CreateService(db);

        var result = await svc.UpdateAsync(999, "title", TodoStatus.InProgress);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_PersistsToDatabaseCorrectly()
    {
        using var db = CreateDbContext();
        var svc = CreateService(db);
        var item = await svc.CreateAsync("Before");

        await svc.UpdateAsync(item.Id, "After", TodoStatus.InProgress);
        var fetched = await svc.GetByIdAsync(item.Id);

        fetched!.Title.Should().Be("After");
        fetched.Status.Should().Be(TodoStatus.InProgress);
    }

    // -------------------------------------------------------------------------
    // DeleteAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_ReturnsTrue_WhenIdExists()
    {
        using var db = CreateDbContext();
        var svc = CreateService(db);
        var item = await svc.CreateAsync("Delete me");

        var result = await svc.DeleteAsync(item.Id);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenIdDoesNotExist()
    {
        using var db = CreateDbContext();
        var svc = CreateService(db);

        var result = await svc.DeleteAsync(999);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_RemovesItemFromDatabase()
    {
        using var db = CreateDbContext();
        var svc = CreateService(db);
        var item = await svc.CreateAsync("Gone");

        await svc.DeleteAsync(item.Id);
        var fetched = await svc.GetByIdAsync(item.Id);

        fetched.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_DoesNotAffectOtherTodos()
    {
        using var db = CreateDbContext();
        var svc = CreateService(db);
        var keep   = await svc.CreateAsync("Keep");
        var remove = await svc.CreateAsync("Remove");

        await svc.DeleteAsync(remove.Id);
        var remaining = await svc.GetAllAsync();

        remaining.Should().HaveCount(1);
        remaining[0].Id.Should().Be(keep.Id);
    }
}
