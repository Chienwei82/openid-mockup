namespace OidcMock.Core.Users;

/// <summary>
/// Repositorio de solo lectura de usuarios del mock.
/// </summary>
public interface IUserStore
{
    User? FindByUserName(string userName);

    User? FindBySubject(string subject);

    IReadOnlyList<User> List();
}
