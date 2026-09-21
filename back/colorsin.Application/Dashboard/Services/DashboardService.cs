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

    /// <summary>
    /// Ventana del historico de ventas, en meses.
    ///
    /// El maximo es de 10 anos: mas alla, la grafica deja de poder dibujarse y
    /// la consulta empieza a recorrer la tabla entera. El acumulado historico no
    /// pasa por aqui, asi que pedir menos meses no esconde ninguna venta del
    /// total.
    /// </summary>
    private const int MesesMinimo = 1;
    private const int MesesMaximo = 120;

    /// <summary>
    /// Los cortes de la rotacion, en DIAS DE COBERTURA.
    ///
    /// Hasta 30 dias de stock es rotacion alta: se vende al ritmo de reponer
    /// mensualmente. Mas de 90, baja: hay mercancia para un trimestre parada en
    /// el estante, y en pintura eso ademas es producto que puede caducar antes
    /// de venderse.
    ///
    /// SON UN CRITERIO DE NEGOCIO, no un dato del sistema: no salen de ninguna
    /// tabla y no hay forma de deducirlos. Viajan en la respuesta para que la
    /// pantalla pueda rotularlos, y el dia que la empresa fije otros se cambian
    /// en esta linea.
    /// </summary>
    private const int DiasCoberturaAlta = 30;
    private const int DiasCoberturaBaja = 90;

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

        // Lo que se pidio y llego corto. Va en el resumen porque es dinero
        // comprometido que todavia no esta en la bodega, y hasta ahora solo se
        // veia entrando orden por orden.
        var faltante = await _repositorio.ObtenerFaltanteRecepcionAsync(sede, cancellationToken);

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
            faltante.Ordenes,
            faltante.Valor,
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
    // HISTORICO DE VENTAS
    // =========================================================================

    public async Task<HistoricoVentasDto> ObtenerHistoricoVentasAsync(
        int? sucursalId = null,
        int meses = 12,
        CancellationToken cancellationToken = default)
    {
        var sede = _contexto.ResolverFiltroSucursal(sucursalId);
        var nombreSucursal = await ResolverNombreSucursalAsync(sede, cancellationToken);

        var mesesAcotados = Acotar(meses, MesesMinimo, MesesMaximo);

        // La ventana empieza el dia 1 del mes de hace N-1: pedir "12 meses" con
        // el mes en curso dentro son once hacia atras mas este. Restar 12 daria
        // trece columnas en la grafica.
        var hoy = HoyLocal();
        var primerMes = new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(-(mesesAcotados - 1));

        var serie = await _repositorio.ObtenerVentasPorMesAsync(
            sede, primerMes.ToDateTime(TimeOnly.MinValue), cancellationToken);

        // El acumulado NO se suma de la serie: esa viene recortada a la ventana,
        // y lo que se pidio es "la suma de todas las ventas realizadas".
        var historico = await _repositorio.ObtenerAcumuladoVentasAsync(sede, cancellationToken);

        // La media se calcula sobre los meses QUE TUVIERON VENTAS de toda la
        // historia, no sobre los de la ventana ni sobre los solicitados.
        // Dividir entre 12 cuando el negocio lleva dos meses abiertos daria una
        // media falsamente baja, y esta cifra se usa para juzgar el mes en curso.
        var mesesConVentas = await _repositorio.ObtenerVentasPorMesAsync(
            sede, null, cancellationToken);

        var promedioMensual = mesesConVentas.Count == 0
            ? 0m
            : Redondear(historico.Total / mesesConVentas.Count);

        return new HistoricoVentasDto(
            sede,
            nombreSucursal,
            mesesAcotados,
            serie,
            historico.Cantidad,
            historico.Total,
            historico.Cantidad == 0 ? 0m : Redondear(historico.Total / historico.Cantidad),
            historico.Primera,
            historico.Ultima,
            promedioMensual);
    }

    // =========================================================================
    // ROTACION DE PRODUCTOS
    // =========================================================================

    public async Task<RotacionProductosDto> ObtenerRotacionProductosAsync(
        DateOnly desde,
        DateOnly hasta,
        int? sucursalId = null,
        CancellationToken cancellationToken = default)
    {
        var sede = _contexto.ResolverFiltroSucursal(sucursalId);
        var nombreSucursal = await ResolverNombreSucursalAsync(sede, cancellationToken);

        // Rango al reves: se responde vacio y sin ir a la base, igual que en
        // metricas de ventas.
        if (hasta < desde)
        {
            return new RotacionProductosDto(
                desde, hasta, 0, sede, nombreSucursal,
                DiasCoberturaAlta, DiasCoberturaBaja,
                [], 0, 0, 0, 0);
        }

        // Inclusivo por los dos extremos: del 1 al 30 son 30 dias, no 29. Es el
        // divisor del ritmo diario, asi que un dia de diferencia se nota.
        var dias = hasta.DayNumber - desde.DayNumber + 1;

        var vendido = await _repositorio.ObtenerVendidoPorProductoAsync(
            sede,
            desde.ToDateTime(TimeOnly.MinValue),
            hasta.AddDays(1).ToDateTime(TimeOnly.MinValue),
            cancellationToken);

        var saldos = await _repositorio.ObtenerSaldoPorProductoAsync(sede, cancellationToken);

        var porProducto = vendido.ToDictionary(v => v.ProductoId);

        var filas = saldos
            .Select(s =>
            {
                porProducto.TryGetValue(s.ProductoId, out var v);

                var vendidaBase = v.CantidadBase ?? 0m;
                var facturado = v.TotalFacturado;

                // VECES QUE ROTO: lo vendido entre lo que hay. Nulo sin saldo,
                // porque dividir entre cero no da "rotacion infinita" sino que
                // no se puede medir. El producto agotado que se vendio mucho
                // sale con cobertura 0, que es la senal util.
                decimal? veces = s.SaldoBase > 0m
                    ? Redondear(vendidaBase / s.SaldoBase)
                    : null;

                // DIAS DE COBERTURA: cuanto aguanta el stock al ritmo del
                // periodo. Nulo sin ventas: sin ritmo no hay cobertura, y poner
                // un numero enorme lo mezclaria con los de rotacion baja, que
                // si se venden.
                decimal? cobertura = vendidaBase > 0m
                    ? Redondear(s.SaldoBase * dias / vendidaBase)
                    : null;

                var clase = vendidaBase <= 0m
                    ? ClaseRotacion.SinMovimiento
                    : cobertura is null || cobertura <= DiasCoberturaAlta
                        ? ClaseRotacion.Alta
                        : cobertura >= DiasCoberturaBaja
                            ? ClaseRotacion.Baja
                            : ClaseRotacion.Media;

                return new RotacionProductoDto(
                    s.ProductoId,
                    s.Nombre,
                    s.Categoria,
                    s.UnidadBaseSimbolo,
                    vendidaBase,
                    s.SaldoBase,
                    facturado,
                    veces,
                    cobertura,
                    clase.ToString());
            })
            // Los sin movimiento AL FINAL aunque su cobertura sea nula: ordenar
            // por cobertura a secas los pondria primeros -nulo ordena antes- y
            // encabezarian la lista de "mas demanda" los que no se vendieron.
            .OrderBy(f => f.Clase == nameof(ClaseRotacion.SinMovimiento) ? 1 : 0)
            .ThenBy(f => f.DiasCobertura ?? decimal.MaxValue)
            .ThenByDescending(f => f.TotalFacturado)
            .ToList();

        return new RotacionProductosDto(
            desde,
            hasta,
            dias,
            sede,
            nombreSucursal,
            DiasCoberturaAlta,
            DiasCoberturaBaja,
            filas,
            filas.Count(f => f.Clase == nameof(ClaseRotacion.Alta)),
            filas.Count(f => f.Clase == nameof(ClaseRotacion.Media)),
            filas.Count(f => f.Clase == nameof(ClaseRotacion.Baja)),
            filas.Count(f => f.Clase == nameof(ClaseRotacion.SinMovimiento)));
    }

    // =========================================================================
    // COMPARATIVA ENTRE SEDES
    // =========================================================================

    public async Task<ComparativaSucursalesDto> ObtenerComparativaSucursalesAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancellationToken = default)
    {
        // AQUI NO SE LLAMA A ResolverFiltroSucursal, y es la unica excepcion de
        // este servicio. Una comparativa en la que cada gerente solo ve su
        // propia fila no es una comparativa: no hay contra que comparar.
        //
        // LO QUE SI SE COMPRUEBA es el rol, y se comprueba aqui ademas de en el
        // endpoint. El endpoint lleva la politica de supervision, pero si algun
        // dia alguien registra otra ruta a este metodo sin acordarse, esta
        // linea es la que sigue negando el paso al operador.
        if (!RolesColorsin.EsSupervision(_contexto.Rol))
        {
            throw new AccesoDenegadoException(
                $"El usuario {_contexto.UsuarioId} (rol '{_contexto.Rol}') pidio la comparativa " +
                "entre sedes, que esta reservada a administracion y gerencia.");
        }

        if (hasta < desde)
        {
            return new ComparativaSucursalesDto(desde, hasta, [], 0m, 0);
        }

        var desdeInclusivo = desde.ToDateTime(TimeOnly.MinValue);
        var hastaExclusivo = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var sedes = await _repositorio.ObtenerSucursalesAsync(cancellationToken);
        var ventas = await _repositorio.ObtenerVentasPorSucursalAsync(
            desdeInclusivo, hastaExclusivo, cancellationToken);
        var traslados = await _repositorio.ObtenerTrasladosPorSucursalAsync(
            desdeInclusivo, hastaExclusivo, cancellationToken);
        var stock = await _repositorio.ObtenerStockPorSucursalAsync(null, cancellationToken);

        var totalRed = ventas.Sum(v => v.Total);
        var ventasRed = ventas.Sum(v => v.Cantidad);

        var filas = sedes
            .Select(s =>
            {
                var v = ventas.FirstOrDefault(x => x.SucursalId == s.Id);
                var t = traslados.FirstOrDefault(x => x.SucursalId == s.Id);
                var inv = stock.FirstOrDefault(x => x.SucursalId == s.Id);

                var saldoLitros = inv?.SaldoLitros ?? 0m;

                return new RendimientoSucursalDto(
                    s.Id,
                    s.Nombre,
                    s.Ciudad,
                    v.Cantidad,
                    v.Total,
                    v.Cantidad == 0 ? 0m : Redondear(v.Total / v.Cantidad),
                    // Nula cuando la red no vendio nada: sin tarta que repartir,
                    // un 0 % sugeriria que esta sede se quedo fuera de algo.
                    totalRed <= 0m ? null : Redondear(v.Total * 100m / totalRed),
                    saldoLitros,
                    inv?.ProductosEnAlerta ?? 0,
                    t.Despachados,
                    t.Recibidos,
                    // Productividad del inventario: pesos vendidos por litro
                    // almacenado. Es lo que permite comparar una sede grande con
                    // una pequena, cosa que el total nunca dice.
                    saldoLitros <= 0m ? null : Redondear(v.Total / saldoLitros));
            })
            .OrderByDescending(f => f.TotalVendido)
            .ThenBy(f => f.SucursalNombre, StringComparer.CurrentCulture)
            .ToList();

        return new ComparativaSucursalesDto(desde, hasta, filas, totalRed, ventasRed);
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
    /// Redondea a dos decimales, alejando del cero.
    ///
    /// <c>AwayFromZero</c> y no el redondeo bancario de .NET, que es el que
    /// aplica <c>Math.Round</c> por omision: aquel redondea 2,5 a 2 y 3,5 a 4,
    /// y nadie que lea un informe espera eso.
    /// </summary>
    private static decimal Redondear(decimal valor) =>
        Math.Round(valor, DecimalesMoneda, MidpointRounding.AwayFromZero);

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
