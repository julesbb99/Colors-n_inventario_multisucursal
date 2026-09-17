namespace Colorsin.Application.Ventas.DTOs;

/// <summary>
/// Cuanto se tomo de un lote concreto para cubrir una linea de venta.
///
/// Una linea puede aparecer repartida en varios de estos: si el lote que vence
/// antes no alcanza, FEFO sigue con el siguiente.
/// </summary>
/// <param name="LoteId">
/// Lote del que se descontó. NULO cuando la cantidad salio del saldo de la sede
/// sin lote asignado, que pasa cuando los lotes registrados no cubren el saldo
/// consolidado (por ejemplo, stock que entro con un ingreso sin lote).
/// </param>
/// <param name="NumeroLote">Numero del lote. Nulo en el caso anterior.</param>
/// <param name="FechaVencimiento">Caducidad del lote, si la tiene.</param>
/// <param name="CantidadBase">Lo tomado de este lote, en unidad base del producto.</param>
/// <param name="MovimientoId">Fila que quedo en el libro mayor por este consumo.</param>
public sealed record ConsumoLoteDto(
    int? LoteId,
    string? NumeroLote,
    DateOnly? FechaVencimiento,
    decimal CantidadBase,
    int MovimientoId);

/// <summary>Lo que salio del stock por una linea de la venta.</summary>
/// <param name="DetalleId">Linea de la venta.</param>
/// <param name="ProductoId">Producto.</param>
/// <param name="ProductoNombre">Nombre del producto.</param>
/// <param name="Cantidad">Cantidad vendida, en su unidad de venta, ya redondeada a 2 decimales.</param>
/// <param name="UnidadId">Unidad de venta.</param>
/// <param name="CantidadBase">La misma cantidad en unidad base del producto.</param>
/// <param name="Consumos">
/// De que lotes salio. Uno solo cuando un lote cubre toda la linea; varios
/// cuando FEFO tuvo que encadenarlos.
/// </param>
public sealed record LineaVendidaDto(
    int DetalleId,
    int ProductoId,
    string ProductoNombre,
    decimal Cantidad,
    int UnidadId,
    decimal CantidadBase,
    IReadOnlyList<ConsumoLoteDto> Consumos);

/// <summary>Saldo de un producto en la sede despues de la venta.</summary>
/// <param name="ProductoId">Producto.</param>
/// <param name="ProductoNombre">Nombre del producto.</param>
/// <param name="CantidadBaseDescontada">Lo que salio en total, sumando las lineas de ese producto.</param>
/// <param name="SaldoResultante">Como quedo el saldo de la sede.</param>
public sealed record SaldoAfectadoDto(
    int ProductoId,
    string ProductoNombre,
    decimal CantidadBaseDescontada,
    decimal SaldoResultante);

/// <summary>
/// Venta ya concretada.
///
/// Solo existe si todo salio bien: los fallos de regla de negocio salen como
/// excepcion, no como un resultado con bandera. Ver <see cref="VentaException"/>.
/// </summary>
/// <param name="VentaId">Id de la venta creada.</param>
/// <param name="Total">Total cobrado, la suma de los subtotales netos.</param>
/// <param name="Lineas">Detalle de lo que salio del stock, linea por linea.</param>
/// <param name="Saldos">
/// Saldos que quedaron afectados, agrupados por producto. Va aparte de
/// <paramref name="Lineas"/> porque una venta puede tener dos lineas del mismo
/// producto y el saldo es uno solo.
/// </param>
public sealed record VentaRegistradaDto(
    int VentaId,
    decimal Total,
    IReadOnlyList<LineaVendidaDto> Lineas,
    IReadOnlyList<SaldoAfectadoDto> Saldos);
