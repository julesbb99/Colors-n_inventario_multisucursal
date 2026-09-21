using Colorsin.Domain.Comun;

namespace Colorsin.Domain.Transferencias;

/// <summary>
/// Incidencia reportada sobre un traslado: faltante, averia, sobrante o
/// retraso. Puede derivar en reclamo a la transportadora, por eso guarda
/// quien la reporto.
///
/// TIENE CICLO DE VIDA PROPIO, y antes no lo tenia. Reportar el hallazgo es
/// solo la mitad: la otra es que se hizo con el, y eso puede tardar dias
/// -esperar el reenvio, pelear la reclamacion-. Mientras tanto la novedad esta
/// <see cref="EstadoNovedad.Abierta"/> y el traslado no puede darse por
/// terminado.
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

    /// <summary>
    /// Que se decidio hacer con lo reportado.
    ///
    /// <see cref="TratamientoNovedad.Reenvio"/> y
    /// <see cref="TratamientoNovedad.Reclamacion"/> dejan la novedad abierta;
    /// los otros dos la cierran en el acto.
    /// </summary>
    public TratamientoNovedad Tratamiento { get; set; } = TratamientoNovedad.Ninguno;

    /// <summary>
    /// Si sigue esperando desenlace. La base impide que este
    /// <see cref="EstadoNovedad.Abierta"/> sin un tratamiento pendiente.
    /// </summary>
    public EstadoNovedad Estado { get; set; } = EstadoNovedad.Cerrada;

    /// <summary>
    /// El porque del cierre. OBLIGATORIO al cerrar.
    ///
    /// Es lo unico que queda para entender, dentro de seis meses, por que aquel
    /// faltante no se le cobro a nadie. Sin el, cerrar una novedad seria
    /// borrarla en la practica.
    /// </summary>
    public string? MotivoCierre { get; set; }

    public DateTime? FechaCierre { get; set; }

    /// <summary>Quien la cerro. Distinto de quien la reporto: suele pasar tiempo.</summary>
    public int? UsuarioCierreId { get; set; }

    // --- Navegacion ---
    public Transferencia Transferencia { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
    public Usuario? UsuarioCierre { get; set; }
}
