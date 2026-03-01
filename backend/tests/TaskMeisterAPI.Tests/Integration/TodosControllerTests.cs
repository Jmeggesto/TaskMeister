using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using TaskMeisterAPI.Models.Entities;
using TaskMeisterAPI.Tests.Fixtures;
using Xunit;

namespace TaskMeisterAPI.Tests.Integration;

/// <summary>
/// Integration tests for the Todos HTTP API.
///
/// IAsyncLifetime.InitializeAsync seeds a test user into the in-memory database
/// and sets the authenticated client's default Authorization header before each
/// test method runs.  xUnit creates a fresh class instance per test, so each
/// test gets its own factory, database, and authenticated client.
/// </summary>
public class TodosControllerTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient                _client;

    public TodosControllerTests()
    {
        _factory = new TestWebApplicationFactory();
        _client  = _factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        // Seed a test user so OnTokenValidated can find them in the DB,
        // then wire up the default auth header for all subsequent requests.
        await _factory.SeedAsync(async db =>
        {
            var user = new User
            {
                Name      = "Test User",
                Email     = "test@example.com",
                Password  = "hashed",
                CreatedOn = DateTime.UtcNow,
                UpdatedOn = DateTime.UtcNow,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(); // user.Id is populated by EF after this call

            var token = TestAuthHelper.GenerateToken(user.Id, user.Name, user.TokenVersion);
            _client.DefaultRequestHeaders.Authorization = TestAuthHelper.AuthHeader(token);
        });
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task<JsonElement> PostTodoAsync(string title)
    {
        var resp = await _client.PostAsJsonAsync("/api/todos", new { title });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await resp.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement;
    }

    // Returns a fresh client with no Authorization header for testing 401 paths.
    private HttpClient AnonClient() => _factory.CreateClient();

    // -------------------------------------------------------------------------
    // Auth enforcement — all endpoints must reject unauthenticated requests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAll_Returns401_WhenNotAuthenticated()
    {
        using var client = AnonClient();
        var resp = await client.GetAsync("/api/todos");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_Returns401_WhenNotAuthenticated()
    {
        using var client = AnonClient();
        var resp = await client.GetAsync("/api/todos/1");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_Returns401_WhenNotAuthenticated()
    {
        using var client = AnonClient();
        var resp = await client.PostAsJsonAsync("/api/todos", new { title = "Ghost" });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_Returns401_WhenNotAuthenticated()
    {
        using var client = AnonClient();
        var resp = await client.PutAsJsonAsync("/api/todos/1", new { title = "Ghost", status = "done" });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_Returns401_WhenNotAuthenticated()
    {
        using var client = AnonClient();
        var resp = await client.DeleteAsync("/api/todos/1");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -------------------------------------------------------------------------
    // GET /api/todos
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAll_Returns200_WithEmptyArray_WhenNoTodosExist()
    {
        var resp = await _client.GetAsync("/api/todos");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Trim().Should().Be("[]");
    }

    [Fact]
    public async Task GetAll_Returns200_WithAllCreatedTodos()
    {
        await PostTodoAsync("A");
        await PostTodoAsync("B");
        await PostTodoAsync("C");

        var resp = await _client.GetAsync("/api/todos");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = JsonSerializer.Deserialize<JsonElement[]>(await resp.Content.ReadAsStringAsync())!;
        items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAll_ReturnsTodos_OrderedNewestFirst()
    {
        await PostTodoAsync("First");
        await PostTodoAsync("Second");
        await PostTodoAsync("Third");

        var resp  = await _client.GetAsync("/api/todos");
        var items = JsonSerializer.Deserialize<JsonElement[]>(await resp.Content.ReadAsStringAsync())!;

        // Newest (highest Id) should appear first because service orders by CreatedAt desc.
        var ids = items.Select(i => i.GetProperty("id").GetInt32()).ToList();
        ids.Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task GetAll_SerializesStatus_AsSnakeCaseString()
    {
        await PostTodoAsync("Status check");

        var resp = await _client.GetAsync("/api/todos");
        var body = await resp.Content.ReadAsStringAsync();

        // Status must be "not_started", not "NotStarted".
        body.Should().Contain("not_started");
        body.Should().NotContain("NotStarted");
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyCurrentUsersTodos()
    {
        // Create a todo as the primary authenticated user.
        await PostTodoAsync("Primary user's todo");

        // Sign up a second user through the API and get their token.
        var signupResp = await _factory.CreateClient().PostAsJsonAsync("/api/users/signup",
            new { name = "Other User", email = "other@example.com", password = "SecurePass123!" });
        var signupDoc = JsonDocument.Parse(await signupResp.Content.ReadAsStringAsync()).RootElement;
        var otherToken = signupDoc.GetProperty("token").GetString()!;

        // Create a todo as the second user.
        using var otherClient = _factory.CreateClient();
        otherClient.DefaultRequestHeaders.Authorization = TestAuthHelper.AuthHeader(otherToken);
        await otherClient.PostAsJsonAsync("/api/todos", new { title = "Other user's todo" });

        // The primary user should only see their own todo.
        var resp  = await _client.GetAsync("/api/todos");
        var items = JsonSerializer.Deserialize<JsonElement[]>(await resp.Content.ReadAsStringAsync())!;

        items.Should().HaveCount(1);
        items[0].GetProperty("title").GetString().Should().Be("Primary user's todo");
    }

    // -------------------------------------------------------------------------
    // GET /api/todos/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetById_Returns200_WithCorrectTodo()
    {
        var todo = await PostTodoAsync("Get by id");
        var id   = todo.GetProperty("id").GetInt32();

        var resp = await _client.GetAsync($"/api/todos/{id}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
        doc.GetProperty("id").GetInt32().Should().Be(id);
        doc.GetProperty("title").GetString().Should().Be("Get by id");
    }

    [Fact]
    public async Task GetById_Returns404_WhenIdDoesNotExist()
    {
        var resp = await _client.GetAsync("/api/todos/99999");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_ReturnsCorrectTodo_WhenMultipleExist()
    {
        await PostTodoAsync("One");
        await PostTodoAsync("Two");
        var todo = await PostTodoAsync("Three");
        var id   = todo.GetProperty("id").GetInt32();

        var resp = await _client.GetAsync($"/api/todos/{id}");
        var doc  = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;

        doc.GetProperty("id").GetInt32().Should().Be(id);
        doc.GetProperty("title").GetString().Should().Be("Three");
    }

    [Fact]
    public async Task GetById_Returns404_WhenTodoBelongsToAnotherUser()
    {
        // Sign up a second user and create a todo as them.
        var signupResp = await _factory.CreateClient().PostAsJsonAsync("/api/users/signup",
            new { name = "Other User", email = "other@example.com", password = "SecurePass123!" });
        var otherToken = JsonDocument.Parse(await signupResp.Content.ReadAsStringAsync())
            .RootElement.GetProperty("token").GetString()!;

        using var otherClient = _factory.CreateClient();
        otherClient.DefaultRequestHeaders.Authorization = TestAuthHelper.AuthHeader(otherToken);
        var otherTodo = JsonDocument.Parse(
            await (await otherClient.PostAsJsonAsync("/api/todos", new { title = "Not yours" }))
                .Content.ReadAsStringAsync()).RootElement;
        var otherId = otherTodo.GetProperty("id").GetInt32();

        // The primary user should not be able to access the other user's todo.
        var resp = await _client.GetAsync($"/api/todos/{otherId}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -------------------------------------------------------------------------
    // POST /api/todos
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_Returns201Created_WithNewTodo()
    {
        var resp = await _client.PostAsJsonAsync("/api/todos", new { title = "New task" });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
        doc.GetProperty("title").GetString().Should().Be("New task");
    }

    [Fact]
    public async Task Create_ReturnedTodo_HasNotStartedStatus()
    {
        var resp = await _client.PostAsJsonAsync("/api/todos", new { title = "Status test" });
        var doc  = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;

        doc.GetProperty("status").GetString().Should().Be("not_started");
    }

    [Fact]
    public async Task Create_SetsCreatedAt_ToNearUtcNow()
    {
        var before = DateTime.UtcNow;
        var resp   = await _client.PostAsJsonAsync("/api/todos", new { title = "Timestamp" });
        var doc    = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;

        var createdAt = doc.GetProperty("createdAt").GetDateTime();
        createdAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task Create_PersistedToDatabase_VerifiedByGetById()
    {
        var todo = await PostTodoAsync("Persist test");
        var id   = todo.GetProperty("id").GetInt32();

        var resp = await _client.GetAsync($"/api/todos/{id}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // -------------------------------------------------------------------------
    // PUT /api/todos/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Update_Returns200_WithUpdatedValues()
    {
        var todo = await PostTodoAsync("Original");
        var id   = todo.GetProperty("id").GetInt32();

        var resp = await _client.PutAsJsonAsync($"/api/todos/{id}",
            new { title = "Updated", status = "in_progress" });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
        doc.GetProperty("title").GetString().Should().Be("Updated");
        doc.GetProperty("status").GetString().Should().Be("in_progress");
    }

    [Fact]
    public async Task Update_Returns404_WhenIdDoesNotExist()
    {
        var resp = await _client.PutAsJsonAsync("/api/todos/99999",
            new { title = "Ghost", status = "done" });

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_CanTransitionStatusToAllValues()
    {
        var todo = await PostTodoAsync("Transitions");
        var id   = todo.GetProperty("id").GetInt32();

        foreach (var status in new[] { "in_progress", "done", "not_started" })
        {
            var resp = await _client.PutAsJsonAsync($"/api/todos/{id}",
                new { title = "Transitions", status });
            resp.StatusCode.Should().Be(HttpStatusCode.OK,
                because: $"status '{status}' should be accepted");
            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
            doc.GetProperty("status").GetString().Should().Be(status);
        }
    }

    [Fact]
    public async Task Update_PersistsChanges_VerifiedByGetById()
    {
        var todo = await PostTodoAsync("Before update");
        var id   = todo.GetProperty("id").GetInt32();

        await _client.PutAsJsonAsync($"/api/todos/{id}",
            new { title = "After update", status = "done" });

        var resp = await _client.GetAsync($"/api/todos/{id}");
        var doc  = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
        doc.GetProperty("title").GetString().Should().Be("After update");
        doc.GetProperty("status").GetString().Should().Be("done");
    }

    [Fact]
    public async Task Update_Returns404_WhenTodoBelongsToAnotherUser()
    {
        // Create a todo as a second user.
        var signupResp = await _factory.CreateClient().PostAsJsonAsync("/api/users/signup",
            new { name = "Other User", email = "other@example.com", password = "SecurePass123!" });
        var otherToken = JsonDocument.Parse(await signupResp.Content.ReadAsStringAsync())
            .RootElement.GetProperty("token").GetString()!;

        using var otherClient = _factory.CreateClient();
        otherClient.DefaultRequestHeaders.Authorization = TestAuthHelper.AuthHeader(otherToken);
        var otherTodo = JsonDocument.Parse(
            await (await otherClient.PostAsJsonAsync("/api/todos", new { title = "Not yours" }))
                .Content.ReadAsStringAsync()).RootElement;
        var otherId = otherTodo.GetProperty("id").GetInt32();

        // The primary user should not be able to update the other user's todo.
        var resp = await _client.PutAsJsonAsync($"/api/todos/{otherId}",
            new { title = "Hijacked", status = "done" });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -------------------------------------------------------------------------
    // DELETE /api/todos/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Delete_Returns204NoContent_WhenIdExists()
    {
        var todo = await PostTodoAsync("Delete me");
        var id   = todo.GetProperty("id").GetInt32();

        var resp = await _client.DeleteAsync($"/api/todos/{id}");

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_Returns404_WhenIdDoesNotExist()
    {
        var resp = await _client.DeleteAsync("/api/todos/99999");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_RemovesItem_VerifiedByGetById()
    {
        var todo = await PostTodoAsync("Gone after delete");
        var id   = todo.GetProperty("id").GetInt32();

        await _client.DeleteAsync($"/api/todos/{id}");

        var resp = await _client.GetAsync($"/api/todos/{id}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_DoesNotAffectOtherTodos()
    {
        var keep   = await PostTodoAsync("Keep");
        var remove = await PostTodoAsync("Remove");

        var idKeep   = keep.GetProperty("id").GetInt32();
        var idRemove = remove.GetProperty("id").GetInt32();

        await _client.DeleteAsync($"/api/todos/{idRemove}");

        var resp = await _client.GetAsync($"/api/todos/{idKeep}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_Returns404_WhenTodoBelongsToAnotherUser()
    {
        // Create a todo as a second user.
        var signupResp = await _factory.CreateClient().PostAsJsonAsync("/api/users/signup",
            new { name = "Other User", email = "other@example.com", password = "SecurePass123!" });
        var otherToken = JsonDocument.Parse(await signupResp.Content.ReadAsStringAsync())
            .RootElement.GetProperty("token").GetString()!;

        using var otherClient = _factory.CreateClient();
        otherClient.DefaultRequestHeaders.Authorization = TestAuthHelper.AuthHeader(otherToken);
        var otherTodo = JsonDocument.Parse(
            await (await otherClient.PostAsJsonAsync("/api/todos", new { title = "Not yours" }))
                .Content.ReadAsStringAsync()).RootElement;
        var otherId = otherTodo.GetProperty("id").GetInt32();

        // The primary user should not be able to delete the other user's todo.
        var resp = await _client.DeleteAsync($"/api/todos/{otherId}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -------------------------------------------------------------------------
    // JSON shape
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Response_TodoItem_HasExpectedFields()
    {
        var todo = await PostTodoAsync("Shape check");
        var id   = todo.GetProperty("id").GetInt32();

        var resp = await _client.GetAsync($"/api/todos/{id}");
        var doc  = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;

        doc.TryGetProperty("id",        out _).Should().BeTrue();
        doc.TryGetProperty("title",     out _).Should().BeTrue();
        doc.TryGetProperty("status",    out _).Should().BeTrue();
        doc.TryGetProperty("createdAt", out _).Should().BeTrue();
    }
}
