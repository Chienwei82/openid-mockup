namespace OidcMock.Core.Scopes;

/// <summary>
/// Scope registrado y los claims que el mock expone cuando se solicita.
/// </summary>
public sealed record ScopeDefinition(string Name, IReadOnlyList<string> Claims);
