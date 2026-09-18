using System.Security.Claims;
using Colorsin.Application.Comun.Auth;
using Colorsin.Application.Comun.DTOs.Auth;
using Colorsin.Application.Comun.Services;

namespace Colorsin.Api.Endpoints;

/// <summary>
/// Endpoints de sesion: entrar y saber quien eres.
///
/// EL LOGIN ES PUBLICO por necesidad -no se puede exigir un token para pedir el
/// primero- y es, por eso mismo, el endpoint mas expuesto de la API: el unico
/// donde cualquiera puede probar combinaciones sin identificarse.
///
/// TODO FALLO SALE COMO EL MISMO 401, sin decir si el correo existe. Ver la nota
/// de <see cref="AuthService"/>.
/// </summary>
public static class AuthEndpoints
{
    /// <summary>Registra el grupo <c>/api/auth</c>.</summary>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/auth")
            .WithTags("Autenticacion");

        // ---------------------------------------------------------------------
        // Entrar
        // ---------------------------------------------------------------------
        grupo.MapPost("/login", async (
                LoginRequestDto peticion,
                IAuthService auth,
                CancellationToken cancellationToken) =>
            {
                var sesion = await auth.IniciarSesionAsync(peticion, cancellationToken);

                return sesion is null
                    // Sin detalle: el mensaje es igual venga de un correo que no
                    // existe o de una contrasena equivocada. 401 y no 400, aunque
                    // vengan campos vacios, para no separar tampoco por ahi.
                    ? Results.Problem(
                        title: "Credenciales incorrectas",
                        detail: "El correo o la contrasena no son correctos.",
                        statusCode: StatusCodes.Status401Unauthorized)
                    : Results.Ok(sesion);
            })
            .WithName("AuthLogin")
            .WithSummary("Inicia sesion y devuelve el token")
            .WithDescription(
                "Recibe correo y contrasena. Devuelve 200 con el token, su caducidad en UTC y " +
                "el usuario, o 401 sin distinguir el motivo. El token se usa despues como " +
                "cabecera 'Authorization: Bearer <token>'.")
            .Produces<LoginResponseDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            // Explicito aunque hoy nada esta protegido por defecto: si manana se
            // exige token en toda la API, este endpoint no puede quedar dentro,
            // o nadie podria conseguir el primero.
            .AllowAnonymous();

        // ---------------------------------------------------------------------
        // Quien soy
        // ---------------------------------------------------------------------
        grupo.MapGet("/yo", (ClaimsPrincipal quien) => TypedResults.Ok(new
            {
                UsuarioId = quien.LeerUsuarioId(),
                Nombre = quien.Identity?.Name,
                Email = quien.FindFirst(ClaimTypes.Email)?.Value,
                Rol = quien.FindFirst(ClaimTypes.Role)?.Value,
                SucursalId = quien.LeerSucursalId()
            }))
            .WithName("AuthYo")
            .WithSummary("Datos de la sesion en curso")
            .WithDescription(
                "Lee el token y devuelve a quien pertenece. Sirve para que el frontend sepa, " +
                "al cargar, si el token que tiene guardado sigue valiendo: si caduco responde " +
                "401 y toca volver a entrar. No consulta la base: todo sale del propio token, " +
                "asi que refleja los datos de cuando se emitio, no los de ahora.")
            .RequireAuthorization();

        return rutas;
    }
}
