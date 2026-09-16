using Colorsin.Domain.Comun;

namespace Colorsin.Domain.Transferencias;

/// <summary>
/// Incidencia reportada sobre un traslado: faltante, averia, sobrante o
/// retraso. Puede derivar en reclamo a la transportadora, por eso guarda
/// quien la reporto.
/// </summary>
public class NovedadTransferencia
{
    public int Id { get; set; }
    public int TransferenciaId { get; set; }
    public int UsuarioId { get; set; }
    public TipoNovedad? Tipo { get; set; }
    public decimal? CantidadAfectada { get; set; }
    public string? Observaciones { get; set; }
    public DateTime? Fecha { get; set; }

    // --- Navegacion ---
    public Transferencia Transferencia { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}
