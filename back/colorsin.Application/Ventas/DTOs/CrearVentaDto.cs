namespace Colorsin.Application.Ventas.DTOs;

/// <summary>
/// Peticion para registrar una venta.
///
/// Una venta se crea ya concretada: no hay estado intermedio. En el momento en
/// que se registra se descuenta el stock de la sede, se descuentan los lotes y
/// se anexan los movimientos al libro mayor, todo en una transaccion.
/// </summary>
/// <param name="ClienteId">A quien se le vende.</param>
/// <param name="SucursalId">
/// Sede que despacha. Es la sede de cuyo saldo se descuenta, y contra la que se
/// valida el stock disponible.
/// </param>
/// <param name="UsuarioId">
/// Quien registra la venta. Queda en `ventas.usuario_id`, en cada movimiento de
/// inventario y en la auditoria. Debe existir en `usuarios`: las tres tablas
/// tienen FK obligatoria.
/// </param>
/// <param name="Lineas">Lineas de la venta. Debe traer al menos una.</param>
/// <param name="Observaciones">Nota que se copia a cada movimiento generado.</param>
public sealed record CrearVentaDto(
    int ClienteId,
    int SucursalId,
    int UsuarioId,
    IReadOnlyList<CrearLineaVentaDto> Lineas,
    string? Observaciones = null);

/// <summary>Linea de una venta que se esta registrando.</summary>
/// <param name="ProductoId">Producto a vender.</param>
/// <param name="Cantidad">
/// Cantidad a vender, en <paramref name="UnidadId"/>. Debe ser mayor que cero;
/// tambien lo exige el CHECK `chk_vd_cantidad`.
///
/// Se redondea a 2 decimales antes de guardar y de convertir, porque es lo que
/// admite la columna `venta_detalle.cantidad`.
/// </param>
/// <param name="UnidadId">
/// Unidad de venta. Puede diferir de la unidad base del producto: se vende por
/// galones y el stock se lleva en litros. El servicio convierte.
/// </param>
/// <param name="PrecioUnitario">Precio por unidad de venta. No admite negativos.</param>
/// <param name="Descuento">
/// Descuento en PORCENTAJE, de 0 a 100. No es un importe: el CHECK
/// `chk_vd_descuento` acota el rango.
/// </param>
/// <param name="LoteId">
/// Lote del que despachar. Opcional.
///
/// Si se indica, se descuenta de ESE lote y se valida que alcance y que
/// pertenezca a la misma sede y producto. Si se omite, se aplica FEFO: se
/// consume de los lotes que vencen antes, y una sola linea puede repartirse
/// entre varios lotes, generando un movimiento por cada uno.
/// </param>
public sealed record CrearLineaVentaDto(
    int ProductoId,
    decimal Cantidad,
    int UnidadId,
    decimal? PrecioUnitario = null,
    decimal Descuento = 0m,
    int? LoteId = null);
