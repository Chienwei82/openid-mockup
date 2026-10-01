namespace OidcMock.Core.Discovery;

/// <summary>
/// Valores de capacidad que el mock anuncia de forma fija en el discovery.
/// </summary>
public static class DiscoveryCapabilities
{
    public static readonly string[] SigningAlgorithms = ["RS256"];

    public static readonly string[] SubjectTypes = ["public"];

    public static readonly string[] BackchannelTokenDeliveryModes = ["poll"];
}
