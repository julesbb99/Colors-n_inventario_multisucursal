using Colorsin.Domain.Inventario;

namespace Colorsin.Domain.Compras;

/// <summary>
/// Tabla puente: que proveedores surten cada producto y a que precio de
/// referencia. La pareja (producto, proveedor) no se repite.
/// </summary>
public class ProductoProveedor
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public int ProveedorId { get; set; }

    /// <summary>Precio orientativo para cotizar, por unidad base del producto.</summary>
    public decimal? PrecioReferencia { get; set; }

    // --- Navegacion ---
    public Producto Producto { get; set; } = null!;
    public Proveedor Proveedor { get; set; } = null!;
}
