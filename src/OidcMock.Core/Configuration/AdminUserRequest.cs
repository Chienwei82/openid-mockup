using System.Collections.Generic;

namespace OidcMock.Core.Configuration;

/// <summary>
/// Petición de creación o actualización de un usuario a través del endpoint de administración.
/// </summary>
public sealed class AdminUserRequest
{
    public string Subject { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, string> Claims { get; set; } = new Dictionary<string, string>();
}