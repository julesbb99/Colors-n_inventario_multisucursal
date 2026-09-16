using Colorsin.Domain.Comun;

namespace Colorsin.Domain.Inventario;

/// <summary>
/// Lote de un producto en una sede. Da trazabilidad y soporta el control
/// FEFO (First Expired, First Out) para productos que caducan.
/// </summary>
public class Lote
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public int SucursalId { get; set; }
    public string NumeroLote { get; set; } = null!;
    public DateOnly? FechaVencimiento { get; set; }

    /// <summary>Cantidad del lote, en unidad base del producto.</summary>
    public decimal? CantidadBase { get; set; }

    public DateTime? FechaIngreso { get; set; }

    // --- Navegacion ---
    public Producto Producto { get; set; } = null!;
    public Sucursal Sucursal { get; set; } = null!;
}
