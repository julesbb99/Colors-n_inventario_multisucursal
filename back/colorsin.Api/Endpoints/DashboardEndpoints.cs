using Colorsin.Api.Auth;
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
            .WithTags("Dashboard")
            // En el GRUPO, no endpoint por endpoint: asi un endpoint nuevo nace
            // protegido y no hay que acordarse de nada. Sin token, 401.
            //
            // Esto controla QUIEN entra. Lo que cada quien puede VER lo decide el
            // aislamiento por sede, y ese no vive aqui sino en DashboardService,
            // para que no dependa de que la capa HTTP se acuerde de aplicarlo.
            .RequireAuthorization();

        // ---------------------------------------------------------------------
        // Resumen general
        // ---------------------------------------------------------------------
        grupo.MapGet("/resumen", async (
                IDashboardService dashboard,
                int? sucursalId,
                DateOnly? fechaCorte,
                int? diasUmbralVencimiento,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await dashboard.ObtenerResumenGeneralAsync(
                sucursalId, fechaCorte, diasUmbralVencimiento, cancellationToken)))
            .WithName("DashboardResumenGeneral")
            .WithSummary("Totales de la pantalla de inicio")
            .WithDescription(
                "Ventas del dia y del mes, stock total en litros, traslados en transito, " +
                "alertas de reposicion y alertas de caducidad. `sucursalId` omitido trae toda " +
                "la red. `fechaCorte` omitida usa el dia de hoy; darla permite consultar un " +
                "cierre pasado. " +
                "`diasUmbralVencimiento` omitido usa el configurado en " +
                "`AlertasInventario:DiasUmbralVencimiento`. " +
                "OJO: las alertas de caducidad se cuentan siempre contra HOY, no contra " +
                "`fechaCorte`: lo que esta por vencer solo tiene sentido desde el presente.");

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

        grupo.MapGet("/ventas/historico", async (
                IDashboardService dashboard,
                int? sucursalId,
                int? meses,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await dashboard.ObtenerHistoricoVentasAsync(
                sucursalId, meses ?? 12, cancellationToken)))
            .WithName("DashboardHistoricoVentas")
            .WithSummary("Ventas mes a mes y acumulado de toda la historia")
            .WithDescription(
                "La serie mensual mas el total de TODAS las ventas registradas. " +
                "EL ACUMULADO NO SALE DE SUMAR LA SERIE: esa viene recortada a `meses`, y el " +
                "acumulado es de toda la historia, sin acotar por fecha. Por eso pedir menos " +
                "meses no esconde ninguna venta del total. " +
                "`meses` cuenta el actual: 12 son once hacia atras mas este. Se acota entre 1 y " +
                "120. " +
                "`promedioMensual` se divide entre los meses QUE TUVIERON VENTAS, no entre los " +
                "solicitados: dividir entre 12 cuando el negocio lleva dos abiertos daria una " +
                "media falsamente baja.");

        // ---------------------------------------------------------------------
        // Rotacion
        // ---------------------------------------------------------------------
        grupo.MapGet("/rotacion", async (
                IDashboardService dashboard,
                DateOnly? desde,
                DateOnly? hasta,
                int? sucursalId,
                CancellationToken cancellationToken) =>
            {
                // Por defecto, los ultimos 90 dias. Mismo criterio que el rango
                // por defecto de /ventas: es comodidad del transporte, no una
                // regla; el servicio sigue exigiendo las dos fechas.
                //
                // Noventa y no treinta porque la rotacion de un producto de
                // pintura no se ve en un mes: con pocas ventas al mes, casi todo
                // el catalogo saldria "sin movimiento".
                var fin = hasta ?? DateOnly.FromDateTime(DateTime.Now);
                var inicio = desde ?? fin.AddDays(-89);

                return TypedResults.Ok(await dashboard.ObtenerRotacionProductosAsync(
                    inicio, fin, sucursalId, cancellationToken));
            })
            .WithName("DashboardRotacionProductos")
            .WithSummary("Rotacion del catalogo: alta y baja demanda")
            .WithDescription(
                "SE CLASIFICA POR DIAS DE COBERTURA -cuanto aguanta el stock actual al ritmo del " +
                "periodo- y no por cantidad vendida: '400 litros' no dice si es mucho sin saber " +
                "cuanto hay en bodega, y los dias si son comparables entre productos de unidades " +
                "distintas. Hasta 30 dias es Alta, desde 90 es Baja, en medio Media. " +
                "'SinMovimiento' es su propia clase y no rotacion baja: la baja se calcula, esta " +
                "no se puede calcular porque no hay ritmo. " +
                "Salen TODOS los productos del catalogo, tambien los que no se vendieron ni " +
                "entraron nunca a la sede: ese es el caso que hay que mirar. " +
                "`desde` y `hasta` son inclusivas; omitidas, los ultimos 90 dias.");

        // ---------------------------------------------------------------------
        // Comparativa entre sedes
        // ---------------------------------------------------------------------
        grupo.MapGet("/sucursales/comparativa", async (
                IDashboardService dashboard,
                DateOnly? desde,
                DateOnly? hasta,
                CancellationToken cancellationToken) =>
            {
                var fin = hasta ?? DateOnly.FromDateTime(DateTime.Now);
                var inicio = desde ?? new DateOnly(fin.Year, fin.Month, 1);

                return TypedResults.Ok(await dashboard.ObtenerComparativaSucursalesAsync(
                    inicio, fin, cancellationToken));
            })
            .WithName("DashboardComparativaSucursales")
            .WithSummary("Rendimiento comparado entre sedes. Administracion y gerencia.")
            .WithDescription(
                "NO APLICA EL AISLAMIENTO POR SEDE, y es la segunda consulta del sistema de la " +
                "que hay que decirlo -la otra es el listado de existencias-. Una comparativa en " +
                "la que cada gerente solo ve su propia fila no es una comparativa: no hay contra " +
                "que comparar. " +
                "LO QUE SIGUE PROTEGIDO: el operador recibe 403, y lo que se expone son " +
                "AGREGADOS. No hay ninguna venta concreta, ningun cliente ni ningun precio: " +
                "quien lea esta tabla sabe que una sede vendio mas, no a quien ni a como. " +
                "`participacionPorcentaje` es la tajada de la red; nula si la red no vendio nada. " +
                "`ventaPorLitroEnBodega` mide la productividad del inventario, que es lo que " +
                "permite comparar una sede grande con una pequena. " +
                "Salen TODAS las sedes, tambien las que se quedaron en cero.")
            .RequireAuthorization(PoliticasAutorizacion.Supervision);

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
                dias,
                topLotes ?? 10,
                topMovimientos ?? 10,
                cancellationToken)))
            .WithName("DashboardMetricasInventario")
            .WithSummary("Stock por sede, vencimientos y ultimos movimientos")
            .WithDescription(
                "`dias` es el horizonte de vencimientos, entre 1 y 365; omitido usa el " +
                "configurado en `AlertasInventario:DiasUmbralVencimiento`, el mismo con el que " +
                "el resumen cuenta sus alertas. Los lotes YA VENCIDOS con saldo entran siempre, " +
                "sea cual sea el horizonte. Los lotes llegan en orden FEFO. " +
                "`topLotes` y `topMovimientos` se acotan entre 1 y 50.");

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
