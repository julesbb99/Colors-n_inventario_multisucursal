namespace Colorsin.Domain.Compras;

/// <summary>Empresa que suministra materia prima o producto terminado.</summary>
public class Proveedor
{
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;
    public string? Contacto { get; set; }
    public string Telefono { get; set; } = null!;

    // --- Navegacion ---
    public ICollection<ProductoProveedor> Productos { get; set; } = new List<ProductoProveedor>();
    public ICollection<OrdenCompra> OrdenesCompra { get; set; } = new List<OrdenCompra>();
}
