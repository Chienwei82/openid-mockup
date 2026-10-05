namespace OidcMock.Core.Authorization;

/// <summary>
/// Valores de response_type. Hay dos listas distintas y no son intercambiables: la que el
/// discovery anuncia y la que el endpoint de autorizacion sabe responder.
/// </summary>
public static class ResponseTypeNames
{
    public const string Code = "code";
    public const string Token = "token";
    public const string IdToken = "id_token";

    /// <summary>
    /// Combinaciones que el endpoint de autorizacion del mock acepta y responde. El mock solo
    /// emite codigo de autorizacion (D-019), asi que el unico flow completo es <c>code</c>.
    /// Deliberadamente no incluye las combinaciones que declaran tokens en el fragmento: aceptarlas
    /// sin emitirlos devolveria al cliente un <c>code</c> donde pidio tokens, sin ningun error que
    /// lo explicara, que es peor que un <c>unsupported_response_type</c> honesto.
    /// </summary>
    public static readonly string[] EmittedByAuthorizationEndpoint = [Code];

    /// <summary>
    /// Combinaciones validas que anuncia el discovery por paridad con el servidor real, que las
    /// declara todas en response_types_supported. Anunciarlas evita que un cliente real falle al
    /// validar la metadata al arrancar, y no implica prometer el flujo: el authorize las rechaza.
    /// </summary>
    public static readonly string[] SupportedCombinations =
    [
        Code,
        Token,
        IdToken,
        $"{IdToken} {Token}",
        $"{Code} {IdToken}",
        $"{Code} {Token}",
        $"{Code} {IdToken} {Token}"
    ];
}