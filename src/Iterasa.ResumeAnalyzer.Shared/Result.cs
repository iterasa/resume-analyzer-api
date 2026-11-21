using System.Diagnostics.CodeAnalysis;

namespace Iterasa.ResumeAnalyzer.Shared;

/// <summary>
/// Representa el resultado no genérico de una operación.
/// </summary>
/// <remarks>
/// Un fallo esperable —no encontrado, conflicto, validación— viaja como valor, no como
/// excepción. El consumidor lo resuelve con <see cref="Match{TOut}" />, que obliga a
/// tratar los dos caminos, o consultando <see cref="IsSuccess" />, que el compilador
/// entiende: tras comprobarlo, <see cref="Error" /> no admite nulo.
/// </remarks>
public class Result {

    /// <summary>
    /// Inicializa una nueva instancia de la clase <see cref="Result" />.
    /// </summary>
    /// <param name="isSuccess">Indica si la operación fue exitosa.</param>
    /// <param name="error">Error asociado cuando la operación falla.</param>
    protected Result(bool isSuccess, Error? error) {
        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>
    /// Obtiene un valor que indica si la operación fue exitosa.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    /// <summary>
    /// Obtiene un valor que indica si la operación falló.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Obtiene el error asociado al resultado cuando existe.
    /// </summary>
    public Error? Error { get; }

    /// <summary>
    /// Crea un resultado exitoso.
    /// </summary>
    /// <returns>Instancia exitosa de <see cref="Result" />.</returns>
    public static Result Success() => new(true, null);

    /// <summary>
    /// Crea un resultado fallido.
    /// </summary>
    /// <param name="error">Error asociado.</param>
    /// <returns>Instancia fallida de <see cref="Result" />.</returns>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>
    /// Resuelve el resultado por uno de sus dos caminos.
    /// </summary>
    /// <typeparam name="TOut">Tipo al que se traduce el resultado.</typeparam>
    /// <param name="onSuccess">Traducción del éxito.</param>
    /// <param name="onFailure">Traducción del fallo, que recibe el error.</param>
    /// <returns>Lo que devuelva la rama que corresponda.</returns>
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<Error, TOut> onFailure) =>
        IsSuccess ? onSuccess() : onFailure(Error);

}

/// <summary>
/// Representa el resultado genérico de una operación.
/// </summary>
/// <typeparam name="T">Tipo del valor retornado cuando la operación tiene éxito.</typeparam>
public sealed class Result<T> : Result {

    private readonly T? value;

    private Result(T value) : base(true, null) {
        this.value = value;
    }

    private Result(Error error) : base(false, error) {
    }

    /// <summary>
    /// Obtiene el valor del resultado. Solo existe cuando la operación fue exitosa.
    /// </summary>
    /// <exception cref="InvalidOperationException">Cuando se consulta sobre un resultado fallido.</exception>
    public T Value => IsSuccess
        ? value!
        : throw new InvalidOperationException($"The result failed with '{Error.Code}' and carries no value.");

    /// <summary>
    /// Crea un resultado exitoso con valor.
    /// </summary>
    /// <param name="value">Valor exitoso.</param>
    /// <returns>Instancia exitosa de <see cref="Result{T}" />.</returns>
    public static Result<T> Success(T value) => new(value);

    /// <summary>
    /// Crea un resultado fallido.
    /// </summary>
    /// <param name="error">Error asociado.</param>
    /// <returns>Instancia fallida de <see cref="Result{T}" />.</returns>
    public static new Result<T> Failure(Error error) => new(error);

    /// <summary>
    /// Resuelve el resultado por uno de sus dos caminos.
    /// </summary>
    /// <typeparam name="TOut">Tipo al que se traduce el resultado.</typeparam>
    /// <param name="onSuccess">Traducción del éxito, que recibe el valor.</param>
    /// <param name="onFailure">Traducción del fallo, que recibe el error.</param>
    /// <returns>Lo que devuelva la rama que corresponda.</returns>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure) =>
        IsSuccess ? onSuccess(value!) : onFailure(Error);

}