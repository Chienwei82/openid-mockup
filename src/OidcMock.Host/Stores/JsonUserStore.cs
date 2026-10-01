using OidcMock.Core.Configuration;
using OidcMock.Core.Users;
using OidcMock.Host.Stores.Configuration;

namespace OidcMock.Host.Stores;

/// <summary>
/// Store de usuarios respaldado por users.json.
/// </summary>
public sealed class JsonUserStore : IUserStore
{
    private readonly JsonFileLoader<UserFile, IReadOnlyList<User>> _loader;

    public JsonUserStore(string configDirectory)
        : this(configDirectory, reloadOnChange: true)
    {
    }

    public JsonUserStore(string configDirectory, bool reloadOnChange)
    {
        _loader = new JsonFileLoader<UserFile, IReadOnlyList<User>>(
            configDirectory,
            ConfigurationFiles.Users,
            reloadOnChange,
            UserMapper.ToDomain);
    }

    public User? FindByUserName(string userName) =>
        _loader.Load().FirstOrDefault(user => string.Equals(user.UserName, userName, StringComparison.Ordinal));

    public User? FindBySubject(string subject) =>
        _loader.Load().FirstOrDefault(user => string.Equals(user.Subject, subject, StringComparison.Ordinal));

    public IReadOnlyList<User> List() => _loader.Load();
}
