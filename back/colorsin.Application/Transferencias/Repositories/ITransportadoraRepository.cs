using Colorsin.Domain.Transferencias;

namespace Colorsin.Application.Transferencias.Repositories;

/// <summary>Acceso de lectura al catalogo de transportadoras.</summary>
public interface ITransportadoraRepository
{
    /// <summary>Todas las transportadoras, ordenadas por nombre.</summary>
    Task<IReadOnlyList<Transportadora>> ObtenerTodasAsync(
        CancellationToken cancellationToken = default);

    /// <summary>La transportadora con ese id, o <c>null</c> si no existe.</summary>
    Task<Transportadora?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Si existe una transportadora con ese id, sin traer la fila. Se usa al
    /// despachar, donde solo hace falta saber si esta o no.
    /// </summary>
    Task<bool> ExisteAsync(int id, CancellationToken cancellationToken = default);
}
