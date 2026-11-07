using System.Collections.Concurrent;
using OidcMock.Core.Configuration;
using OidcMock.Core.Users;
using OidcMock.Host.Stores;
using OidcMock.Host.Stores.Configuration;

namespace OidcMock.Host.Stores;

/// <summary>
/// Store de usuarios con lectura desde el JSON configurado y escritura en memoria: los cambios
/// hechos vía el endpoint de administracion se perdenden al reiniciar, pero son inmediatos y no
/// exigen restart.
/// </summary>
public sealed class AdminInMemoryUserStore : IMutableUserStore
{
    private readonly JsonFileLoader<UserFile, IReadOnlyList<User>> _loader;
    private readonly ConcurrentDictionary<string, User> _writes = new(StringComparer.Ordinal);

    public AdminInMemoryUserStore(string configDirectory, bool reloadOnChange)
    {
        _loader = new JsonFileLoader<UserFile, IReadOnlyList<User>>(
            configDirectory,
            ConfigurationFiles.Users,
            reloadOnChange,
            UserMapper.ToDomain);
    }

    public User? FindByUserName(string userName) => Find(userName);

    public User? FindBySubject(string subject) => _writes.TryGetValue(subject, out var user) ? user : _loader.Load().FirstOrDefault(u => string.Equals(u.Subject, subject, StringComparison.Ordinal));

    public IReadOnlyList<User> List() => _loader.Load();

    private User? Find(string userName)
    {
        if (string.IsNullOrEmpty(userName))
        {
            return null;
        }
        return _writes.Values.FirstOrDefault(user => string.Equals(user.UserName, userName, StringComparison.Ordinal))
            ?? _loader.Load().FirstOrDefault(u => string.Equals(u.UserName, userName, StringComparison.Ordinal));
    }

    public void Add(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (Find(user.UserName) is not null)
        {
            throw new InvalidOperationException($"El usuario '{user.UserName}' ya existe.");
        }
        _writes[user.Subject] = user;
    }

    public void Update(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (Find(user.UserName) is not null)
        {
            throw new InvalidOperationException($"El usuario '{user.UserName}' no existe.");
        }
        _writes[user.Subject] = user;
    }

    public void Delete(string subject)
    {
        ArgumentException.ThrowIfNullOrEmpty(subject);
        _writes.TryRemove(subject, out _);
    }
}
