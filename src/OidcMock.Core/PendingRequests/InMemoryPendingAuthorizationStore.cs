using System.Collections.Concurrent;
using OidcMock.Core.Errors;

namespace OidcMock.Core.PendingRequests;

/// <summary>
/// Store en memoria de peticiones pendientes, compartido por PAR, device code y CIBA.
/// </summary>
public sealed class InMemoryPendingAuthorizationStore : IPendingAuthorizationStore
{
    private const string UnknownRequest = "La peticion no existe, ya fue canjeada o caduco.";

    private readonly ConcurrentDictionary<string, PendingAuthorizationRequest> _requests = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    public InMemoryPendingAuthorizationStore(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _timeProvider = timeProvider;
    }

    public PendingAuthorizationRequest Issue(PendingAuthorizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Expire();
        _requests[request.Handle] = request;

        return request;
    }

    public Result<PendingAuthorizationRequest> Find(string handle) =>
        TryGet(handle, out var request)
            ? Result<PendingAuthorizationRequest>.Ok(request)
            : Result<PendingAuthorizationRequest>.Fail(ProtocolErrors.InvalidRequest(UnknownRequest));

    public Result<PendingAuthorizationRequest> Redeem(string handle)
    {
        if (!TryGet(handle, out var request))
        {
            return Result<PendingAuthorizationRequest>.Fail(ProtocolErrors.InvalidGrant(UnknownRequest));
        }

        if (!request.IsAuthorizedAt(_timeProvider.GetUtcNow()))
        {
            return Result<PendingAuthorizationRequest>.Fail(ProtocolErrors.InvalidGrant(UnknownRequest));
        }

        _requests.TryRemove(handle, out _);

        return Result<PendingAuthorizationRequest>.Ok(request);
    }

    public Result<PendingAuthorizationRequest> Approve(
        string handle,
        string userName,
        string subject,
        DateTimeOffset authenticatedAt)
    {
        if (!TryGet(handle, out var request))
        {
            return Result<PendingAuthorizationRequest>.Fail(ProtocolErrors.InvalidRequest(UnknownRequest));
        }

        var approved = request with
        {
            UserName = userName,
            Subject = subject,
            AuthenticatedAt = authenticatedAt
        };

        _requests[handle] = approved;

        return Result<PendingAuthorizationRequest>.Ok(approved);
    }

    /// <summary>
    /// Marca la peticion como denegada. Se limpia la aprobacion previa para que el sondeo ya no pueda
    /// canjearla y responda access_denied.
    /// </summary>
    public Result<PendingAuthorizationRequest> Deny(string handle)
    {
        if (!TryGet(handle, out var request))
        {
            return Result<PendingAuthorizationRequest>.Fail(ProtocolErrors.InvalidRequest(UnknownRequest));
        }

        var denied = request with { Denied = true, Subject = null, UserName = null, AuthenticatedAt = null };
        _requests[handle] = denied;

        return Result<PendingAuthorizationRequest>.Ok(denied);
    }

    public void Expire()
    {
        var now = _timeProvider.GetUtcNow();

        foreach (var expired in _requests
            .Where(entry => entry.Value.IsExpiredAt(now))
            .Select(entry => entry.Key))
        {
            _requests.TryRemove(expired, out _);
        }
    }

    public IReadOnlyList<PendingAuthorizationRequest> List()
    {
        Expire();

        return [.. _requests.Values];
    }

    private bool TryGet(string handle, out PendingAuthorizationRequest request)
    {
        if (!string.IsNullOrEmpty(handle) && _requests.TryGetValue(handle, out var found))
        {
            if (!found.IsExpiredAt(_timeProvider.GetUtcNow()))
            {
                request = found;

                return true;
            }

            _requests.TryRemove(handle, out _);
        }

        request = null!;

        return false;
    }
}