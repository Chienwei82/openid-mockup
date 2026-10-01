namespace OidcMock.Core.Grants;

/// <summary>
/// Coleccion de estrategias de grant. Permite agregar un grant nuevo sin modificar el dispatcher
/// del token endpoint, y responder unsupported_grant_type a partir de lo registrado.
/// </summary>
public sealed class GrantHandlerRegistry(IEnumerable<IGrantHandler> handlers)
{
    private readonly Dictionary<string, IGrantHandler> _handlers =
        handlers.ToDictionary(handler => handler.GrantType, StringComparer.Ordinal);

    public IGrantHandler? Find(string grantType) =>
        _handlers.GetValueOrDefault(grantType ?? string.Empty);

    public bool Supports(string grantType) => Find(grantType) is not null;
}