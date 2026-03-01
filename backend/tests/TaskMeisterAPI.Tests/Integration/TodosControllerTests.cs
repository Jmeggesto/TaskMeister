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
/// xUnit creates a fresh class instance per test method, so each test gets its own
/// TestWebApplicationFactory (and its own isolated in-memory database).
/// </summary>
public class TodosControllerTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient                _client;

    // JSON options matching the app's JsonStringEnumConverter(SnakeCaseLower) setting.
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public TodosControllerTests()
    {
        _factory = new TestWebApplicationFactory();
        _client  = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
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
        await _client.PostAsJsonAsync("/api/todos", new { title = "A" });
        await _client.PostAsJsonAsync("/api/todos", new { title = "B" });
        await _client.PostAsJsonAsync("/api/todos", new { title = "C" });

        var resp = await _client.GetAsync("/api/todos");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await resp.Content.ReadAsStringAsync();
        var items = JsonSerializer.Deserialize<JsonElement[]>(content)!;
        items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAll_ReturnsTodos_OrderedNewestFirst()
    {
        await _client.PostAsJsonAsync("/api/todos", new { title = "First" });
        await _client.PostAsJsonAsync("/api/todos", new { title = "Second" });
        await _client.PostAsJsonAsync("/api/todos", new { title = "Third" });

        var resp  = await _client.GetAsync("/api/todos");
        var items = JsonSerializer.Deserialize<JsonElement[]>(await resp.Content.ReadAsStringAsync())!;

        // Newest (highest Id) should appear first because service orders by CreatedAt desc.
        var ids = items.Select(i => i.GetProperty("id").GetInt32()).ToList();
        ids.Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task GetAll_SerializesStatus_AsSnakeCaseString()
    {
        await _client.PostAsJsonAsync("/api/todos", new { title = "Status check" });

        var resp = await _client.GetAsync("/api/todos");
        var body = await resp.Content.ReadAsStringAsync();

        // Status must be "not_started", not "NotStarted".
        body.Should().Contain("not_started");
        body.Should().NotContain("NotStarted");
    }

    // -------------------------------------------------------------------------
    // GET /api/todos/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetById_Returns200_WithCorrectTodo()
    {
        var todo   = await PostTodoAsync("Get by id");
        var id     = todo.GetProperty("id").GetInt32();
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
        var id = todo.GetProperty("id").GetInt32();

        var resp = await _client.GetAsync($"/api/todos/{id}");
        var doc  = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;

        doc.GetProperty("id").GetInt32().Should().Be(id);
        doc.GetProperty("title").GetString().Should().Be("Three");
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
        var todo   = await PostTodoAsync("Persist test");
        var id = todo.GetProperty("id").GetInt32();
        var resp = await _client.GetAsync($"/api/todos/{id}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // -------------------------------------------------------------------------
    // PUT /api/todos/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Update_Returns200_WithUpdatedValues()
    {
        var todo   = await PostTodoAsync("Original");
        var id = todo.GetProperty("id").GetInt32();
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
        var id = todo.GetProperty("id").GetInt32();

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
        var id = todo.GetProperty("id").GetInt32();

        await _client.PutAsJsonAsync($"/api/todos/{id}",
            new { title = "After update", status = "done" });

        var resp = await _client.GetAsync($"/api/todos/{id}");
        var doc  = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
        doc.GetProperty("title").GetString().Should().Be("After update");
        doc.GetProperty("status").GetString().Should().Be("done");
    }

    // -------------------------------------------------------------------------
    // DELETE /api/todos/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Delete_Returns204NoContent_WhenIdExists()
    {
        var todo   = await PostTodoAsync("Delete me");
        var id = todo.GetProperty("id").GetInt32();
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
        var id = todo.GetProperty("id").GetInt32();

        await _client.DeleteAsync($"/api/todos/{id}");
        var resp = await _client.GetAsync($"/api/todos/{id}");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_DoesNotAffectOtherTodos()
    {
        var todoKeep   = await PostTodoAsync("Keep");
        var todoRemove = await PostTodoAsync("Remove");

        var idKeep = todoKeep.GetProperty("id").GetInt32();
        var idRemove = todoRemove.GetProperty("id").GetInt32();

        await _client.DeleteAsync($"/api/todos/{idRemove}");

        var keepResp = await _client.GetAsync($"/api/todos/{idKeep}");
        keepResp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // -------------------------------------------------------------------------
    // JSON shape
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Response_TodoItem_HasExpectedFields()
    {
        var todo   = await PostTodoAsync("Shape check");
        var id     = todo.GetProperty("id").GetInt32();
        var resp = await _client.GetAsync($"/api/todos/{id}");
        var doc  = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;

        doc.TryGetProperty("id",        out _).Should().BeTrue();
        doc.TryGetProperty("title",     out _).Should().BeTrue();
        doc.TryGetProperty("status",    out _).Should().BeTrue();
        doc.TryGetProperty("createdAt", out _).Should().BeTrue();
    }
}
