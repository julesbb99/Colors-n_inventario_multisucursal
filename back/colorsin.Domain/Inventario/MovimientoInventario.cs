using Colorsin.Domain.Comun;

namespace Colorsin.Domain.Inventario;

/// <summary>
/// Libro mayor del inventario: una fila por cada entrada o salida de stock.
/// El saldo de <see cref="InventarioSucursal"/> deberia poder reconstruirse
/// sumando esta tabla.
/// </summary>
public class MovimientoInventario
{
    public int Id { get; set; }
    public int SucursalId { get; set; }
    public int ProductoId { get; set; }

    /// <summary>Quien registro el movimiento. Es el rastro de auditoria.</summary>
    public int UsuarioId { get; set; }

    public TipoMovimiento? Tipo { get; set; }
    public MotivoMovimiento? Motivo { get; set; }

    /// <summary>
    /// Cantidad tal como la digito el operario, en <see cref="UnidadId"/>.
    /// Siempre positiva: el signo lo aporta <see cref="Tipo"/>.
    /// </summary>
    public decimal? Cantidad { get; set; }

    /// <summary>Unidad que uso el operario, no necesariamente la unidad base.</summary>
    public int UnidadId { get; set; }

    /// <summary>La misma cantidad convertida a la unidad base del producto.</summary>
    public decimal? CantidadBase { get; set; }

    /// <summary>
    /// Lote afectado, cuando el movimiento se imputa a uno concreto.
    /// Nulo en movimientos que no discriminan lote (un ajuste global, por
    /// ejemplo). Es lo que permite la trazabilidad FEFO de punta a punta.
    /// </summary>
    public int? LoteId { get; set; }

    /// <summary>Nota del operario sobre el movimiento.</summary>
    public string? Observaciones { get; set; }

    public DateTime? Fecha { get; set; }

    // --- Navegacion ---
    public Sucursal Sucursal { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
    public UnidadMedida Unidad { get; set; } = null!;
    public Lote? Lote { get; set; }
}
