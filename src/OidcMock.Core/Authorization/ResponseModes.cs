namespace OidcMock.Core.Authorization;

/// <summary>
/// Modos de respuesta de la peticion de autorizacion que anuncia el discovery del servidor real.
/// </summary>
public static class ResponseModes
{
    public const string Query = "query";
    public const string Fragment = "fragment";
    public const string FormPost = "form_post";

    public static readonly string[] Supported = [Query, Fragment, FormPost];

    /// <summary>
    /// response_mode por defecto cuando la peticion no lo declara (Multi Response Type Encoding 3):
    /// el codigo viaja en el query string; cualquier respuesta que lleve tokens, en el fragmento.
    /// </summary>
    public static string DefaultFor(string? responseType) =>
        string.Equals(responseType, ResponseTypeNames.Code, StringComparison.Ordinal) ? Query : Fragment;
}

/// <summary>
/// Valores de prompt que admite el endpoint, los mismos que anuncia prompt_values_supported.
/// Viven como constante porque los usan el validador, el discovery y las pantallas del mock.
/// </summary>
public static class PromptValues
{
    public const string None = "none";
    public const string Login = "login";
    public const string Consent = "consent";
    public const string SelectAccount = "select_account";

    public static readonly string[] Supported = [None, Login, Consent, SelectAccount];
}
