using Colorsin.Domain.Comun;

namespace Colorsin.Domain.Inventario;

/// <summary>
/// Saldo consolidado de un producto en una sede. Una sola fila por pareja
/// (sucursal, producto): lo garantiza un indice unico en la base.
/// Todas las cantidades van en la unidad base del producto.
/// </summary>
public class InventarioSucursal
{
    public int Id { get; set; }
    public int SucursalId { get; set; }
    public int ProductoId { get; set; }

    /// <summary>Saldo actual, en unidad base.</summary>
    public decimal CantidadBase { get; set; }

    /// <summary>Umbral por debajo del cual se dispara la alerta de reposicion.</summary>
    public decimal StockMinimo { get; set; }

    /// <summary>Costo promedio ponderado por unidad base.</summary>
    public decimal CostoPromedio { get; set; }

    // --- Navegacion ---
    public Sucursal Sucursal { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}
