namespace Colorsin.Application.Compras.DTOs;

/// <summary>
/// Peticion para crear una orden de compra. Nace siempre en estado
/// 'Pendiente': el stock no se mueve hasta confirmar la recepcion.
/// </summary>
/// <param name="ProveedorId">A quien se le pide.</param>
/// <param name="SucursalId">Sede que recibira la mercancia.</param>
/// <param name="UsuarioId">
/// Quien crea la orden. Obligatorio: se guarda en `ordenes_compra.usuario_id`
/// y ademas queda en el evento de auditoria. Debe existir en `usuarios`; la FK
/// `fk_oc_usuario` lo impone.
/// </param>
/// <param name="PlazoPagoDias">Dias de credito. Cero es contado. No admite negativos.</param>
/// <param name="Lineas">Lineas de la orden. Debe traer al menos una.</param>
public sealed record CrearOrdenCompraDto(
    int ProveedorId,
    int SucursalId,
    int UsuarioId,
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
