namespace OidcMock.Core.Clients;

/// <summary>
/// Comparacion del secreto compartida por los dos metodos: en tiempo constante para no filtrar el
/// secreto por temporizacion, aunque el mock sea de desarrollo local.
/// </summary>
public static class ClientSecrets
{
    public static bool Match(Client client, string? presentedSecret) =>
        !string.IsNullOrEmpty(client.ClientSecret) &&
        !string.IsNullOrEmpty(presentedSecret) &&
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(client.ClientSecret),
            System.Text.Encoding.UTF8.GetBytes(presentedSecret));
}