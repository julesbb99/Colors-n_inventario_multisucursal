using Colorsin.Application.Comun.Auditoria;
using Colorsin.Domain.Comun;
using Colorsin.Infrastructure.Persistence;

namespace Colorsin.Infrastructure.Auditoria;

/// <inheritdoc cref="IAuditoriaService"/>
public sealed class AuditoriaService : IAuditoriaService
{
    /// <summary>Tope de `auditoria_eventos.detalle` en la base: VARCHAR(1000).</summary>
    private const int MaximoDetalle = 1000;

    private readonly AppDbContext _db;

    public AuditoriaService(AppDbContext db)
    {
        _db = db;
    }

    public Task RegistrarEventoAsync(
        string modulo,
        string accion,
        int usuarioId,
        string detalle,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modulo);
        ArgumentException.ThrowIfNullOrWhiteSpace(accion);

        _db.EventosAuditoria.Add(new EventoAuditoria
        {
            Modulo = modulo,
            Accion = accion,
            UsuarioId = usuarioId,
            Detalle = Recortar(detalle)
            // Fecha la pone la base con CURRENT_TIMESTAMP: una bitacora fechada
            // por el reloj de cada maquina que corra la API no sirve para
            // ordenar eventos entre si.
        });

        // Se deja PREPARADO, no se guarda. Quien llama confirma junto con el
        // cambio que esta auditando, dentro de su misma transaccion. Si aqui se
        // llamara a SaveChanges, el evento se confirmaria antes que la
        // operacion y podria quedar registrando algo que luego se revierte.
        return Task.CompletedTask;
    }

    /// <summary>
    /// Recorta el detalle al ancho de la columna.
    ///
    /// Se prefiere un detalle truncado a una excepcion: la auditoria viaja en la
    /// misma transaccion que la operacion auditada, asi que un detalle
    /// demasiado largo tumbaria el movimiento de inventario que lo origino. La
    /// marca final deja claro que falta texto.
    /// </summary>
    private static string? Recortar(string? detalle)
    {
        if (string.IsNullOrEmpty(detalle) || detalle.Length <= MaximoDetalle)
        {
            return detalle;
        }

        const string marca = "...[recortado]";
        return string.Concat(detalle.AsSpan(0, MaximoDetalle - marca.Length), marca);
    }
}
