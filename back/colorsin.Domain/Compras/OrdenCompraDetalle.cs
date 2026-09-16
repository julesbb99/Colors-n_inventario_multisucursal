using Colorsin.Domain.Inventario;

namespace Colorsin.Domain.Compras;

/// <summary>Linea de una orden de compra.</summary>
public class OrdenCompraDetalle
{
    public int Id { get; set; }
    public int OrdenCompraId { get; set; }
    public int ProductoId { get; set; }

    /// <summary>Cantidad pedida, expresada en <see cref="UnidadId"/>.</summary>
    public decimal? Cantidad { get; set; }

    /// <summary>Unidad de compra; puede diferir de la unidad base del producto.</summary>
    public int UnidadId { get; set; }

    public decimal? PrecioUnitario { get; set; }

    /// <summary>Descuento en PORCENTAJE, de 0 a 100.</summary>
    public decimal Descuento { get; set; }

    // --- Navegacion ---
    public OrdenCompra OrdenCompra { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
    public UnidadMedida Unidad { get; set; } = null!;
}
