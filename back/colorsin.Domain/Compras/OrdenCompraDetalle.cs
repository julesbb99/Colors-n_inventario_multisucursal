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

    /// <summary>
    /// Cuanto se ha recibido de esta linea, acumulado entre todas las
    /// recepciones. Va en <see cref="UnidadId"/>, la misma unidad que
    /// <see cref="Cantidad"/>, para poder compararlas sin convertir.
    ///
    /// Arranca en cero y solo sube. Cuando iguala a <see cref="Cantidad"/> la
    /// linea esta completa; mientras sea menor, la orden queda
    /// ParcialmenteRecibida.
    /// </summary>
    public decimal CantidadRecibida { get; set; }

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
