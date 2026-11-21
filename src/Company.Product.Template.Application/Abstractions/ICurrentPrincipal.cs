namespace Company.Product.Template.Application.Abstractions;

/// <summary>
/// Expone información del principal autenticado actual.
/// </summary>
public interface ICurrentPrincipal {

    /// <summary>Identificador del usuario actual, o <see cref="Guid.Empty" /> si es anónimo.</summary>
    Guid UserId { get; }

    /// <summary>Indica si la petición viene autenticada.</summary>
    bool IsAuthenticated { get; }

}