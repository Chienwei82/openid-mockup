using System.Collections.Concurrent;

namespace OidcMock.Core.Users;

/// <summary>
/// Store que consulta primero las identidades recordadas desde la pantalla del mock y cae al store
/// configurado. Asi el token endpoint y userinfo devuelven los mismos claims que el usuario edito,
/// sin tocar el resto del pipeline, que sigue hablando de IUserStore.
/// </summary>
public sealed class OverlayUserStore : IUserStore, IUserOverlay
{
    private readonly ConcurrentDictionary<string, User> _remembered = new(StringComparer.Ordinal);

    private readonly IUserStore _inner;

    public OverlayUserStore(IUserStore inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    public void Remember(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        _remembered[user.Subject] = user;
    }

    public User? FindByUserName(string userName) =>
        _remembered.Values.FirstOrDefault(user => string.Equals(user.UserName, userName, StringComparison.Ordinal))
        ?? _inner.FindByUserName(userName);

    public User? FindBySubject(string subject) =>
        _remembered.TryGetValue(subject, out var remembered)
            ? remembered
            : _inner.FindBySubject(subject);

    public IReadOnlyList<User> List()
    {
        var remembered = _remembered.Values.Where(user => _inner.FindBySubject(user.Subject) is null);
        return [.. _inner.List(), .. remembered];
    }
}
