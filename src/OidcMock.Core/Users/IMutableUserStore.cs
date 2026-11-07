namespace OidcMock.Core.Users;

/// <summary>
/// Store de usuarios con operaciones de escritura, para el endpoint de administración.
/// </summary>
public interface IMutableUserStore : IUserStore
{
    void Add(User user);
    void Update(User user);
    void Delete(string subject);
}