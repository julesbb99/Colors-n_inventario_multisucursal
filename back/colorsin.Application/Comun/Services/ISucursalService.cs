using Colorsin.Application.Comun.DTOs;

namespace Colorsin.Application.Comun.Services;

/// <summary>Consultas del catalogo de sedes.</summary>
public interface ISucursalService
{
    Task<IReadOnlyList<SucursalDto>> ListarAsync(CancellationToken cancellationToken = default);

    /// <summary>La sede con ese id, o <c>null</c> si no existe.</summary>
    Task<SucursalDto?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);
}
