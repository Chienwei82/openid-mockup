using System.Text.Json;
using OidcMock.Core.Configuration;

namespace OidcMock.ClientCompatibilityTests.Infrastructure;

/// <summary>
/// Configuracion del mock para las pruebas de compatibilidad. El <c>clients.json</c> se escribe con
/// las URLs reales de los hosts de prueba, que se reservan antes de arrancar nada: por eso el mock
/// no puede usar el <c>config/</c> del repositorio, que tiene los puertos de los ejemplos.
/// Los <c>users.json</c> y <c>scopes.json</c> si se copian del repositorio, para no duplicar datos.
/// </summary>
public sealed class CompatibilityConfig : IDisposable
{
    private readonly string _path;

    public CompatibilityConfig(string clientBaseAddress, string clientId, string clientSecret)
    {
        _path = Path.Combine(Path.GetTempPath(), $"oidc-mock-compat-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_path);

        CopyRepositoryFile(ConfigurationFiles.Users);
        CopyRepositoryFile(ConfigurationFiles.Scopes);
        WriteFile(ConfigurationFiles.Clients, ClientsFile(clientBaseAddress, clientId, clientSecret));
    }

    /// <summary>
    /// Cliente de maquina a maquina, sin usuario: es el que usa el grant client_credentials y el que
    /// valida la API protegida con JwtBearer.
    /// </summary>
    public const string ServiceClientId = "backend-service";

    /// <summary>Secreto del cliente de maquina a maquina.</summary>
    public const string ServiceClientSecret = "super-secret-backend";

    /// <summary>
    /// Directorio que se le pasa al mock con <c>OidcMock:ConfigDirectory</c>. El nombre lleva el
    /// prefijo del tipo a proposito: un property llamado <c>Path</c> o <c>Directory</c> sombrearia
    /// <see cref="System.IO.Path"/> o <see cref="System.IO.Directory"/> dentro de la clase.
    /// </summary>
    public string ConfigDirectory => _path;

    /// <summary>
    /// Los JSON de ejemplo se copian tal cual: los tests no mutan usuarios ni scopes, y asi el
    /// cliente de prueba se valida contra los mismos datos que usaria un desarrollador.
    /// </summary>
    private void CopyRepositoryFile(string fileName) =>
        WriteFile(fileName, File.ReadAllText(RepositoryLayout.ConfigFile(fileName)));

    private void WriteFile(string fileName, string content) =>
        File.WriteAllText(Path.Combine(_path, fileName), content);

    private static string ClientsFile(string clientBaseAddress, string clientId, string clientSecret) =>
        $$"""
          {
            "clients": [
              {
                "client_id": {{Quote(clientId)}},
                "client_secret": {{Quote(clientSecret)}},
                "redirect_uris": [ {{Quote($"{clientBaseAddress}/signin-oidc")}} ],
                "post_logout_redirect_uris": [ {{Quote($"{clientBaseAddress}/signout-callback-oidc")}} ],
                "allowed_grant_types": [ "authorization_code", "refresh_token" ],
                "allowed_scopes": [ "openid", "profile", "email", "offline_access" ],
                "require_pkce": true,
                "require_client_secret": true,
                "token_lifetimes": {
                  "access_token": "00:30:00",
                  "id_token": "00:30:00",
                  "refresh_token": "08:00:00",
                  "authorization_code": "00:05:00"
                }
              },
              {
                "client_id": {{Quote(ServiceClientId)}},
                "client_secret": {{Quote(ServiceClientSecret)}},
                "redirect_uris": [],
                "post_logout_redirect_uris": [],
                "allowed_grant_types": [ "client_credentials" ],
                "allowed_scopes": [ "openid", "email" ],
                "require_pkce": false,
                "require_client_secret": true,
                "token_lifetimes": {
                  "access_token": "00:30:00",
                  "id_token": "00:30:00",
                  "refresh_token": "08:00:00",
                  "authorization_code": "00:05:00"
                }
              }
            ]
          }
          """;

    private static string Quote(string value) => JsonSerializer.Serialize(value);

    public void Dispose()
    {
        if (Directory.Exists(_path))
        {
            Directory.Delete(_path, recursive: true);
        }
    }
}