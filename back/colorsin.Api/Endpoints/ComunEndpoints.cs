using Colorsin.Api.Auth;
using Colorsin.Application.Comun.Auth;
using Colorsin.Application.Comun.Services;

namespace Colorsin.Api.Endpoints;

/// <summary>
/// Catalogos transversales: sedes, unidades de medida y usuarios.
///
/// SON LOS DESPLEGABLES DE LOS FORMULARIOS. Sin ellos no se puede armar una
/// linea de compra, venta o traslado: hay que elegir una unidad, y el `unidadId`
/// no se puede adivinar desde el frontend.
///
/// SOLO LECTURA. No hay forma de crear ni editar sedes, usuarios ni unidades
/// desde la API: esas tres tablas se pueblan con los scripts de infra. Anadir
/// escritura -sobre todo de usuarios- es una decision aparte, con su propia
/// discusion sobre quien puede crear cuentas y con que rol.
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

        return rutas;
    }
}
