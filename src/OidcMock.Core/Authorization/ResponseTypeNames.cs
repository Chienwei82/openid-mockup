namespace OidcMock.Core.Authorization;

/// <summary>
/// Valores de response_type que admite el endpoint de autorizacion del mock.
/// </summary>
public static class ResponseTypeNames
{
    public const string Code = "code";
    public const string Token = "token";
    public const string IdToken = "id_token";

    public static readonly string[] Supported = [Code, Token, IdToken];

    /// <summary>
    /// Los response types validos son combinaciones unicas de code, id_token y token separadas por
    /// espacio, en ese orden, tal como los declara el discovery del servidor real.
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