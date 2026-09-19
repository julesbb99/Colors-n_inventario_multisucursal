using Colorsin.Application.Dashboard.DTOs;
using Colorsin.Application.Inventario.DTOs;
using Colorsin.Domain.Transferencias;

namespace Colorsin.Application.Dashboard.Repositories;

/// <summary>Ventas de un periodo: cuantas y por cuanto.</summary>
/// <param name="Cantidad">Ventas registradas.</param>
/// <param name="Total">Suma de <c>ventas.total</c>.</param>
public sealed record ResumenVentas(int Cantidad, decimal Total);

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
