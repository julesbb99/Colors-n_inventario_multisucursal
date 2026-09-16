using Colorsin.Domain.Inventario;

namespace Colorsin.Domain.Ventas;

/// <summary>Linea de una venta.</summary>
public class VentaDetalle
{
    public int Id { get; set; }
    public int VentaId { get; set; }
    public int ProductoId { get; set; }

    /// <summary>
    /// Cantidad vendida, expresada en <see cref="UnidadId"/>.
    /// En la base tiene solo 2 decimales, a diferencia del resto del modelo.
    /// </summary>
    public decimal? Cantidad { get; set; }

    /// <summary>Unidad de venta; puede diferir de la unidad base del producto.</summary>
    public int UnidadId { get; set; }

    public decimal? PrecioUnitario { get; set; }

    /// <summary>Descuento en PORCENTAJE, de 0 a 100.</summary>
    public decimal Descuento { get; set; }

    // --- Navegacion ---
    public Venta Venta { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
    public UnidadMedida Unidad { get; set; } = null!;
}
