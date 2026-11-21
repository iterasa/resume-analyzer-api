using Asp.Versioning;
using Company.Product.Template.Api.Contracts;
using Company.Product.Template.Api.Extensions;
using Company.Product.Template.Application.Features.Users.Commands.CreateUser;
using Company.Product.Template.Application.Features.Users.Commands.SetUserStatus;
using Company.Product.Template.Application.Features.Users.Queries.GetUserById;
using Company.Product.Template.Application.Features.Users.Queries.GetUsers;
using Company.Product.Template.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Company.Product.Template.Api.Controllers;

/// <summary>
/// Administración de usuarios: alta, consulta, baja y reactivación.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/users")]
public sealed class UsersController(
    ICreateUserHandler createUserHandler,
    IGetUsersHandler getUsersHandler,
    IGetUserByIdHandler getUserByIdHandler,
    ISetUserStatusHandler setUserStatusHandler) : ControllerBase {

    /// <summary>Lista los usuarios, paginados.</summary>
    /// <response code="200">Página obtenida.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) {
        var result = await getUsersHandler.Handle(PageQuery.Create(page, pageSize), cancellationToken);
        return result.Match(Ok, this.ToProblem);
    }

    /// <summary>Detalle de un usuario.</summary>
    /// <response code="200">Usuario encontrado.</response>
    /// <response code="404">El usuario no existe.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GetUserByIdResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) {
        var result = await getUserByIdHandler.Handle(new GetUserByIdQuery(id), cancellationToken);
        return result.Match(Ok, this.ToProblem);
    }

    /// <summary>Registra un usuario.</summary>
    /// <response code="201">Usuario registrado.</response>
    /// <response code="400">Los datos no son válidos.</response>
    /// <response code="409">Ya existe un usuario que entra en conflicto.</response>
    [HttpPost]
    [ProducesResponseType(typeof(CreateUserResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken) {
        var result = await createUserHandler.Handle(new CreateUserCommand(request.Username, request.Email), cancellationToken);
        return result.Match(created => CreatedAtAction(nameof(GetById), new { id = created.Id }, created), this.ToProblem);
    }

    /// <summary>Da de baja lógica al usuario.</summary>
    /// <response code="204">Usuario deshabilitado.</response>
    /// <response code="404">El usuario no existe.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken) {
        var result = await setUserStatusHandler.Handle(new SetUserStatusCommand(id, IsEnabled: false), cancellationToken);
        return result.Match(NoContent, this.ToProblem);
    }

    /// <summary>Reactiva a un usuario dado de baja.</summary>
    /// <response code="204">Usuario habilitado.</response>
    /// <response code="404">El usuario no existe.</response>
    [HttpPost("{id:guid}/activation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken) {
        var result = await setUserStatusHandler.Handle(new SetUserStatusCommand(id, IsEnabled: true), cancellationToken);
        return result.Match(NoContent, this.ToProblem);
    }

}