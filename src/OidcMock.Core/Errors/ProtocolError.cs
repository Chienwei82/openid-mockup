namespace OidcMock.Core.Errors;

/// <summary>
/// Error de protocolo OAuth 2.0 / OpenID Connect: el codigo que la especificacion exige devolver,
/// una descripcion legible y el codigo HTTP con el que responde el endpoint.
/// </summary>
public sealed record ProtocolError(string Code, string Description, int StatusCode)
{
    public override string ToString() => $"{Code} ({StatusCode}): {Description}";
}