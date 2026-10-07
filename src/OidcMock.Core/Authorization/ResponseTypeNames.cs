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

    /// <summary>Flujo hibrido: el code viaja en el fragmento junto al id_token y el session_state.</summary>
    public static readonly string CodeIdToken = $"{Code} {IdToken}";

    /// <summary>
    /// Combinaciones que el endpoint de autorizacion del mock acepta y responde: el code del flujo
    /// de autorizacion y el flujo hibrido <c>code id_token</c>, que es el que pide el cliente real
    /// contra el que se valida el mock. Deliberadamente no incluye las combinaciones que declaran
    /// un access token en el fragmento: aceptarlas sin emitirlo devolveria al cliente una respuesta
    /// donde pidio un token y no llega, sin ningun error que lo explicara, que es peor que un
    /// <c>unsupported_response_type</c> honesto.
    /// </summary>
    public static readonly string[] EmittedByAuthorizationEndpoint = [Code, CodeIdToken];

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