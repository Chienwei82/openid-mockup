namespace OidcMock.Core.Scopes;

/// <summary>
/// Nombres de scope con significado especial en OpenID Connect.
/// </summary>
public static class ScopeNames
{
    public const string OpenId = "openid";
    public const string Profile = "profile";
    public const string Email = "email";
    public const string Address = "address";
    public const string Phone = "phone";
    public const string OfflineAccess = "offline_access";

    /// <summary>
    /// Separa el valor textual del parametro <c>scope</c> en nombres de scope.
    /// </summary>
    /// <remarks>
    /// RFC 6749 3.3 codifica el scope como una lista de tokens separados por <c>SP</c>, y los clientes
    /// reales no coinciden en el separador ni en los espacios sobrantes. Vive aqui, y no junto a cada
    /// endpoint, para que el token endpoint, PAR, device y CIBA no puedan empezar a interpretarlo
    /// distinto: una diferencia de un espacio es un <c>invalid_scope</c> dificil de diagnosticar.
    /// </remarks>
    public static string[] Split(string? scope) =>
        string.IsNullOrWhiteSpace(scope)
            ? []
            : scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}