using Colorsin.Application.Comun.Auth;
using Colorsin.Application.Dashboard.DTOs;
using Colorsin.Application.Dashboard.Repositories;
using Colorsin.Application.Inventario;
using Colorsin.Domain.Transferencias;

namespace Colorsin.Application.Dashboard.Services;

/// <inheritdoc cref="IDashboardService"/>
///
/// QUE HACE ESTA CLASE, ya que el repositorio es quien consulta: acota los
/// parametros, resuelve las fechas de corte, deriva las cifras que no son una
/// consulta (el total en litros, el ticket promedio, los contadores por estado)
/// y arma los cuatro bloques. Nada de eso es SQL, y nada de eso deberia estar
/// repetido en cada pantalla que consuma el tablero.
///
/// LAS CONSULTAS VAN EN SERIE, no con <c>Task.WhenAll</c>. Todas comparten el
/// <c>AppDbContext</c> de la peticion, que no admite dos operaciones a la vez:
/// lanzarlas en paralelo falla con "A second operation was started on this
/// context". Paralelizarlas exigiria un DbContext por consulta, y a esta escala
/// no compensa.
///
/// NO ESCRIBE AUDITORIA. Consultar un tablero no es un hecho que haya que
/// rastrear, y un evento por cada refresco llenaria `auditoria_eventos` de ruido
/// que le quita valor a lo que si importa rastrear.
///
/// EL AISLAMIENTO ENTRE SEDES SE APLICA AQUI, no en los endpoints. Los cuatro
/// metodos empiezan igual: pasan el `sucursalId` que llego por
/// <see cref="IUsuarioContexto.ResolverFiltroSucursal"/> y usan lo que ese
/// metodo devuelve, nunca el parametro original.
///
/// Podria haberse hecho en la capa HTTP, pero entonces la garantia duraria
/// hasta el primer endpoint que se escribiera sin acordarse. Aqui no hay forma
/// de consultar el tablero sin pasar por la regla: quien llame al servicio, la
/// cumple. El precio es que este servicio ya no se puede usar fuera de una
/// peticion autenticada, lo cual es exactamente lo que se quiere de un tablero.
public sealed class DashboardService : IDashboardService
{
    // Topes duros de los parametros. Sin ellos, un `top` descuidado se trae el
    // catalogo entero. El horizonte de vencimientos lo acota
    // OpcionesAlertasInventario, que es tambien quien conoce el valor por
    // defecto: dos sitios acotando lo mismo terminan acotando distinto.
    private const int TopMinimo = 1;
    private const int TopMaximo = 50;

    /// <summary>Decimales de los importes en pesos.</summary>
    private const int DecimalesMoneda = 2;

    private readonly IDashboardRepository _repositorio;
    private readonly IUsuarioContexto _contexto;
    private readonly OpcionesAlertasInventario _alertas;

    public DashboardService(
        IDashboardRepository repositorio,
        IUsuarioContexto contexto,
        OpcionesAlertasInventario alertas)
    {
        _repositorio = repositorio;
        _contexto = contexto;
        _alertas = alertas;
    }

    // =========================================================================
    // RESUMEN GENERAL
    // =========================================================================

    public async Task<ResumenGeneralDto> ObtenerResumenGeneralAsync(
        int? sucursalId = null,
        DateOnly? fechaCorte = null,
        int? diasUmbralVencimiento = null,
        CancellationToken cancellationToken = default)
    {
        // Aislamiento entre sedes. Desde aqui se usa `sede`, NUNCA `sucursalId`:
        // para un gerente o un operador los dos valores no coinciden.
        var sede = _contexto.ResolverFiltroSucursal(sucursalId);

        var corte = fechaCorte ?? HoyLocal();

        // El dia va como [00:00 del corte, 00:00 del dia siguiente): `ventas.fecha`
        // es DATETIME, y comparar contra el corte a secas dejaria por fuera todo
        // lo vendido despues de medianoche.
        var inicioDelDia = corte.ToDateTime(TimeOnly.MinValue);
        var finExclusivo = corte.AddDays(1).ToDateTime(TimeOnly.MinValue);

        // Mes CALENDARIO, del dia 1 al corte. No son los ultimos 30 dias.
        var inicioDelMes = new DateOnly(corte.Year, corte.Month, 1)
            .ToDateTime(TimeOnly.MinValue);

        var nombreSucursal = await ResolverNombreSucursalAsync(sede, cancellationToken);

        var ventasDelDia = await _repositorio.ObtenerResumenVentasAsync(
            sede, inicioDelDia, finExclusivo, cancellationToken);

        var ventasDelMes = await _repositorio.ObtenerResumenVentasAsync(
            sede, inicioDelMes, finExclusivo, cancellationToken);

        // De esta unica consulta salen tres cifras del resumen: litros, saldos
        // sin convertir y alertas. Ver la nota de IDashboardRepository sobre por
        // que no hay un metodo por cada una.
        var stock = await _repositorio.ObtenerStockPorSucursalAsync(
            sede, cancellationToken);

        var estados = await _repositorio.ObtenerConteoTransferenciasAsync(
            sede, cancellationToken);

        // Las alertas de vencimiento se cuentan contra HOY, no contra la fecha de
        // corte. No es un descuido: el corte sirve para preguntar "cuanto se
        // vendio aquel dia", que es historia, mientras que "que esta por vencer"
        // solo tiene sentido desde el presente. Consultar el cierre del mes
        // pasado no deberia devolver una lista de urgencias de entonces.
        var horizonte = _alertas.ResolverHorizonte(diasUmbralVencimiento);

        var alertasVencimiento = await _repositorio.ContarLotesProximosAVencerAsync(
            sede, HoyLocal().AddDays(horizonte), cancellationToken);

        return new ResumenGeneralDto(
            // La sede EFECTIVA, no la pedida: asi un gerente que no mando nada
            // ve en la respuesta cual es el alcance real de lo que esta mirando.
            sede,
            nombreSucursal,
            corte,
            ventasDelDia.Total,
            ventasDelDia.Cantidad,
            ventasDelMes.Total,
            ventasDelMes.Cantidad,
            stock.Sum(s => s.SaldoLitros),
            stock.Sum(s => s.ProductosSinConversionALitros),
            ContarEstado(estados, EstadoTransferencia.EnTransito),
            stock.Sum(s => s.ProductosEnAlerta),
            alertasVencimiento,
            horizonte,
            DateTime.Now);
    }

    // =========================================================================
    // VENTAS
    // =========================================================================

    public async Task<MetricasVentasDto> ObtenerMetricasVentasAsync(
        DateOnly desde,
        DateOnly hasta,
        int? sucursalId = null,
        int top = 5,
        CancellationToken cancellationToken = default)
    {
        // Se resuelve ANTES de mirar el rango, para que un rango invalido no se
        // convierta en una forma de saltarse la comprobacion de sede.
        var sede = _contexto.ResolverFiltroSucursal(sucursalId);

        var nombreSucursal = await ResolverNombreSucursalAsync(sede, cancellationToken);

        // Rango al reves: es un error de quien llama, no un fallo del sistema.
        // Se responde vacio en vez de lanzar, y sin ir a la base: ninguna de las
        // cuatro consultas podria devolver algo.
        if (hasta < desde)
        {
            return new MetricasVentasDto(
                desde, hasta, sede, nombreSucursal,
                CantidadVentas: 0,
                TotalVendido: 0m,
                TicketPromedio: 0m,
                SerieDiaria: [],
                ProductosMasVendidos: [],
                TopClientes: []);
        }

        var topAcotado = Acotar(top, TopMinimo, TopMaximo);

        var desdeInclusivo = desde.ToDateTime(TimeOnly.MinValue);
        var hastaExclusivo = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var resumen = await _repositorio.ObtenerResumenVentasAsync(
            sede, desdeInclusivo, hastaExclusivo, cancellationToken);

        var serie = await _repositorio.ObtenerVentasPorDiaAsync(
            sede, desdeInclusivo, hastaExclusivo, cancellationToken);

        var productos = await _repositorio.ObtenerProductosMasVendidosAsync(
            sede, desdeInclusivo, hastaExclusivo, topAcotado, cancellationToken);

        var clientes = await _repositorio.ObtenerTopClientesAsync(
            sede, desdeInclusivo, hastaExclusivo, topAcotado, cancellationToken);

        // Sin ventas el promedio es cero, no una division por cero. Se calcula
        // aqui para que ninguna pantalla tenga que acordarse de protegerlo.
        var ticket = resumen.Cantidad == 0
            ? 0m
            : Math.Round(
                resumen.Total / resumen.Cantidad,
                DecimalesMoneda,
                MidpointRounding.AwayFromZero);

        return new MetricasVentasDto(
            desde,
            hasta,
            sede,
            nombreSucursal,
            resumen.Cantidad,
            resumen.Total,
            ticket,
            serie,
            productos,
            clientes);
    }

    // =========================================================================
    // INVENTARIO
    // =========================================================================

    public async Task<MetricasInventarioDto> ObtenerMetricasInventarioAsync(
        int? sucursalId = null,
        int? diasHorizonteVencimiento = null,
        int topLotes = 10,
        int topMovimientos = 10,
        CancellationToken cancellationToken = default)
    {
        var sede = _contexto.ResolverFiltroSucursal(sucursalId);

        var horizonte = _alertas.ResolverHorizonte(diasHorizonteVencimiento);

        // Aqui SI se lee el reloj, a diferencia del resumen general, que recibe
        // la fecha de corte por parametro. No es una inconsistencia: "cuantos
        // dias le faltan a este lote" solo tiene sentido contra hoy, mientras que
        // "cuanto se vendio" si se puede preguntar de un dia pasado.
        var hoy = HoyLocal();

        var nombreSucursal = await ResolverNombreSucursalAsync(sede, cancellationToken);

        var stock = await _repositorio.ObtenerStockPorSucursalAsync(
            sede, cancellationToken);

        var lotes = await _repositorio.ObtenerLotesProximosAVencerAsync(
            sede,
            hoy,
            hoy.AddDays(horizonte),
            Acotar(topLotes, TopMinimo, TopMaximo),
            cancellationToken);

        var movimientos = await _repositorio.ObtenerMovimientosRecientesAsync(
            sede,
            Acotar(topMovimientos, TopMinimo, TopMaximo),
            cancellationToken);

        return new MetricasInventarioDto(
            sede,
            // Del catalogo de sedes, no de `stock`: una sede sin ninguna fila de
            // inventario no sale en esa lista y se quedaria sin nombre.
            nombreSucursal,
            horizonte,
            stock.Sum(s => s.SaldoLitros),
            stock.Sum(s => s.ProductosSinConversionALitros),
            stock.Sum(s => s.ValorInventario),
            stock,
            lotes,
            movimientos);
    }

    // =========================================================================
    // TRANSFERENCIAS
    // =========================================================================

    public async Task<MetricasTransferenciasDto> ObtenerMetricasTransferenciasAsync(
        int? sucursalId = null,
        int topNovedades = 10,
        CancellationToken cancellationToken = default)
    {
        var sede = _contexto.ResolverFiltroSucursal(sucursalId);

        var nombreSucursal = await ResolverNombreSucursalAsync(sede, cancellationToken);

        var conteos = await _repositorio.ObtenerConteoTransferenciasAsync(
            sede, cancellationToken);

        var novedades = await _repositorio.ObtenerNovedadesRecientesAsync(
            sede,
            Acotar(topNovedades, TopMinimo, TopMaximo),
            cancellationToken);

        return new MetricasTransferenciasDto(
            sede,
            nombreSucursal,
            // El total sale de sumar lo que vino, no de sumar los siete
            // contadores. Asi, si algun dia se agrega un estado al enum y se
            // olvida agregarlo aqui, el total queda por encima de la suma de las
            // partes y la diferencia se ve, en vez de perderse en silencio.
            conteos.Sum(c => c.Cantidad),
            ContarEstado(conteos, EstadoTransferencia.Solicitada),
            ContarEstado(conteos, EstadoTransferencia.EnTransito),
            ContarEstado(conteos, EstadoTransferencia.Completada),
            ContarEstado(conteos, EstadoTransferencia.RecibidaParcial),
            ContarEstado(conteos, EstadoTransferencia.Rechazada),
            ContarEstado(conteos, EstadoTransferencia.Cancelada),
            conteos.FirstOrDefault(c => c.Estado is null)?.Cantidad ?? 0,
            novedades);
    }

    // =========================================================================
    // APOYO
    // =========================================================================

    /// <summary>
    /// Fecha de hoy segun el reloj local del servidor de la aplicacion.
    ///
    /// OJO CON LA ZONA HORARIA. Las columnas de fecha las llena MySQL con
    /// <c>CURRENT_TIMESTAMP</c>, es decir con el reloj del CONTENEDOR, no con el
    /// de la aplicacion. Hoy los dos estan en -05:00 y coinciden, pero eso no
    /// esta fijado en el docker-compose: si el contenedor quedara en UTC, entre
    /// las 7 de la noche y la medianoche "las ventas de hoy" mirarian un dia que
    /// para la base ya es el siguiente.
    /// </summary>
    private static DateOnly HoyLocal() => DateOnly.FromDateTime(DateTime.Now);

    /// <summary>Deja el valor dentro del rango, sin quejarse.</summary>
    private static int Acotar(int valor, int minimo, int maximo) =>
        Math.Clamp(valor, minimo, maximo);

    /// <summary>
    /// Cuantos traslados hay en un estado. Cero si el repositorio no devolvio
    /// esa fila, que es lo que pasa cuando no hay ninguno.
    /// </summary>
    private static int ContarEstado(
        IReadOnlyList<ConteoPorEstado> conteos,
        EstadoTransferencia estado) =>
        conteos.FirstOrDefault(c => c.Estado == estado)?.Cantidad ?? 0;

    /// <summary>
    /// Nombre de la sede filtrada, o <c>null</c> si el alcance es toda la red.
    /// Devuelve <c>null</c> tambien si el id no existe: el tablero responde
    /// vacio en vez de fallar, porque un filtro invalido no es una falla del
    /// sistema.
    /// </summary>
    private async Task<string?> ResolverNombreSucursalAsync(
        int? sucursalId,
        CancellationToken cancellationToken) =>
        sucursalId is int id
            ? await _repositorio.ObtenerNombreSucursalAsync(id, cancellationToken)
            : null;
}
