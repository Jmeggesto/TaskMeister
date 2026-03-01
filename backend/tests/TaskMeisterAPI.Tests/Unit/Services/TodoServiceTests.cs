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

    private static async Task<User> CreateUserAsync(AppDbContext db, int id = 1)
    {
        var user = new User
        {
            Id        = id,
            Name      = $"User {id}",
            Email     = $"user{id}@example.com",
            Password  = "hash",
            CreatedOn = DateTime.UtcNow,
            UpdatedOn = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    // -------------------------------------------------------------------------
    // GetAllForUserAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllForUserAsync_ReturnsEmptyList_WhenNoTodosExist()
    {
        using var db = CreateDbContext();
        var svc  = CreateService(db);
        var user = await CreateUserAsync(db);

        var result = await svc.GetAllForUserAsync(user);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllForUserAsync_ReturnsTodosOrderedByCreatedAtDescending()
    {
        using var db = CreateDbContext();
        var svc  = CreateService(db);
        var user = await CreateUserAsync(db);

        var oldest = new TodoItem { Title = "Oldest", CreatedAt = DateTime.UtcNow.AddHours(-2), UserId = user.Id };
        var middle = new TodoItem { Title = "Middle", CreatedAt = DateTime.UtcNow.AddHours(-1), UserId = user.Id };
        var newest = new TodoItem { Title = "Newest", CreatedAt = DateTime.UtcNow,              UserId = user.Id };
        db.Todos.AddRange(oldest, middle, newest);
        await db.SaveChangesAsync();

        var result = await svc.GetAllForUserAsync(user);

        result.Should().HaveCount(3);
        result[0].Title.Should().Be("Newest");
        result[1].Title.Should().Be("Middle");
        result[2].Title.Should().Be("Oldest");
    }

    [Fact]
    public async Task GetAllForUserAsync_ReturnsReadOnlyList()
    {
        using var db = CreateDbContext();
        var svc  = CreateService(db);
        var user = await CreateUserAsync(db);
        await svc.CreateForUserAsync("Test", user);

        var result = await svc.GetAllForUserAsync(user);

        result.Should().BeAssignableTo<IReadOnlyList<TodoItem>>();
    }

    [Fact]
    public async Task GetAllForUserAsync_ReturnsOnlyCurrentUsersTodos()
    {
        using var db = CreateDbContext();
        var svc   = CreateService(db);
        var userA = await CreateUserAsync(db, id: 1);
        var userB = await CreateUserAsync(db, id: 2);

        await svc.CreateForUserAsync("A's task",   userA);
        await svc.CreateForUserAsync("B's task 1", userB);
        await svc.CreateForUserAsync("B's task 2", userB);

        var resultA = await svc.GetAllForUserAsync(userA);
        var resultB = await svc.GetAllForUserAsync(userB);

        resultA.Should().HaveCount(1).And.OnlyContain(t => t.UserId == userA.Id);
        resultB.Should().HaveCount(2).And.OnlyContain(t => t.UserId == userB.Id);
    }

    // -------------------------------------------------------------------------
    // GetByIdForUserAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdForUserAsync_ReturnsTodo_WhenIdExists()
    {
        using var db = CreateDbContext();
        var svc  = CreateService(db);
        var user = await CreateUserAsync(db);

        var item   = await svc.CreateForUserAsync("Find me", user);
        var result = await svc.GetByIdForUserAsync(item.Id, user);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Find me");
    }

    [Fact]
    public async Task GetByIdForUserAsync_ReturnsNull_WhenIdDoesNotExist()
    {
        using var db = CreateDbContext();
        var svc  = CreateService(db);
        var user = await CreateUserAsync(db);

        var result = await svc.GetByIdForUserAsync(999, user);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdForUserAsync_ReturnsNull_WhenTodoBelongsToAnotherUser()
    {
        using var db = CreateDbContext();
        var svc   = CreateService(db);
        var userA = await CreateUserAsync(db, id: 1);
        var userB = await CreateUserAsync(db, id: 2);

        var item = await svc.CreateForUserAsync("A's task", userA);

        var result = await svc.GetByIdForUserAsync(item.Id, userB);

        result.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // CreateForUserAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateForUserAsync_ReturnsNewTodo_WithNotStartedStatus()
    {
        using var db = CreateDbContext();
        var svc  = CreateService(db);
        var user = await CreateUserAsync(db);

        var result = await svc.CreateForUserAsync("My new task", user);

        result.Title.Should().Be("My new task");
        result.Status.Should().Be(TodoStatus.NotStarted);
    }

    [Fact]
    public async Task CreateForUserAsync_SetsCreatedAt_ToNearCurrentUtc()
    {
        using var db = CreateDbContext();
        var svc    = CreateService(db);
        var user   = await CreateUserAsync(db);
        var before = DateTime.UtcNow;

        var result = await svc.CreateForUserAsync("Timestamp test", user);

        result.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public async Task CreateForUserAsync_AssignsUserId()
    {
        using var db = CreateDbContext();
        var svc  = CreateService(db);
        var user = await CreateUserAsync(db);

        var result = await svc.CreateForUserAsync("User-owned task", user);

        result.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task CreateForUserAsync_PersistedToDatabase()
    {
        using var db = CreateDbContext();
        var svc  = CreateService(db);
        var user = await CreateUserAsync(db);

        var created = await svc.CreateForUserAsync("Persist me", user);
        var fetched = await svc.GetByIdForUserAsync(created.Id, user);

        fetched.Should().NotBeNull();
        fetched!.Title.Should().Be("Persist me");
    }

    [Fact]
    public async Task CreateForUserAsync_AssignsUniqueIds_ForMultipleTodos()
    {
        using var db = CreateDbContext();
        var svc  = CreateService(db);
        var user = await CreateUserAsync(db);

        var first  = await svc.CreateForUserAsync("First",  user);
        var second = await svc.CreateForUserAsync("Second", user);

        second.Id.Should().BeGreaterThan(first.Id);
    }

    // -------------------------------------------------------------------------
    // UpdateForUserAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateForUserAsync_UpdatesTitleAndStatus_WhenIdExists()
    {
        using var db = CreateDbContext();
        var svc  = CreateService(db);
        var user = await CreateUserAsync(db);
        var item = await svc.CreateForUserAsync("Original", user);

        var result = await svc.UpdateForUserAsync(item.Id, "Updated", TodoStatus.Done, user);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Updated");
        result.Status.Should().Be(TodoStatus.Done);
    }

    [Fact]
    public async Task UpdateForUserAsync_ReturnsNull_WhenIdDoesNotExist()
    {
        using var db = CreateDbContext();
        var svc  = CreateService(db);
        var user = await CreateUserAsync(db);

        var result = await svc.UpdateForUserAsync(999, "title", TodoStatus.InProgress, user);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateForUserAsync_ReturnsNull_WhenTodoBelongsToAnotherUser()
    {
        using var db = CreateDbContext();
        var svc   = CreateService(db);
        var userA = await CreateUserAsync(db, id: 1);
        var userB = await CreateUserAsync(db, id: 2);

        var item = await svc.CreateForUserAsync("A's task", userA);

        var result = await svc.UpdateForUserAsync(item.Id, "Hijacked", TodoStatus.Done, userB);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateForUserAsync_PersistsToDatabaseCorrectly()
    {
        using var db = CreateDbContext();
        var svc  = CreateService(db);
        var user = await CreateUserAsync(db);
        var item = await svc.CreateForUserAsync("Before", user);

        await svc.UpdateForUserAsync(item.Id, "After", TodoStatus.InProgress, user);
        var fetched = await svc.GetByIdForUserAsync(item.Id, user);

        fetched!.Title.Should().Be("After");
        fetched.Status.Should().Be(TodoStatus.InProgress);
    }

    // -------------------------------------------------------------------------
    // DeleteForUserAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteForUserAsync_ReturnsTrue_WhenIdExists()
    {
        using var db = CreateDbContext();
        var svc  = CreateService(db);
        var user = await CreateUserAsync(db);
        var item = await svc.CreateForUserAsync("Delete me", user);

        var result = await svc.DeleteForUserAsync(item.Id, user);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteForUserAsync_ReturnsFalse_WhenIdDoesNotExist()
    {
        using var db = CreateDbContext();
        var svc  = CreateService(db);
        var user = await CreateUserAsync(db);

        var result = await svc.DeleteForUserAsync(999, user);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteForUserAsync_ReturnsFalse_WhenTodoBelongsToAnotherUser()
    {
        using var db = CreateDbContext();
        var svc   = CreateService(db);
        var userA = await CreateUserAsync(db, id: 1);
        var userB = await CreateUserAsync(db, id: 2);

        var item = await svc.CreateForUserAsync("A's task", userA);

        var result = await svc.DeleteForUserAsync(item.Id, userB);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteForUserAsync_RemovesItemFromDatabase()
    {
        using var db = CreateDbContext();
        var svc  = CreateService(db);
        var user = await CreateUserAsync(db);
        var item = await svc.CreateForUserAsync("Gone", user);

        await svc.DeleteForUserAsync(item.Id, user);
        var fetched = await svc.GetByIdForUserAsync(item.Id, user);

        fetched.Should().BeNull();
    }

    [Fact]
    public async Task DeleteForUserAsync_DoesNotAffectOtherTodos()
    {
        using var db = CreateDbContext();
        var svc    = CreateService(db);
        var user   = await CreateUserAsync(db);
        var keep   = await svc.CreateForUserAsync("Keep",   user);
        var remove = await svc.CreateForUserAsync("Remove", user);

        await svc.DeleteForUserAsync(remove.Id, user);
        var remaining = await svc.GetAllForUserAsync(user);

        remaining.Should().HaveCount(1);
        remaining[0].Id.Should().Be(keep.Id);
    }
}
