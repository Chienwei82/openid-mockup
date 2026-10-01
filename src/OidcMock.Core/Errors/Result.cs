namespace OidcMock.Core.Errors;

/// <summary>
/// Resultado de una operacion de negocio: o un valor, o un error de protocolo con su codigo HTTP.
/// Evita lanzar excepciones para los errores previstos del flujo OAuth.
/// </summary>
public readonly record struct Result<T>
{
    private Result(bool succeeded, T? value, ProtocolError? error)
    {
        Succeeded = succeeded;
        Value = value;
        Error = error;
    }

    public bool Succeeded { get; }

    public bool Failed => !Succeeded;

    public T? Value { get; }

    public ProtocolError? Error { get; }

    // CA1000: las fabricas viven en el propio Result<T> para poder escribirse Result<TokenResponse>.Ok(...)
    // y Result<AuthorizationCode>.Fail(...). Sin ellas habria que repetir el constructor privado en
    // cada caso de uso, que es justo lo que el patron evita.
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1000:Do not declare static members on generic types",
        Justification = "Fabricas del patron Result, propias del tipo de resultado.")]
    public static Result<T> Ok(T value) => new(true, value, null);

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1000:Do not declare static members on generic types",
        Justification = "Fabricas del patron Result, propias del tipo de resultado.")]
    public static Result<T> Fail(ProtocolError error) => new(false, default, error);

    /// <summary>
    /// Valor si la operacion tuvo exito; si no, lanza el error. Para cuando el caller ya valido.
    /// </summary>
    public T ValueOrThrow() =>
        Succeeded
            ? Value!
            : throw new InvalidOperationException($"La operacion fallo con el error de protocolo '{Error?.Code}'.");

    public Result<TNext> Map<TNext>(Func<T, TNext> map) =>
        Succeeded ? Result<TNext>.Ok(map(Value!)) : Result<TNext>.Fail(Error!);
}