using Colorsin.Domain.Comun;

namespace Colorsin.Domain.Compras;

/// <summary>Encabezado del pedido a un proveedor para reabastecer una sede.</summary>
public class OrdenCompra
{
    public int Id { get; set; }
    public int ProveedorId { get; set; }
    public int SucursalId { get; set; }

    /// <summary>
    /// Quien creo la orden. Obligatorio: una compra siempre tiene un
    /// responsable, y hasta ahora ese dato solo sobrevivia en la bitacora de
    /// auditoria, donde no se puede consultar desde la orden.
    /// </summary>
    public int UsuarioId { get; set; }

    public DateTime? Fecha { get; set; }
    public EstadoOrdenCompra? Estado { get; set; }

    /// <summary>Dias de credito pactados. Cero significa contado.</summary>
    public int? PlazoPagoDias { get; set; }

    // --- Navegacion ---
    public Proveedor Proveedor { get; set; } = null!;
    public Sucursal Sucursal { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
    public ICollection<OrdenCompraDetalle> Detalles { get; set; } = new List<OrdenCompraDetalle>();
}
