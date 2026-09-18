namespace Colorsin.Application.Compras.DTOs;

/// <summary>
/// Peticion para crear una orden de compra. Nace siempre en estado
/// 'Pendiente': el stock no se mueve hasta confirmar la recepcion.
///
/// NO LLEVA UsuarioId, a proposito. Quien crea la orden es un parametro del
/// metodo y sale del token, no del cuerpo del JSON: una orden firmada a nombre
/// de otro es un problema de negocio, no solo de auditoria.
/// </summary>
/// <param name="ProveedorId">A quien se le pide.</param>
/// <param name="SucursalId">Sede que recibira la mercancia.</param>
/// <param name="PlazoPagoDias">Dias de credito. Cero es contado. No admite negativos.</param>
/// <param name="Lineas">Lineas de la orden. Debe traer al menos una.</param>
public sealed record CrearOrdenCompraDto(
    int ProveedorId,
    int SucursalId,
    int? PlazoPagoDias,
    IReadOnlyList<CrearLineaOrdenCompraDto> Lineas);

/// <summary>Linea de una orden de compra que se esta creando.</summary>
/// <param name="ProductoId">Producto a pedir.</param>
/// <param name="Cantidad">
/// Cantidad a pedir, en <paramref name="UnidadId"/>. Debe ser mayor que cero;
/// tambien lo exige el CHECK `chk_ocd_cantidad`.
/// </param>
/// <param name="UnidadId">
/// Unidad de compra. Puede diferir de la unidad base del producto: se compra
/// por canecas y se lleva el stock en litros. La conversion se hace al recibir.
/// </param>
/// <param name="PrecioUnitario">Precio por unidad de compra. No admite negativos.</param>
/// <param name="Descuento">
/// Descuento en PORCENTAJE, de 0 a 100. No es un importe: el CHECK
/// `chk_ocd_descuento` acota el rango.
/// </param>
public sealed record CrearLineaOrdenCompraDto(
    int ProductoId,
    decimal Cantidad,
    int UnidadId,
    decimal? PrecioUnitario = null,
    decimal Descuento = 0m);
