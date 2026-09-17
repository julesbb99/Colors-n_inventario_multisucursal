namespace Colorsin.Application.Transferencias.DTOs;

/// <summary>
/// Una linea del movimiento de stock que genero un traslado: cuanto salio o
/// entro, y de que lote.
///
/// OJO con el nombre: `transferencias` NO tiene tabla de detalle. Un traslado
/// mueve UN producto, no una lista. Lo que si tiene varias lineas es el
/// movimiento de inventario que lo respalda: FEFO puede repartir la cantidad
/// entre varios lotes, y cada reparto deja su propia fila en el libro mayor.
/// Eso es lo que representa este DTO, y es el detalle que de verdad hace falta
/// para reconstruir un traslado.
/// </summary>
/// <param name="MovimientoId">Fila del libro mayor.</param>
/// <param name="Tipo">'Ingreso' o 'Retiro'. El despacho genera retiros; la recepcion, ingresos.</param>
/// <param name="SucursalId">Sede afectada: el origen al despachar, el destino al recibir.</param>
/// <param name="SucursalNombre">Nombre de esa sede.</param>
/// <param name="LoteId">Lote afectado. Nulo si la cantidad salio sin lote asignado.</param>
/// <param name="NumeroLote">
/// Numero del lote. En la recepcion es el MISMO que en el despacho: el destino
/// recrea el lote con el numero y el vencimiento del origen para no perder la
/// trazabilidad del fabricante.
/// </param>
/// <param name="FechaVencimiento">Caducidad de ese lote.</param>
/// <param name="CantidadBase">Cantidad movida, en unidad base del producto.</param>
/// <param name="Fecha">Momento del movimiento.</param>
public sealed record DetalleTransferenciaDto(
    int MovimientoId,
    string? Tipo,
    int SucursalId,
    string SucursalNombre,
    int? LoteId,
    string? NumeroLote,
    DateOnly? FechaVencimiento,
    decimal? CantidadBase,
    DateTime? Fecha);
