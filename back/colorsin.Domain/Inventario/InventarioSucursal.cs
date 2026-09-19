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

    /// <summary>
    /// Baja logica. <c>false</c> saca la existencia del listado y de las alertas,
    /// pero conserva el saldo, los lotes y todo el libro mayor que la referencia.
    ///
    /// NO ES "sin stock": un saldo en cero sigue activo -el producto se maneja en
    /// esa sede, simplemente se agoto- mientras que uno inactivo dice que esa
    /// sede dejo de manejar el producto.
    /// </summary>
    public bool Activo { get; set; } = true;

    // --- Navegacion ---
    public Sucursal Sucursal { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}
