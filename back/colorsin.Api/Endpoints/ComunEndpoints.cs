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
                bool? incluirInactivos,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await usuarios.ListarAsync(
                contexto.ResolverFiltroSucursal(sucursalId),
                incluirInactivos ?? false,
                cancellationToken)))
            .WithName("ComunUsuarios")
            .WithSummary("Usuarios, para asignaciones")
            .WithDescription(
                "Un gerente ve solo el equipo de su sede; el administrador ve la red, o una sede " +
                "si la pide. El Operador no accede: la lista con correos y roles del personal es " +
                "informacion de gestion. " +
                "Por omision NO trae los perfiles deshabilitados, que es lo que quiere cualquier " +
                "desplegable de asignacion: no se le asigna trabajo a quien no puede entrar. " +
                "`incluirInactivos=true` los anade, para la pantalla que los administra. " +
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

        // ---------------------------------------------------------------------
        // Habilitar y deshabilitar perfiles
        //
        // MISMA PUERTA Y MISMA REGLA QUE EL ALTA. La politica de supervision
        // deja pasar al administrador y al gerente; lo que cada uno puede
        // apagar -y de que sede- lo decide ReglasGestionUsuario dentro del
        // servicio. Es deliberado que la jerarquia sea la misma que la de
        // creacion: quien puede dar de alta a alguien es quien responde por esa
        // persona, asi que es quien tiene que poder cerrarle la cuenta.
        // ---------------------------------------------------------------------
        grupo.MapDelete("/usuarios/{id:int}", async (
                int id,
                IUsuarioService usuarios,
                IUsuarioContexto contexto,
                ClaimsPrincipal quien,
                CancellationToken cancellationToken) =>
            await CambiarEstadoUsuarioAsync(
                id, activo: false, usuarios, contexto, quien, cancellationToken))
            .WithName("ComunDeshabilitarUsuario")
            .WithSummary("Deshabilita un perfil. Administracion y gerencia.")
            .WithDescription(
                "NO BORRA NADA, aunque el verbo sea DELETE: el usuario sigue en la base con toda " +
                "su historia -sus ventas, sus movimientos, su rastro en la bitacora- porque esas " +
                "filas no pueden quedarse sin responsable. Lo que cambia es que deja de poder " +
                "iniciar sesion; el login responde 401 aunque la contrasena sea correcta. " +
                "JERARQUIA: el Administrador General deshabilita gerentes y operadores de " +
                "cualquier sede; un gerente solo operadores de SU sede. NADIE deshabilita a un " +
                "Administrador General por esta via, y NADIE se deshabilita a si mismo: las dos " +
                "cosas responden 403. " +
                "OJO CON LAS SESIONES ABIERTAS: el token ya emitido sigue valiendo hasta que " +
                "caduque, porque el contexto de usuario se lee del token y no de la base.")
            .Produces<ResultadoUsuario>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(PoliticasAutorizacion.Supervision);

        grupo.MapPost("/usuarios/{id:int}/habilitar", async (
                int id,
                IUsuarioService usuarios,
                IUsuarioContexto contexto,
                ClaimsPrincipal quien,
                CancellationToken cancellationToken) =>
            await CambiarEstadoUsuarioAsync(
                id, activo: true, usuarios, contexto, quien, cancellationToken))
            .WithName("ComunHabilitarUsuario")
            .WithSummary("Devuelve el acceso a un perfil deshabilitado.")
            .WithDescription(
                "La contraparte del anterior, con la MISMA jerarquia: quien puede apagar una " +
                "cuenta tiene que poder volver a encenderla, o cada baja por error acabaria " +
                "arreglandose en la base de datos. La contrasena no cambia: es la que tenia.")
            .Produces<ResultadoUsuario>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(PoliticasAutorizacion.Supervision);

        return rutas;
    }

    /// <summary>
    /// El cuerpo compartido de habilitar y deshabilitar.
    ///
    /// Son la misma operacion con la bandera al reves, y van juntas para que no
    /// puedan divergir: dos copias de esta lectura del token es como una de las
    /// dos acaba sin la comprobacion de rol.
    /// </summary>
    private static async Task<IResult> CambiarEstadoUsuarioAsync(
        int id,
        bool activo,
        IUsuarioService usuarios,
        IUsuarioContexto contexto,
        ClaimsPrincipal quien,
        CancellationToken cancellationToken)
    {
        var rol = RolDelDominio(quien.FindFirst(ClaimTypes.Role)?.Value);

        if (rol is null)
        {
            return RespuestasHttp.Fallo(
                StatusCodes.Status403Forbidden,
                "Sesion invalida",
                "Tu token no declara un rol reconocido. Vuelve a iniciar sesion.");
        }

        var actor = new CreadorUsuario(
            contexto.UsuarioIdRequerido(), rol.Value, contexto.SucursalId);

        var resultado = await usuarios.CambiarEstadoAsync(id, activo, actor, cancellationToken);

        return resultado.Exito
            ? Results.Ok(resultado)
            : RespuestasHttp.Fallo(
                CodigoDe(resultado.Error),
                activo ? "Habilitacion rechazada" : "Baja rechazada",
                resultado.Mensaje);
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

        ErrorUsuario.SucursalNoEncontrada
            or ErrorUsuario.NoEncontrado
            => StatusCodes.Status404NotFound,

        // Existe, pero el estado actual de los datos no admite la operacion.
        ErrorUsuario.EmailDuplicado
            or ErrorUsuario.SinCambio
            => StatusCodes.Status409Conflict,

        _ => StatusCodes.Status400BadRequest
    };
}
