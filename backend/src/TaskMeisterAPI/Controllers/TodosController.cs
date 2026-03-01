using Microsoft.AspNetCore.Mvc;
using TaskMeisterAPI.Models.Requests;
using TaskMeisterAPI.Services;

namespace TaskMeisterAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TodosController : ControllerBase
{
    private readonly ITodoService _todoService;

    public TodosController(ITodoService todoService)
    {
        _todoService = todoService;
    }

    // GET api/todos
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var todos = await _todoService.GetAllAsync();
        return Ok(todos);
    }

    // GET api/todos/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var todo = await _todoService.GetByIdAsync(id);
        return todo is null ? NotFound() : Ok(todo);
    }

    // POST api/todos
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTodoRequest request)
    {
        var todo = await _todoService.CreateAsync(request.Title);
        return CreatedAtAction(nameof(GetById), new { id = todo.Id }, todo);
    }

    // PUT api/todos/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTodoRequest request)
    {
        var todo = await _todoService.UpdateAsync(id, request.Title, request.Status);
        return todo is null ? NotFound() : Ok(todo);
    }

    // DELETE api/todos/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _todoService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
