using System.ComponentModel.DataAnnotations.Schema;

namespace TaskMeisterAPI.Models.Entities;

[Table("Users")]
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty; // stored as PBKDF2 hash
    public DateTime CreatedOn { get; set; }
    public DateTime UpdatedOn { get; set; }
    /// <summary>
    /// Incremented on logout. JWTs carrying an older version are rejected.
    /// </summary>
    public int TokenVersion { get; set; } = 1;
}
