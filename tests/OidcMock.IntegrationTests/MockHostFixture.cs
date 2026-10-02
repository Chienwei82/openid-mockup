namespace OidcMock.IntegrationTests;

/// <summary>
/// Fixture de ensamblado que libera los hosts que los tests dejaron sin liberar. Vive en el
/// ensamblado para que se ejecute una sola vez al terminar, tambien si alguna prueba fallo antes de
/// llegar a su <c>Dispose</c>.
/// </summary>
public sealed class MockHostFixture : IDisposable
{
    public void Dispose() => MockHost.DisposeCreatedHosts();
}
