using System.ComponentModel.DataAnnotations;

namespace TaskMeisterAPI.Configuration;

public class AppOptions
{
    public const string SectionName = "App";

    [Required]
    public string Name { get; init; } = "TaskMeister";

    /// <summary>
    /// Origins the CORS policy will allow. Add entries per environment via
    /// appsettings.{Environment}.json or the App__AllowedOrigins env var.
    /// </summary>
    public string[] AllowedOrigins { get; init; } = [];
}
