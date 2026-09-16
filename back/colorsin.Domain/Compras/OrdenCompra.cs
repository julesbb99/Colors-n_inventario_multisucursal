using Colorsin.Domain.Comun;

namespace Colorsin.Domain.Compras;

/// <summary>Encabezado del pedido a un proveedor para reabastecer una sede.</summary>
public class OrdenCompra
{
    public int Id { get; set; }
    public int ProveedorId { get; set; }
    public int SucursalId { get; set; }
    public DateTime? Fecha { get; set; }
    public EstadoOrdenCompra? Estado { get; set; }

    /// <summary>Dias de credito pactados. Cero significa contado.</summary>
    public int? PlazoPagoDias { get; set; }

    // --- Navegacion ---
    public Proveedor Proveedor { get; set; } = null!;
    public Sucursal Sucursal { get; set; } = null!;
    public ICollection<OrdenCompraDetalle> Detalles { get; set; } = new List<OrdenCompraDetalle>();
}
