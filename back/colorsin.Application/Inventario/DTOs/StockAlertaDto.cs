namespace Colorsin.Application.Inventario.DTOs;

/// <summary>
/// Producto de una sede cuyo saldo cayo a o por debajo del minimo.
/// </summary>
/// <param name="ProductoId">Producto a reponer.</param>
/// <param name="NombreProducto">Nombre del producto.</param>
/// <param name="SucursalId">Sede donde falta.</param>
/// <param name="NombreSucursal">Nombre de la sede.</param>
/// <param name="CantidadBaseActual">Saldo actual, en unidad base.</param>
/// <param name="StockMinimo">Umbral configurado para ese producto en esa sede.</param>
/// <param name="UnidadBaseSimbolo">Unidad de las dos cantidades ('L', 'gal').</param>
/// <param name="Faltante">
/// Cuanto hay que reponer para volver al minimo (<c>StockMinimo - CantidadBaseActual</c>).
/// Es cero cuando el saldo esta justo en el umbral, que tambien dispara alerta.
/// </param>
public sealed record StockAlertaDto(
    int ProductoId,
    string NombreProducto,
    int SucursalId,
    string NombreSucursal,
    decimal CantidadBaseActual,
    decimal StockMinimo,
    string? UnidadBaseSimbolo,
    decimal Faltante);
