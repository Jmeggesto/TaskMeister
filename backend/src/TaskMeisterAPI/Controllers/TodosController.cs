using Microsoft.AspNetCore.Mvc;
using TaskMeisterAPI.Models.Requests;
using TaskMeisterAPI.Services;
using TaskMeisterAPI.Infrastructure.Auth;
using TaskMeisterAPI.Infrastructure.ModelBinding;
using TaskMeisterAPI.Models.Entities;

namespace TaskMeisterAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[ServiceFilter(typeof(RequireUserFilter))]
public class TodosController : ControllerBase
{
    private readonly ITodoService _todoService;

    public TodosController(ITodoService todoService)
    {
        _todoService = todoService;
    }

    // GET api/todos
    [HttpGet]
    public async Task<IActionResult> GetAll([FromUser] User currentUser)
    {
        var todos = await _todoService.GetAllAsync();
        return Ok(todos);
    }

    // GET api/todos/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id, [FromUser] User currentUser)
    {
        var todo = await _todoService.GetByIdAsync(id);
        return todo is null ? NotFound() : Ok(todo);
    }

    // POST api/todos
    [HttpPost]
    public async Task<IActionResult> Create([
        FromBody] CreateTodoRequest request,
        [FromUser] User currentUser
    )
    {
        var todo = await _todoService.CreateAsync(request.Title);
        return CreatedAtAction(nameof(GetById), new { id = todo.Id }, todo);
    }

    // PUT api/todos/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateTodoRequest request,
        [FromUser] User currentUser
    )
    {
        var todo = await _todoService.UpdateAsync(id, request.Title, request.Status);
        return todo is null ? NotFound() : Ok(todo);
    }

    // DELETE api/todos/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, [FromUser] User currentUser)
    {
        var deleted = await _todoService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
