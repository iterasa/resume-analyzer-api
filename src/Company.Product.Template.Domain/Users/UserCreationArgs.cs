namespace Company.Product.Template.Domain.Users;

/// <summary>
/// Parámetros necesarios para crear un usuario (Parameter Object).
/// Agrupa los datos de entrada del factory method y escala sin romper la firma.
/// </summary>
internal sealed record UserCreationArgs(
    Guid Id,
    string Username,
    string Email,
    Guid ActorId);