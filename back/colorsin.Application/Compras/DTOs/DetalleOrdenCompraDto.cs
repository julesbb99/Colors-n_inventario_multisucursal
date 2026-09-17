namespace Colorsin.Application.Compras.DTOs;

/// <summary>
/// Linea de una orden de compra.
///
/// Los importes se calculan aqui y no se guardan en la base: derivarlos en cada
/// lectura garantiza que nunca contradigan a cantidad, precio y descuento.
/// Una columna `subtotal` almacenada se queda vieja en cuanto alguien corrige
/// el precio de la linea.
/// </summary>
/// <param name="Id">Clave primaria de la linea.</param>
/// <param name="OrdenCompraId">Orden a la que pertenece.</param>
/// <param name="ProductoId">Producto pedido.</param>
/// <param name="ProductoNombre">Nombre del producto.</param>
/// <param name="Cantidad">Cantidad pedida, en <paramref name="UnidadId"/>.</param>
/// <param name="CantidadRecibida">
/// Cuanto se ha recibido en total, sumando todas las entregas. Misma unidad que
/// <paramref name="Cantidad"/>.
/// </param>
/// <param name="CantidadPendiente">
/// Lo que falta por llegar. Cero cuando la linea esta completa. Se calcula aqui
/// para que la interfaz no repita la resta y se desincronice del criterio que
/// usa el servicio para decidir el estado de la orden.
/// </param>
/// <param name="Completa"><c>true</c> cuando ya no falta nada de esta linea.</param>
/// <param name="UnidadId">Unidad de compra, que puede no ser la unidad base del producto.</param>
/// <param name="UnidadSimbolo">Abreviatura de esa unidad.</param>
/// <param name="PrecioUnitario">Precio por unidad de compra, antes de descuento.</param>
/// <param name="Descuento">Descuento en PORCENTAJE, de 0 a 100. No es un importe.</param>
/// <param name="SubtotalBruto">Cantidad x precio, sin descuento.</param>
/// <param name="ValorDescuento">Lo que rebaja el descuento.</param>
/// <param name="SubtotalNeto">Lo que se paga por esta linea.</param>
public sealed record DetalleOrdenCompraDto(
    int Id,
    int OrdenCompraId,
    int ProductoId,
    string ProductoNombre,
    decimal? Cantidad,
    decimal CantidadRecibida,
    decimal CantidadPendiente,
    bool Completa,
    int UnidadId,
    string UnidadSimbolo,
    decimal? PrecioUnitario,
    decimal Descuento,
    decimal SubtotalBruto,
    decimal ValorDescuento,
    decimal SubtotalNeto);
