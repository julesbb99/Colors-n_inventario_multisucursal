using Colorsin.Domain.Comun;
using Colorsin.Domain.Inventario;

namespace Colorsin.Domain.Transferencias;

/// <summary>
/// Traslado de un producto entre dos sedes de la red. Origen y destino
/// apuntan a la misma tabla, y la base impide que sean iguales.
/// </summary>
public class Transferencia
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public int SucursalOrigenId { get; set; }
    public int SucursalDestinoId { get; set; }
    public int TransportadoraId { get; set; }

    /// <summary>Cantidad pedida, expresada en <see cref="UnidadId"/>.</summary>
    public decimal? CantidadSolicitada { get; set; }

    /// <summary>
    /// Nulo hasta que la sede destino confirma. Comparada con
    /// <see cref="CantidadSolicitada"/> distingue recepcion completa de parcial.
    /// </summary>
    public decimal? CantidadRecibida { get; set; }

    /// <summary>
    /// Unidad del traslado: se puede pedir en galones aunque el stock se
    /// lleve en litros.
    /// </summary>
    public int UnidadId { get; set; }

    public EstadoTransferencia? Estado { get; set; }
    public Urgencia? Urgencia { get; set; }
    public DateTime? FechaSolicitud { get; set; }
    public DateTime? FechaEstimadaLlegada { get; set; }

    // --- Navegacion ---
    public Producto Producto { get; set; } = null!;
    public Sucursal SucursalOrigen { get; set; } = null!;
    public Sucursal SucursalDestino { get; set; } = null!;
    public Transportadora Transportadora { get; set; } = null!;
    public UnidadMedida Unidad { get; set; } = null!;
    public ICollection<NovedadTransferencia> Novedades { get; set; } = new List<NovedadTransferencia>();
}
