using System.Text.Json;
using OidcMock.Core.Scopes;
using OidcMock.Core.Users;

namespace OidcMock.Core.Claims;

/// <summary>
/// Proyecta los claims del usuario leyendo la tabla de claims por scope de <c>scopes.json</c>: la
/// decision de que se expone sale de la configuracion, no de condicionales en el codigo. Los claims
/// declarados que el usuario no tiene se omiten, y los no declarados nunca se emiten.
/// </summary>
public sealed class ScopesClaimsProjector : IClaimsProjector
{
    private readonly IReadOnlyList<IUserClaimSource> _claimSources;
    private readonly IScopeStore _scopeStore;

    public ScopesClaimsProjector(IScopeStore scopeStore, IReadOnlyList<IUserClaimSource>? claimSources = null)
    {
        ArgumentNullException.ThrowIfNull(scopeStore);

        _scopeStore = scopeStore;
        _claimSources = claimSources ?? DefaultClaimSources();
    }

    public IReadOnlyDictionary<string, JsonElement> Project(User user, IReadOnlyList<string> grantedScopes)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(grantedScopes);

        return grantedScopes
            .SelectMany(ClaimsGrantedBy)
            .Distinct(StringComparer.Ordinal)
            .Select(claimName => Resolve(claimName, user))
            .OfType<ProjectedClaim>()
            .ToDictionary(claim => claim.Name, claim => claim.Value, StringComparer.Ordinal);
    }

    private IReadOnlyList<string> ClaimsGrantedBy(string scopeName) =>
        _scopeStore.Find(scopeName)?.Claims ?? [];

    private ProjectedClaim? Resolve(string claimName, User user)
    {
        foreach (var claimSource in _claimSources)
        {
            if (claimSource.TryResolve(claimName, user, out var value))
            {
                return new ProjectedClaim(claimName, value);
            }
        }

        return null;
    }

    private static IReadOnlyList<IUserClaimSource> DefaultClaimSources() =>
        [new SubjectClaimSource(), new UserDictionaryClaimSource()];

    private sealed record ProjectedClaim(string Name, JsonElement Value);
}