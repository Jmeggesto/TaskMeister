using System.ComponentModel.DataAnnotations;

namespace TaskMeisterAPI.Configuration;

public class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// SQLite connection string. Override via user-secrets (dev) or environment
    /// variable Database__ConnectionString (production).
    /// </summary>
    [Required]
    public string ConnectionString { get; init; } = "Data Source=todos.db";
}
