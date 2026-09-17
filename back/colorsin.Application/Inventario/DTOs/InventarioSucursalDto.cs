namespace Colorsin.Application.Inventario.DTOs;

/// <summary>
/// Saldo de un producto en una sede. Las tres cantidades van en la unidad base
/// del producto, con 4 decimales, igual que las columnas.
/// </summary>
/// <param name="Id">Clave primaria de la fila de saldo.</param>
/// <param name="SucursalId">Sede.</param>
/// <param name="SucursalNombre">Nombre de la sede.</param>
/// <param name="ProductoId">Producto.</param>
/// <param name="ProductoNombre">Nombre del producto.</param>
/// <param name="UnidadBaseSimbolo">Unidad en que estan expresadas las cantidades.</param>
/// <param name="CantidadBase">Saldo actual.</param>
/// <param name="StockMinimo">Umbral de reposicion.</param>
/// <param name="CostoPromedio">Costo promedio ponderado por unidad base.</param>
/// <param name="EnAlerta">
/// Atajo de <c>CantidadBase &lt;= StockMinimo</c>, calculado aqui para que la
/// interfaz no repita el criterio y se desincronice del que usa la consulta de
/// alertas.
/// </param>
public sealed record InventarioSucursalDto(
    int Id,
    int SucursalId,
    string SucursalNombre,
    int ProductoId,
    string ProductoNombre,
    string? UnidadBaseSimbolo,
    decimal CantidadBase,
    decimal StockMinimo,
    decimal CostoPromedio,
    bool EnAlerta);
