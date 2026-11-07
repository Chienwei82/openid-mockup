namespace OidcMock.Core.Configuration;

/// <summary>
/// Petición de creación o actualización de un scope a través del endpoint de administración.
/// </summary>
public sealed class AdminScopeRequest
{
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<string> Claims { get; set; } = [];
}