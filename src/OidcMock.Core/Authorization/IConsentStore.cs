namespace OidcMock.Core.Authorization;

/// <summary>
/// Consentimientos recordados: los scopes que un usuario ya aprobo para un cliente. La memoria
/// hace que el flujo repetido no vuelva a preguntar, pero solo cubre lo aprobado: un scope nuevo
/// sigue exigiendo la pantalla.
/// </summary>
public interface IConsentStore
{
    /// <summary>Recuerda la aprobacion, ampliando el conjunto de scopes ya aprobados.</summary>
    void Remember(string clientId, string userName, IReadOnlyList<string> scopes);

    /// <summary>Si lo pedido esta cubierto por lo aprobado para ese cliente y usuario.</summary>
    bool IsGranted(string clientId, string userName, IReadOnlyList<string> scopes);
}