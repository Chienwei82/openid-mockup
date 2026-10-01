namespace OidcMock.Core.Authorization;

/// <summary>
/// Modos de respuesta de la peticion de autorizacion que anuncia el discovery del servidor real.
/// </summary>
public static class ResponseModes
{
    public const string Query = "query";
    public const string Fragment = "fragment";
    public const string FormPost = "form_post";
}