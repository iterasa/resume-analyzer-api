using System.ComponentModel.DataAnnotations;

namespace Company.Product.Template.Api.Contracts;

/// <summary>Cuerpo HTTP para registrar un usuario.</summary>
/// <param name="Username">Nombre de usuario.</param>
/// <param name="Email">Correo de contacto.</param>
public sealed record CreateUserRequest([Required] string Username, [Required][EmailAddress] string Email);