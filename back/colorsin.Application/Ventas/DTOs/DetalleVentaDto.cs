namespace Colorsin.Application.Ventas.DTOs;

/// <summary>
/// Linea de una venta.
///
/// Los importes se derivan en cada lectura en vez de guardarse, igual que en
/// compras: una columna `subtotal` almacenada se queda vieja en cuanto alguien
/// corrige el precio de la linea.
/// </summary>
/// <param name="Id">Clave primaria de la linea.</param>
/// <param name="VentaId">Venta a la que pertenece.</param>
/// <param name="ProductoId">Producto vendido.</param>
/// <param name="ProductoNombre">Nombre del producto.</param>
/// <param name="Cantidad">
/// Cantidad vendida, en <paramref name="UnidadId"/>.
///
/// OJO: la columna es DECIMAL(12,2), con solo 2 decimales, a diferencia del
/// resto del modelo, que usa 4. El servicio redondea a 2 ANTES de convertir a
/// litros, para que lo descontado del stock derive del mismo numero que queda
/// almacenado.
/// </param>
/// <param name="UnidadId">Unidad de venta, que puede no ser la unidad base del producto.</param>
/// <param name="UnidadSimbolo">Abreviatura de esa unidad.</param>
/// <param name="PrecioUnitario">Precio por unidad de venta, antes de descuento.</param>
/// <param name="Descuento">Descuento en PORCENTAJE, de 0 a 100. No es un importe.</param>
/// <param name="SubtotalBruto">Cantidad x precio, sin descuento.</param>
/// <param name="ValorDescuento">Lo que rebaja el descuento.</param>
/// <param name="SubtotalNeto">Lo que se cobra por esta linea.</param>
public sealed record DetalleVentaDto(
    int Id,
    int VentaId,
    int ProductoId,
    string ProductoNombre,
    decimal? Cantidad,
    int UnidadId,
    string UnidadSimbolo,
    decimal? PrecioUnitario,
    decimal Descuento,
    decimal SubtotalBruto,
    decimal ValorDescuento,
    decimal SubtotalNeto);
