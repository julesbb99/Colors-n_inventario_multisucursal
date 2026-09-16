using Colorsin.Domain.Compras;
using Colorsin.Domain.Transferencias;
using Colorsin.Domain.Ventas;

namespace Colorsin.Domain.Inventario;

/// <summary>Producto del catalogo: pinturas, disolventes e insumos.</summary>
public class Producto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;
    public string? Categoria { get; set; }
    public string? Descripcion { get; set; }

    /// <summary>Unidad en la que se lleva el stock de este producto.</summary>
    public int? UnidadBaseId { get; set; }

    // --- Navegacion ---
    public UnidadMedida? UnidadBase { get; set; }
    public ICollection<InventarioSucursal> Inventarios { get; set; } = new List<InventarioSucursal>();
    public ICollection<Lote> Lotes { get; set; } = new List<Lote>();
    public ICollection<MovimientoInventario> Movimientos { get; set; } = new List<MovimientoInventario>();
    public ICollection<ProductoProveedor> Proveedores { get; set; } = new List<ProductoProveedor>();
    public ICollection<OrdenCompraDetalle> LineasCompra { get; set; } = new List<OrdenCompraDetalle>();
    public ICollection<VentaDetalle> LineasVenta { get; set; } = new List<VentaDetalle>();
    public ICollection<Transferencia> Transferencias { get; set; } = new List<Transferencia>();
}
