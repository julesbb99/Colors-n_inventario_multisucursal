using Colorsin.Domain.Compras;
using Colorsin.Domain.Transferencias;
using Colorsin.Domain.Ventas;

namespace Colorsin.Domain.Inventario;

/// <summary>
/// Unidad de medida del catalogo. El litro es la unidad base del sistema
/// (factor 1.0); el resto se convierte contra el.
/// </summary>
public class UnidadMedida
{
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;
    public string Simbolo { get; set; } = null!;

    /// <summary>
    /// Cuantos litros equivalen a una unidad de esta medida (galon = 3.785410).
    /// Nulo en unidades que no son de volumen, como el kilogramo.
    /// Necesita 6 decimales: redondearlo desvia todas las conversiones.
    /// </summary>
    public decimal? FactorConversionLitros { get; set; }

    // --- Navegacion ---
    public ICollection<Producto> ProductosConUnidadBase { get; set; } = new List<Producto>();
    public ICollection<MovimientoInventario> Movimientos { get; set; } = new List<MovimientoInventario>();
    public ICollection<Transferencia> Transferencias { get; set; } = new List<Transferencia>();
    public ICollection<OrdenCompraDetalle> LineasCompra { get; set; } = new List<OrdenCompraDetalle>();
    public ICollection<VentaDetalle> LineasVenta { get; set; } = new List<VentaDetalle>();
}
