using Colorsin.Application.Dashboard.DTOs;
using Colorsin.Application.Inventario.DTOs;
using Colorsin.Domain.Transferencias;

namespace Colorsin.Application.Dashboard.Repositories;

/// <summary>Ventas de un periodo: cuantas y por cuanto.</summary>
/// <param name="Cantidad">Ventas registradas.</param>
/// <param name="Total">Suma de <c>ventas.total</c>.</param>
public sealed record ResumenVentas(int Cantidad, decimal Total);

/// <summary>Una sede, con lo justo para nombrarla en una tabla.</summary>
/// <param name="Id">Sede.</param>
/// <param name="Nombre">Nombre de la sede.</param>
/// <param name="Ciudad">Ciudad. Distingue sedes de nombre parecido.</param>
public readonly record struct SucursalBasica(int Id, string Nombre, string? Ciudad);

/// <summary>El acumulado de ventas de toda la historia.</summary>
/// <param name="Cantidad">Ventas registradas.</param>
/// <param name="Total">Suma de <c>ventas.total</c>.</param>
/// <param name="Primera">Fecha de la venta mas antigua. Nula si no hay ninguna.</param>
/// <param name="Ultima">Fecha de la venta mas reciente. Nula si no hay ninguna.</param>
public readonly record struct HistoricoVentas(
    int Cantidad,
    decimal Total,
    DateTime? Primera,
    DateTime? Ultima);

/// <summary>Lo vendido de un producto en el periodo.</summary>
/// <param name="ProductoId">Producto.</param>
/// <param name="CantidadBase">
/// En la unidad base del producto. Nula cuando alguna linea no se pudo
/// convertir, que pasa si la unidad de venta y la base no tienen factor comun.
/// </param>
/// <param name="TotalFacturado">Dinero de ese producto en el periodo.</param>
public readonly record struct VendidoPorProducto(
    int ProductoId,
    decimal? CantidadBase,
    decimal TotalFacturado);

/// <summary>Saldo actual de un producto, con lo que hace falta para nombrarlo.</summary>
/// <param name="ProductoId">Producto.</param>
/// <param name="Nombre">Nombre del producto.</param>
/// <param name="Categoria">Categoria del catalogo. Puede ser nula.</param>
/// <param name="UnidadBaseSimbolo">Unidad del saldo. Nula si el producto no tiene unidad base.</param>
/// <param name="SaldoBase">Lo que hay ahora, en esa unidad. Cero si nunca entro a la sede.</param>
public readonly record struct SaldoPorProducto(
    int ProductoId,
    string Nombre,
    string? Categoria,
    string? UnidadBaseSimbolo,
    decimal SaldoBase);

/// <summary>Ventas de una sede en el periodo.</summary>
/// <param name="SucursalId">Sede.</param>
/// <param name="Cantidad">Ventas registradas.</param>
/// <param name="Total">Suma de <c>ventas.total</c>.</param>
public readonly record struct VentasPorSucursal(
    int SucursalId,
    int Cantidad,
    decimal Total);

/// <summary>Traslados de una sede en el periodo, por lado.</summary>
/// <param name="SucursalId">Sede.</param>
/// <param name="Despachados">Los que esta sede mando.</param>
/// <param name="Recibidos">Los que esta sede recibio.</param>
public readonly record struct TrasladosPorSucursal(
    int SucursalId,
    int Despachados,
    int Recibidos);

/// <summary>Cuantos traslados hay en un estado.</summary>
/// <param name="Estado">
/// El estado, o <c>null</c> para las filas que tienen la columna en NULL.
/// </param>
/// <param name="Cantidad">Cuantos.</param>
public sealed record ConteoPorEstado(EstadoTransferencia? Estado, int Cantidad);

/// <summary>
/// Consultas agregadas para el tablero. Cada metodo es UNA sentencia SQL.
///
/// POR QUE DEVUELVE DTOs Y NO ENTIDADES, a diferencia de los otros repositorios
/// del proyecto. Aqui el trabajo ES la agregacion: <c>SUM</c>, <c>COUNT</c> y
/// <c>GROUP BY</c> corriendo en el servidor. Devolver entidades obligaria a
/// traer las tablas completas y agregar en memoria, que es justo lo que no se
/// debe hacer: el tablero lee miles de filas para mostrar diez numeros. Todo va
/// proyectado con <c>Select</c>, sin <c>Include</c> y sin rastreo.
///
/// SOLO LECTURA. No hay escritura, ni <c>SaveChanges</c>, ni transacciones: un
/// tablero no cambia nada, y sin escrituras no hay nada que hacer atomico.
///
/// LA LISTA ES CORTA A PROPOSITO. No hay un metodo por cada cifra del tablero:
/// varias se derivan de otras. El saldo en litros, el valor del inventario y el
/// conteo de alertas salen de sumar <see cref="ObtenerStockPorSucursalAsync"/>,
/// y los traslados en transito salen de
/// <see cref="ObtenerConteoTransferenciasAsync"/>. Un metodo aparte para cada
/// una repetiria el criterio en dos consultas que despues se desincronizan: el
/// dia que cambie la definicion de "alerta", cambia en un solo sitio.
///
/// SOBRE LOS FILTROS. <c>sucursalId</c> nulo significa toda la red, en todos los
/// metodos. Los rangos de fecha van siempre como
/// [<c>desdeInclusivo</c>, <c>hastaExclusivo</c>): las columnas de fecha son
/// DATETIME, y un <c>&lt;=</c> contra el ultimo dia dejaria por fuera todo lo
/// registrado despues de medianoche.
/// </summary>
public interface IDashboardRepository
{
    // -------------------------------------------------------------------------
    // Comun
    // -------------------------------------------------------------------------

    /// <summary>
    /// Nombre de una sede, o <c>null</c> si ese id no existe. Sirve para rotular
    /// el tablero sin cargar la entidad entera.
    /// </summary>
    Task<string?> ObtenerNombreSucursalAsync(
        int sucursalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// TODAS las sedes de la red, con su ciudad, ordenadas por nombre.
    ///
    /// Hace falta para la comparativa: alli tienen que salir tambien las sedes
    /// que no vendieron ni tienen saldo, porque una sede ausente de la tabla se
    /// lee como que no existe, y lo que se quiere ver es que se quedo en cero.
    /// </summary>
    Task<IReadOnlyList<SucursalBasica>> ObtenerSucursalesAsync(
        CancellationToken cancellationToken = default);

    // -------------------------------------------------------------------------
    // Ventas
    // -------------------------------------------------------------------------

    /// <summary>Cuantas ventas y por cuanto, en un rango.</summary>
    Task<ResumenVentas> ObtenerResumenVentasAsync(
        int? sucursalId,
        DateTime desdeInclusivo,
        DateTime hastaExclusivo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ventas agrupadas por dia, en orden cronologico. Solo aparecen los dias
    /// que tuvieron ventas.
    /// </summary>
    Task<IReadOnlyList<VentasPorDiaDto>> ObtenerVentasPorDiaAsync(
        int? sucursalId,
        DateTime desdeInclusivo,
        DateTime hastaExclusivo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ranking de productos por facturacion, de mayor a menor.
    ///
    /// La cantidad se convierte a la unidad base del producto ANTES de sumarse;
    /// ver <see cref="ProductoMasVendidoDto.CantidadBase"/>.
    /// </summary>
    Task<IReadOnlyList<ProductoMasVendidoDto>> ObtenerProductosMasVendidosAsync(
        int? sucursalId,
        DateTime desdeInclusivo,
        DateTime hastaExclusivo,
        int top,
        CancellationToken cancellationToken = default);

    /// <summary>Ranking de clientes por monto comprado, de mayor a menor.</summary>
    Task<IReadOnlyList<ClienteTopDto>> ObtenerTopClientesAsync(
        int? sucursalId,
        DateTime desdeInclusivo,
        DateTime hastaExclusivo,
        int top,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ventas agrupadas por MES calendario, del mes mas antiguo al mas reciente.
    /// Solo aparecen los meses que tuvieron ventas.
    /// </summary>
    /// <param name="desdeInclusivo">
    /// Desde cuando. Nulo trae toda la historia, que es lo que hace falta para el
    /// acumulado.
    /// </param>
    Task<IReadOnlyList<VentasPorMesDto>> ObtenerVentasPorMesAsync(
        int? sucursalId,
        DateTime? desdeInclusivo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// El acumulado de TODA la historia, sin acotar por fecha.
    ///
    /// Existe aparte de sumar <see cref="ObtenerVentasPorMesAsync"/> porque esa
    /// lista puede venir recortada a una ventana de meses: sumarla daria el
    /// acumulado de la ventana y lo presentaria como el de la historia.
    /// </summary>
    Task<HistoricoVentas> ObtenerAcumuladoVentasAsync(
        int? sucursalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lo vendido de cada producto en el periodo, en su UNIDAD BASE, con lo
    /// facturado.
    ///
    /// Es <see cref="ObtenerProductosMasVendidosAsync"/> sin tope y sin los
    /// datos descriptivos: aqui interesan todos los productos, porque el que no
    /// vendio nada es justo el que hay que mirar. El nombre y la unidad salen de
    /// <see cref="ObtenerSaldoPorProductoAsync"/>, que recorre el catalogo.
    /// </summary>
    Task<IReadOnlyList<VendidoPorProducto>> ObtenerVendidoPorProductoAsync(
        int? sucursalId,
        DateTime desdeInclusivo,
        DateTime hastaExclusivo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// TODO el catalogo con el saldo actual de cada producto, en su unidad base.
    ///
    /// Recorre <c>productos</c> y no <c>inventario_sucursal</c> a proposito: un
    /// producto que nunca entro a la sede no tiene fila de saldo, y si se
    /// recorriera la tabla de saldos desapareceria del informe de rotacion.
    /// Desaparecer es justo lo contrario de lo que merece un producto que no se
    /// mueve.
    /// </summary>
    Task<IReadOnlyList<SaldoPorProducto>> ObtenerSaldoPorProductoAsync(
        int? sucursalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ventas del periodo agrupadas por SEDE. Para la comparativa de
    /// rendimiento.
    /// </summary>
    Task<IReadOnlyList<VentasPorSucursal>> ObtenerVentasPorSucursalAsync(
        DateTime desdeInclusivo,
        DateTime hastaExclusivo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Traslados del periodo contados por sede, separando los que despacho de
    /// los que recibio. Son dos cifras distintas y no se suman: una mide lo que
    /// la sede entrega y la otra lo que le llega.
    /// </summary>
    Task<IReadOnlyList<TrasladosPorSucursal>> ObtenerTrasladosPorSucursalAsync(
        DateTime desdeInclusivo,
        DateTime hastaExclusivo,
        CancellationToken cancellationToken = default);

    // -------------------------------------------------------------------------
    // Inventario
    // -------------------------------------------------------------------------

    /// <summary>
    /// Una fila por sede con saldos registrados, de mayor a menor saldo en
    /// litros.
    ///
    /// Es la consulta base del bloque de inventario: de sumar sus filas salen
    /// tambien el saldo total en litros, el valor del inventario y el numero de
    /// alertas de stock bajo.
    ///
    /// Una sede sin ninguna fila en <c>inventario_sucursal</c> no aparece. Es un
    /// GROUP BY sobre los saldos, no sobre el catalogo de sedes: sin saldos no
    /// hay nada que reportar de ella.
    /// </summary>
    Task<IReadOnlyList<StockPorSucursalDto>> ObtenerStockPorSucursalAsync(
        int? sucursalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lotes con saldo que vencen el dia <paramref name="limiteVencimiento"/> o
    /// antes, en orden FEFO. Los ya vencidos entran por definicion, porque su
    /// fecha es anterior.
    /// </summary>
    /// <param name="hoy">Fecha contra la que se cuentan los dias que faltan.</param>
    /// <param name="limiteVencimiento">Ultimo dia de vencimiento que se incluye.</param>
    Task<IReadOnlyList<LotePorVencerDto>> ObtenerLotesProximosAVencerAsync(
        int? sucursalId,
        DateOnly hoy,
        DateOnly limiteVencimiento,
        int top,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// CUANTOS lotes con saldo vencen el dia <paramref name="limiteVencimiento"/>
    /// o antes. Mismo criterio que <see cref="ObtenerLotesProximosAVencerAsync"/>,
    /// incluidos los ya vencidos.
    ///
    /// Existe aparte, y no contando lo que devuelve aquel, porque aquel viene
    /// recortado por <c>top</c>: con diez lotes en la lista y un tope de diez, la
    /// cifra del resumen diria "10" tanto si hay diez como si hay noventa. Un
    /// COUNT no trae filas, asi que sale mas barato ademas de salir bien.
    /// </summary>
    Task<int> ContarLotesProximosAVencerAsync(
        int? sucursalId,
        DateOnly limiteVencimiento,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ultimas filas del libro mayor, de la mas nueva a la mas vieja.
    ///
    /// Proyecta a mano en vez de reusar <c>IInventarioService.ObtenerMovimientosAsync</c>:
    /// aquel carga las entidades con cinco <c>Include</c> para despues mapearlas,
    /// y el tablero solo necesita las columnas que muestra.
    /// </summary>
    Task<IReadOnlyList<MovimientoInventarioDto>> ObtenerMovimientosRecientesAsync(
        int? sucursalId,
        int top,
        CancellationToken cancellationToken = default);

    // -------------------------------------------------------------------------
    // Transferencias
    // -------------------------------------------------------------------------

    /// <summary>
    /// Cuantos traslados hay en cada estado. Un traslado entra si la sede es su
    /// origen O su destino.
    ///
    /// Solo devuelve los estados que tienen al menos un traslado; armar los
    /// ceros de los demas es trabajo de quien llama.
    /// </summary>
    Task<IReadOnlyList<ConteoPorEstado>> ObtenerConteoTransferenciasAsync(
        int? sucursalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ultimas novedades reportadas, de la mas nueva a la mas vieja, con el
    /// producto y las dos sedes del traslado.
    /// </summary>
    Task<IReadOnlyList<NovedadRecienteDto>> ObtenerNovedadesRecientesAsync(
        int? sucursalId,
        int top,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lo que falta por llegar de las ordenes que se recibieron CORTAS y siguen
    /// abiertas (estado 'ParcialmenteRecibida').
    /// </summary>
    Task<FaltanteRecepcion> ObtenerFaltanteRecepcionAsync(
        int? sucursalId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// El hueco entre lo pedido y lo recibido de las ordenes abiertas a medias.
/// </summary>
/// <param name="Ordenes">Cuantas ordenes estan en ese estado.</param>
/// <param name="Valor">
/// Lo que vale lo que falta, al precio pactado y con su descuento.
///
/// NO SE DESCUENTA DE NINGUN SALDO: esa mercancia nunca entro a la bodega, asi
/// que no hay nada que restar. Es un dato de compras, no de inventario.
/// </param>
public readonly record struct FaltanteRecepcion(int Ordenes, decimal Valor);
