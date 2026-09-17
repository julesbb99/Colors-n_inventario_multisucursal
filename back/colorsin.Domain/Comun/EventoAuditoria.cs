namespace Colorsin.Domain.Comun;

/// <summary>
/// Registro de trazabilidad de un cambio critico, transversal a todos los
/// modulos. Es un libro de solo-anexar: se inserta y nunca se modifica ni se
/// borra.
///
/// No sustituye a <c>MovimientoInventario</c>. Ese es el libro mayor del stock
/// y responde "cuanto habia"; este responde "quien hizo que y cuando", incluso
/// para acciones que no mueven cantidades (ajustar un lote, cambiar un stock
/// minimo, anular una orden).
///
/// OJO con el alcance de "inmutable": hoy lo garantiza la aplicacion, que no
/// expone ninguna forma de editar ni borrar eventos. La base todavia no lo
/// refuerza. Ver la migracion AuditoriaYTrazabilidadLote: los triggers que lo
/// harian cumplir chocan con el ERROR 1419 de MySQL y necesitan un cambio de
/// configuracion del servidor.
/// </summary>
public class EventoAuditoria
{
    /// <summary>
    /// BIGINT y no INT: esta tabla crece con cada operacion del sistema, no
    /// con cada producto. 2.147 millones de filas se alcanzan.
    /// </summary>
    public long Id { get; set; }

    /// <summary>Modulo que origino el evento: 'Inventario', 'Compras', 'Ventas'.</summary>
    public string Modulo { get; set; } = null!;

    /// <summary>Accion ejecutada: 'RegistrarMovimiento', 'AjustarLote'.</summary>
    public string Accion { get; set; } = null!;

    /// <summary>Responsable. Con FK a usuarios: no se audita a nombre de un id inexistente.</summary>
    public int UsuarioId { get; set; }

    /// <summary>Texto libre con el contexto del cambio (ids, cantidades, saldo resultante).</summary>
    public string? Detalle { get; set; }

    /// <summary>Momento del evento. Lo pone la base con CURRENT_TIMESTAMP.</summary>
    public DateTime Fecha { get; set; }

    // --- Navegacion ---
    public Usuario Usuario { get; set; } = null!;
}
