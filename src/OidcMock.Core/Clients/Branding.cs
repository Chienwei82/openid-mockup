namespace OidcMock.Core.Clients;

/// <summary>
/// Apariencia del cliente mostrada en las pantallas de login/consentimiento del mock.
/// </summary>
public sealed record Branding(string DisplayName, string? LogoUrl, string? PrimaryColor);
