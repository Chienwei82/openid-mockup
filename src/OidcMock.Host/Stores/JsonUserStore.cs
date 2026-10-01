using OidcMock.Core.Configuration;
using OidcMock.Core.Users;
using OidcMock.Host.Stores.Configuration;

namespace OidcMock.Host.Stores;

/// <summary>
/// Store de usuarios respaldado por users.json.
/// </summary>
public sealed class JsonUserStore : IUserStore
{
    private readonly JsonFileLoader<UserFile> _loader;

    public JsonUserStore(string configDirectory)
        : this(configDirectory, reloadOnChange: true)
    {
    }

    public JsonUserStore(string configDirectory, bool reloadOnChange)
    {
        _loader = new JsonFileLoader<UserFile>(configDirectory, ConfigurationFiles.Users, reloadOnChange);
    }

    public User? FindByUserName(string userName) =>
        Users().FirstOrDefault(user => string.Equals(user.UserName, userName, StringComparison.Ordinal));

    public User? FindBySubject(string subject) =>
        Users().FirstOrDefault(user => string.Equals(user.Subject, subject, StringComparison.Ordinal));

    public IReadOnlyList<User> List() => Users();

    private IReadOnlyList<User> Users() => UserMapper.ToDomain(_loader.Load());
}
