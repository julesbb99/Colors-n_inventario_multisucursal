using Colorsin.Api.Auth;
using Colorsin.Application.Comun.Auth;
using Colorsin.Application.Inventario.DTOs;
using Colorsin.Application.Inventario.Services;

namespace Colorsin.Api.Endpoints;

/// <summary>
/// Endpoints de inventario: existencias, lotes, libro mayor y movimientos.
///
/// AISLAMIENTO POR SEDE. Las consultas pasan el <c>sucursalId</c> por
/// <see cref="IUsuarioContexto.ResolverFiltroSucursal"/> y usan lo que ese
/// metodo devuelve. El registro de un movimiento, que si nombra una sede
/// concreta, pasa por <see cref="IUsuarioContexto.ExigirAccesoASucursal"/>.
///
/// A diferencia del tablero, donde la regla vive dentro del servicio, aqui va en
/// el endpoint: <c>IInventarioService</c> no depende del contexto del usuario y
/// atarlo lo dejaria inservible fuera de una peticion HTTP. La contrapartida es
/// que un endpoint nuevo tiene que acordarse, y por eso todos los de este
/// archivo empiezan igual.
/// </summary>
public static class InventarioEndpoints
{
    /// <summary>Registra los grupos <c>/api/inventario</c> y <c>/api/productos</c>.</summary>
    public static IEndpointRouteBuilder MapInventarioEndpoints(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/inventario")
            .WithTags("Inventario")
            .RequireAuthorization();

        // ---------------------------------------------------------------------
        // Catalogo de productos
        //
        // Cuelga de /api/productos y no de /api/inventario porque no es una
        // consulta de stock: es el catalogo con el que se arma cualquier linea
        // de compra, venta o traslado. El stock de un producto en una sede si
        // vive en /api/inventario/existencias.
        // ---------------------------------------------------------------------
        rutas.MapGet("/api/productos", async (
                IInventarioService inventario,
                string? categoria,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await inventario.ObtenerProductosAsync(categoria, cancellationToken)))
            .WithTags("Productos")
            .WithName("ProductosListar")
            .WithSummary("Catalogo de productos con su unidad base")
            .WithDescription(
                "De toda la red, sin filtrar por sede: que una sede no tenga saldo de un producto " +
                "no significa que no lo maneje, y esconderselo le impediria pedirlo o comprarlo, " +
                "que es justo lo que hace falta cuando no hay. " +
                "`categoria` filtra por coincidencia exacta; omitida trae el catalogo completo. " +
                "OJO: el esquema no tiene marca de activo o inactivo, asi que salen todos.")
            .RequireAuthorization();

        // ---------------------------------------------------------------------
        // Consultas
        // ---------------------------------------------------------------------
        grupo.MapGet("/existencias", async (
                IInventarioService inventario,
                IUsuarioContexto contexto,
                int? sucursalId,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await inventario.ObtenerExistenciasAsync(
                contexto.ResolverFiltroSucursal(sucursalId), cancellationToken)))
            .WithName("InventarioExistencias")
            .WithSummary("Saldos por sede y producto")
            .WithDescription(
                "Un gerente u operador ve solo su sede, omita o no el parametro. El administrador " +
                "ve la red entera si no lo manda.");

        grupo.MapGet("/alertas", async (
                IInventarioService inventario,
                IUsuarioContexto contexto,
                int? sucursalId,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await inventario.ObtenerAlertasStockBajoAsync(
                contexto.ResolverFiltroSucursal(sucursalId), cancellationToken)))
            .WithName("InventarioAlertas")
            .WithSummary("Productos por debajo del minimo")
            .WithDescription("Criterio: cantidad_base <= stock_minimo. Los mas criticos primero.");

        grupo.MapGet("/movimientos", async (
                IInventarioService inventario,
                IUsuarioContexto contexto,
                int? sucursalId,
                int? productoId,
                int? limite,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await inventario.ObtenerMovimientosAsync(
                contexto.ResolverFiltroSucursal(sucursalId),
                productoId,
                limite ?? 100,
                cancellationToken)))
            .WithName("InventarioMovimientos")
            .WithSummary("Libro mayor, del mas reciente al mas antiguo")
            .WithDescription("`limite` se acota entre 1 y 1000.");

        grupo.MapGet("/lotes", async (
                IInventarioService inventario,
                IUsuarioContexto contexto,
                int sucursalId,
                int productoId,
                CancellationToken cancellationToken) =>
            {
                // Aqui la sede es obligatoria -un lote esta en una bodega
                // concreta- asi que se exige acceso en vez de acotar el filtro.
                contexto.ExigirAccesoASucursal(sucursalId);

                return TypedResults.Ok(await inventario.ObtenerLotesPorVencimientoAsync(
                    sucursalId, productoId, cancellationToken));
            })
            .WithName("InventarioLotes")
            .WithSummary("Lotes disponibles en orden FEFO")
            .WithDescription(
                "El primero de la lista es el que se debe despachar: vence antes. Los que no " +
                "caducan van al final. Solo trae lotes con saldo mayor que cero.");

        // ---------------------------------------------------------------------
        // Escritura
        // ---------------------------------------------------------------------
        grupo.MapPost("/movimientos", async (
                RegistrarMovimientoDto peticion,
                IInventarioService inventario,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                contexto.ExigirAccesoASucursal(peticion.SucursalId);

                var resultado = await inventario.RegistrarMovimientoAsync(
                    peticion, contexto.UsuarioIdRequerido(), cancellationToken);

                return resultado.Exito
                    ? Results.Ok(resultado)
                    : RespuestasHttp.Fallo(
                        CodigoDe(resultado.Error), "Movimiento rechazado", resultado.Mensaje);
            })
            .WithName("InventarioRegistrarMovimiento")
            .WithSummary("Registra una entrada o salida de stock. Solo supervision.")
            .WithDescription(
                "La cantidad va en la unidad que tenga a mano el operario; el servicio convierte " +
                "a la unidad base del producto. El responsable NO se manda en el cuerpo: sale del " +
                "token. Un retiro que deje el saldo en negativo se rechaza con 409. " +
                "Restringido a Administrador General y Gerente de Sucursal.")
            .Produces<ResultadoMovimiento>()
            // Supervision, y esta no venia en la lista que se pidio: es una
            // inferencia, explicada aqui para que se pueda discutir.
            //
            // Lo que entra por compras, sale por ventas o se mueve por traslados
            // tiene su propio flujo y su propio documento detras. Un movimiento
            // registrado a mano es lo que queda: un ajuste, una merma, una
            // devolucion. Es decir, la unica forma de cambiar el stock SIN un
            // documento que lo respalde, y por eso mismo la via por la que se
            // tapa un faltante.
            //
            // Si en la operacion real el operador es quien reporta la merma
            // -que es razonable, es quien ve el envase roto- esto hay que
            // abrirlo; pero conviene que sea una decision tomada, no un descuido.
            .RequireAuthorization(PoliticasAutorizacion.Supervision);

        return rutas;
    }

    private static int CodigoDe(ErrorMovimiento error) => error switch
    {
        ErrorMovimiento.ProductoNoEncontrado
            or ErrorMovimiento.UnidadNoEncontrada
            or ErrorMovimiento.LoteNoEncontrado
            or ErrorMovimiento.SaldoNoEncontrado
            => StatusCodes.Status404NotFound,

        ErrorMovimiento.StockInsuficiente
            or ErrorMovimiento.StockLoteInsuficiente
            => StatusCodes.Status409Conflict,

        _ => StatusCodes.Status400BadRequest
    };
}
