namespace Colorsin.Application.Ventas.DTOs;

/// <summary>
/// Peticion para registrar una venta.
///
/// Una venta se crea ya concretada: no hay estado intermedio. En el momento en
/// que se registra se descuenta el stock de la sede, se descuentan los lotes y
/// se anexan los movimientos al libro mayor, todo en una transaccion.
///
/// NO LLEVA UsuarioId, a proposito. Quien vende es un parametro del metodo y
/// sale del token, no del cuerpo del JSON. Una venta registrada a nombre de otro
/// no es solo un rastro falso: es la comision, el cuadre de caja y la
/// responsabilidad sobre el stock que salio, todo apuntando a quien no fue.
/// </summary>
/// <param name="ClienteId">A quien se le vende.</param>
/// <param name="SucursalId">
/// Sede que despacha. Es la sede de cuyo saldo se descuenta, y contra la que se
/// valida el stock disponible.
/// </param>
/// <param name="Lineas">Lineas de la venta. Debe traer al menos una.</param>
/// <param name="Observaciones">Nota que se copia a cada movimiento generado.</param>
public sealed record CrearVentaDto(
    int ClienteId,
    int SucursalId,
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
