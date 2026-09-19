namespace Colorsin.Domain.Compras;

/// <summary>Empresa que suministra materia prima o producto terminado.</summary>
public class Proveedor
{
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;
    public string? Contacto { get; set; }
    public string Telefono { get; set; } = null!;

    /// <summary>
    /// Baja logica. <c>false</c> lo saca del selector de una orden nueva, pero
    /// conserva sus ordenes historicas y su lista de precios.
    ///
    /// No se borra nunca de verdad: `ordenes_compra` lo referencia con RESTRICT
    /// -MySQL rechazaria el borrado en cuanto tenga una orden- y
    /// `producto_proveedor` con CASCADE, que se llevaria su lista de precios por
    /// delante. Ver la migracion 13.
    /// </summary>
    public bool Activo { get; set; } = true;

    // --- Navegacion ---
    public ICollection<ProductoProveedor> Productos { get; set; } = new List<ProductoProveedor>();
    public ICollection<OrdenCompra> OrdenesCompra { get; set; } = new List<OrdenCompra>();
}
