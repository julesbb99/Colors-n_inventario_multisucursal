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
///
/// OJO: una existencia DESHABILITADA nunca esta en alerta, por mas que su saldo
/// este bajo el minimo. Avisar de que hay que reponer un producto que la sede
/// decidio dejar de manejar es ruido, y ademas la consulta de alertas ya no la
/// devuelve: si aqui dijera lo contrario, las dos pantallas se contradirian.
/// </param>
/// <param name="Activo">
/// <c>false</c> si la existencia esta dada de baja logica. Sigue en la base con
/// su saldo y su historia, pero no se lista ni alerta.
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
    bool EnAlerta,
    bool Activo);
