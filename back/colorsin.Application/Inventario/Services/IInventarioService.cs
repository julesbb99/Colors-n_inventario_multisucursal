using Colorsin.Application.Inventario.DTOs;

namespace Colorsin.Application.Inventario.Services;

/// <summary>Operaciones de stock: consulta, movimientos y alertas.</summary>
public interface IInventarioService
{
    /// <summary>Existencias de una sede, o de toda la red si no se indica sede.</summary>
    Task<IReadOnlyList<InventarioSucursalDto>> ObtenerExistenciasAsync(
        int? sucursalId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lotes disponibles de un producto en una sede, en orden FEFO: el primero
    /// de la lista es el que se debe despachar.
    /// </summary>
    Task<IReadOnlyList<LoteDto>> ObtenerLotesPorVencimientoAsync(
        int sucursalId,
        int productoId,
        CancellationToken cancellationToken = default);

    /// <summary>Movimientos del libro mayor, del mas reciente al mas antiguo.</summary>
    Task<IReadOnlyList<MovimientoInventarioDto>> ObtenerMovimientosAsync(
        int? sucursalId = null,
        int? productoId = null,
        int limite = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Productos cuyo saldo cayo a o por debajo del minimo
    /// (<c>CantidadBase &lt;= StockMinimo</c>).
    /// </summary>
    Task<IReadOnlyList<StockAlertaDto>> ObtenerAlertasStockBajoAsync(
        int? sucursalId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra una entrada o salida de stock.
    ///
    /// Convierte la cantidad a la unidad base del producto, valida que un
    /// retiro no deje el saldo en negativo, actualiza el saldo (y el lote, si
    /// se indico), anexa la fila al libro mayor y deja el evento de auditoria.
    /// Todo en una sola transaccion: o queda todo, o no queda nada.
    ///
    /// No lanza excepciones por reglas de negocio. Revisa
    /// <see cref="ResultadoMovimiento.Exito"/>.
    /// </summary>
    Task<ResultadoMovimiento> RegistrarMovimientoAsync(
        RegistrarMovimientoDto peticion,
        CancellationToken cancellationToken = default);
}
