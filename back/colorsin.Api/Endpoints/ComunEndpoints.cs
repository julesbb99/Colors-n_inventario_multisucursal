using System.Security.Claims;
using Colorsin.Api.Auth;
using Colorsin.Application.Comun.Auth;
using Colorsin.Application.Comun.DTOs;
using Colorsin.Application.Comun.Services;
using Colorsin.Domain.Comun;

namespace Colorsin.Api.Endpoints;

/// <summary>
/// Catalogos transversales: sedes, unidades de medida y usuarios.
///
/// SON LOS DESPLEGABLES DE LOS FORMULARIOS. Sin ellos no se puede armar una
/// linea de compra, venta o traslado: hay que elegir una unidad, y el `unidadId`
/// no se puede adivinar desde el frontend.
///
/// SEDES Y UNIDADES SON SOLO LECTURA: esas dos tablas se pueblan con los
/// scripts de infra.
///
/// USUARIOS SI ADMITE ALTA, con una jerarquia estricta: el Administrador General
/// da de alta gerentes y operadores en cualquier sede, y un gerente solo
/// operadores de la suya. La regla completa vive en
/// <see cref="ReglasCreacionUsuario"/>, que es codigo puro y se puede leer de un
/// vistazo.
/// </summary>
public static class ComunEndpoints
{
    /// <summary>Registra el grupo <c>/api/comun</c>.</summary>
    public static IEndpointRouteBuilder MapComunEndpoints(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/comun")
            .WithTags("Comun")
            .RequireAuthorization();

        // ---------------------------------------------------------------------
        // Sedes
        // ---------------------------------------------------------------------
        grupo.MapGet("/sucursales", async (
                ISucursalService sucursales,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await sucursales.ListarAsync(cancellationToken)))
            .WithName("ComunSucursales")
            .WithSummary("Sedes de la red")
            .WithDescription(
                "Devuelve TODAS las sedes, tambien a un gerente o un operador. No es una fuga " +
                "del aislamiento: para pedir un traslado hay que poder nombrar la sede de la que " +
                "se pide, y el nombre de una sede no es un dato reservado. Lo que si esta " +
                "aislado son sus existencias, sus ventas y sus traslados. " +
                "OJO: el esquema no tiene marca de activa o inactiva, asi que salen todas.");

        // ---------------------------------------------------------------------
        // Unidades de medida
        // ---------------------------------------------------------------------
        grupo.MapGet("/unidades-medida", async (
                IUnidadMedidaService unidades,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await unidades.ListarAsync(cancellationToken)))
            .WithName("ComunUnidadesMedida")
            .WithSummary("Unidades de medida del catalogo")
            .WithDescription(
                "Cada una trae su `factorConversionLitros`, que es con lo que el servidor " +
                "convierte a la unidad base del producto. El frontend NO debe convertir por su " +
                "cuenta y mandar el resultado: se manda la cantidad tal como la escribio la " +
                "persona junto con su `unidadId`, y la aritmetica la hace el servidor una sola " +
                "vez. Un factor nulo significa que esa unidad no es de volumen.");

        // ---------------------------------------------------------------------
        // Usuarios
        // ---------------------------------------------------------------------
        grupo.MapGet("/usuarios", async (
                IUsuarioService usuarios,
                IUsuarioContexto contexto,
                int? sucursalId,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await usuarios.ListarAsync(
                contexto.ResolverFiltroSucursal(sucursalId), cancellationToken)))
            .WithName("ComunUsuarios")
            .WithSummary("Usuarios, para asignaciones")
            .WithDescription(
                "Un gerente ve solo el equipo de su sede; el administrador ve la red, o una sede " +
                "si la pide. El Operador no accede: la lista con correos y roles del personal es " +
                "informacion de gestion. " +
                "El DTO nunca lleva el hash de la contrasena, por construccion.")
            // Supervision: esta lista existe para asignar trabajo, que es tarea
            // de quien coordina. Ademas expone correos y roles del personal.
            .RequireAuthorization(PoliticasAutorizacion.Supervision);

        // ---------------------------------------------------------------------
        // Alta de usuarios
        //
        // LA POLITICA DE SUPERVISION ES LA PUERTA, NO LA REGLA. Deja pasar al
        // administrador y al gerente, que son los dos que pueden crear a
        // alguien; pero lo que cada uno puede crear -y en que sede- lo decide
        // ReglasCreacionUsuario dentro del servicio. Si estuviera aqui, un
        // gerente podria dar de alta a otro gerente en otra sede, que es
        // justamente lo que no debe poder hacer.
        // ---------------------------------------------------------------------
        grupo.MapPost("/usuarios", async (
                CrearUsuarioDto peticion,
                IUsuarioService usuarios,
                IUsuarioContexto contexto,
                ClaimsPrincipal quien,
                CancellationToken cancellationToken) =>
            {
                var rol = RolDelDominio(quien.FindFirst(ClaimTypes.Role)?.Value);

                if (rol is null)
                {
                    // Token sin rol reconocible: no es un caso de negocio, es un
                    // token que no deberia existir.
                    return RespuestasHttp.Fallo(
                        StatusCodes.Status403Forbidden,
                        "Sesion invalida",
                        "Tu token no declara un rol reconocido. Vuelve a iniciar sesion.");
                }

                var creador = new CreadorUsuario(
                    contexto.UsuarioIdRequerido(),
                    rol.Value,
                    contexto.SucursalId);

                var resultado = await usuarios.CrearAsync(peticion, creador, cancellationToken);

                return resultado.Exito
                    ? Results.Created($"/api/comun/usuarios/{resultado.Usuario!.Id}", resultado)
                    : RespuestasHttp.Fallo(
                        CodigoDe(resultado.Error), "Alta rechazada", resultado.Mensaje);
            })
            .WithName("ComunCrearUsuario")
            .WithSummary("Da de alta un usuario. Jerarquia estricta por rol y sede.")
            .WithDescription(
                "El Administrador General crea gerentes y operadores en cualquier sede; un " +
                "Gerente de Sucursal solo operadores de SU sede, y recibe 403 en cualquier otro " +
                "caso. NADIE crea un Administrador General por esta via. " +
                "`rol` viaja como numero: 1 = Gerente de Sucursal, 2 = Operador. " +
                "La contrasena va en claro en el cuerpo -por eso este endpoint exige HTTPS fuera " +
                "de desarrollo- y se guarda hasheada con BCrypt; nunca vuelve en la respuesta. " +
                "Minimo 12 caracteres. " +
                "OJO: el sistema todavia no tiene cambio de contrasena, asi que quien crea la " +
                "cuenta conoce la clave inicial y su dueno no puede cambiarla desde la aplicacion.")
            .Produces<ResultadoUsuario>(StatusCodes.Status201Created)
            .RequireAuthorization(PoliticasAutorizacion.Supervision);

        return rutas;
    }

    /// <summary>
    /// El rol del token, traducido al enum del dominio.
    ///
    /// Es el camino INVERSO de <see cref="RolesColorsin.ParaClaim"/>, y por eso
    /// va pegado a el: el claim dice 'AdminGeneral' y el dominio dice
    /// 'AdministradorGeneral'. Devuelve nulo ante cualquier cosa desconocida en
    /// vez de adivinar; un rol mal traducido aqui decidiria permisos.
    /// </summary>
    private static RolUsuario? RolDelDominio(string? claim) => claim switch
    {
        RolesColorsin.AdminGeneral => RolUsuario.AdministradorGeneral,
        RolesColorsin.GerenteSucursal => RolUsuario.GerenteDeSucursal,
        RolesColorsin.Operador => RolUsuario.Operador,
        _ => null
    };

    private static int CodigoDe(ErrorUsuario error) => error switch
    {
        // El rol o la sede no alcanzan. Es el mismo 403 del resto del sistema.
        ErrorUsuario.NoAutorizado => StatusCodes.Status403Forbidden,

        ErrorUsuario.SucursalNoEncontrada => StatusCodes.Status404NotFound,

        // Existe, pero el estado actual de los datos no admite la operacion.
        ErrorUsuario.EmailDuplicado => StatusCodes.Status409Conflict,

        _ => StatusCodes.Status400BadRequest
    };
}
