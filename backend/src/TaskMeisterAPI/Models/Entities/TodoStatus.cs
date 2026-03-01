namespace TaskMeisterAPI.Models.Entities;

/// <summary>
/// Lifecycle state of a todo item.
/// Serialised to/from JSON as snake_case strings: not_started, in_progress, done.
/// </summary>
public enum TodoStatus
{
    NotStarted,
    InProgress,
    Done,
}
