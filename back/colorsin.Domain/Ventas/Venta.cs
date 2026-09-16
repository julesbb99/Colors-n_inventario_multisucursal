using Colorsin.Domain.Comun;

namespace Colorsin.Domain.Ventas;

/// <summary>Encabezado de una venta a un cliente.</summary>
public class Venta
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public int SucursalId { get; set; }

    /// <summary>Quien registro la venta. Rastro de auditoria de la operacion.</summary>
    public int UsuarioId { get; set; }

    public DateTime? Fecha { get; set; }

    /// <summary>
    /// Suma del detalle. Queda nulo mientras la venta se esta armando y se
    /// consolida al cerrarla.
    /// </summary>
    public decimal? Total { get; set; }

    // --- Navegacion ---
    public Cliente Cliente { get; set; } = null!;
    public Sucursal Sucursal { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
    public ICollection<VentaDetalle> Detalles { get; set; } = new List<VentaDetalle>();
}
