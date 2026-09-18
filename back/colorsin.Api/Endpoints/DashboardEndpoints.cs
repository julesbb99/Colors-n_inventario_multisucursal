using Colorsin.Application.Dashboard.Services;

namespace Colorsin.Api.Endpoints;

/// <summary>
/// Endpoints HTTP del tablero. Todos son <c>GET</c> y ninguno cambia nada: es
/// el unico modulo del que eso se puede afirmar entero.
///
/// SON DELIBERADAMENTE DELGADOS. No validan, no acotan, no calculan: reciben la
/// peticion, llaman al servicio y devuelven. Toda la politica -los topes de
/// `top`, el rango invertido que responde vacio, la sede inexistente que
/// responde en ceros- vive en <see cref="IDashboardService"/>, que es donde se
/// puede probar sin levantar un servidor. Si un endpoint empieza a decidir algo,
/// esa decision quedo fuera del alcance de las pruebas.
///
/// LA UNICA EXCEPCION es el rango por defecto de /ventas, y esta abajo explicada:
/// es una comodidad del transporte, no una regla de negocio.
///
/// TypedResults EN VEZ DE Results. La diferencia no es de estilo: con
/// <c>TypedResults.Ok(x)</c> el tipo de respuesta queda en la firma del
/// delegado, y OpenAPI publica el esquema del DTO sin que haya que repetirlo en
/// un <c>.Produces&lt;T&gt;()</c> que se queda viejo en cuanto cambie el DTO.
///
/// EL CancellationToken SE PROPAGA HASTA LA CONSULTA. ASP.NET Core lo cancela
/// cuando el cliente corta la conexion, cosa que en un tablero pasa a menudo:
/// alguien abre la pantalla y navega a otra parte antes de que respondan las
/// agregaciones. Sin propagarlo, esas consultas seguirian ocupando MySQL para
/// nadie.
/// </summary>
public static class DashboardEndpoints
{
    /// <summary>Registra el grupo <c>/api/dashboard</c>.</summary>
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/dashboard")
            .WithTags("Dashboard");

        // ---------------------------------------------------------------------
        // Resumen general
        // ---------------------------------------------------------------------
        grupo.MapGet("/resumen", async (
                IDashboardService dashboard,
                int? sucursalId,
                DateOnly? fechaCorte,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await dashboard.ObtenerResumenGeneralAsync(
                sucursalId, fechaCorte, cancellationToken)))
            .WithName("DashboardResumenGeneral")
            .WithSummary("Totales de la pantalla de inicio")
            .WithDescription(
                "Ventas del dia y del mes, stock total en litros, traslados en transito y " +
                "alertas de reposicion. `sucursalId` omitido trae toda la red. `fechaCorte` " +
                "omitida usa el dia de hoy; darla permite consultar un cierre pasado.");

        // ---------------------------------------------------------------------
        // Ventas
        // ---------------------------------------------------------------------
        grupo.MapGet("/ventas", async (
                IDashboardService dashboard,
                DateOnly? desde,
                DateOnly? hasta,
                int? sucursalId,
                int? top,
                CancellationToken cancellationToken) =>
            {
                // Rango por defecto: el mes en curso, el mismo periodo que
                // reporta "ventas del mes" en el resumen. Asi la pantalla de
                // ventas abre mostrando algo coherente con la de inicio en vez
                // de exigir dos fechas para la primera carga.
                //
                // Esto es comodidad del transporte, no una regla: el servicio
                // sigue exigiendo las dos fechas, que es lo correcto para que el
                // resultado no dependa de un valor implicito.
                var fin = hasta ?? DateOnly.FromDateTime(DateTime.Now);
                var inicio = desde ?? new DateOnly(fin.Year, fin.Month, 1);

                return TypedResults.Ok(await dashboard.ObtenerMetricasVentasAsync(
                    inicio, fin, sucursalId, top ?? 5, cancellationToken));
            })
            .WithName("DashboardMetricasVentas")
            .WithSummary("Ventas de un periodo, con sus rankings")
            .WithDescription(
                "Consolidado, serie diaria, productos mas vendidos y mejores clientes. " +
                "`desde` y `hasta` son inclusivas las dos; omitidas, toman el mes en curso. " +
                "Un rango al reves responde en ceros, no con error. `top` se acota entre 1 y 50.");

        // ---------------------------------------------------------------------
        // Inventario
        // ---------------------------------------------------------------------
        grupo.MapGet("/inventario", async (
                IDashboardService dashboard,
                int? sucursalId,
                int? dias,
                int? topLotes,
                int? topMovimientos,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await dashboard.ObtenerMetricasInventarioAsync(
                sucursalId,
                dias ?? 30,
                topLotes ?? 10,
                topMovimientos ?? 10,
                cancellationToken)))
            .WithName("DashboardMetricasInventario")
            .WithSummary("Stock por sede, vencimientos y ultimos movimientos")
            .WithDescription(
                "`dias` es el horizonte de vencimientos, entre 1 y 365; los lotes YA VENCIDOS " +
                "con saldo entran siempre, sea cual sea el horizonte. Los lotes llegan en orden " +
                "FEFO. `topLotes` y `topMovimientos` se acotan entre 1 y 50.");

        // ---------------------------------------------------------------------
        // Transferencias
        // ---------------------------------------------------------------------
        grupo.MapGet("/transferencias", async (
                IDashboardService dashboard,
                int? sucursalId,
                int? topNovedades,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await dashboard.ObtenerMetricasTransferenciasAsync(
                sucursalId, topNovedades ?? 10, cancellationToken)))
            .WithName("DashboardMetricasTransferencias")
            .WithSummary("Traslados por estado y novedades recientes")
            .WithDescription(
                "Con `sucursalId`, cuenta los traslados en que la sede es origen O destino: " +
                "los dos lados tienen algo pendiente. `topNovedades` se acota entre 1 y 50.");

        return rutas;
    }
}
