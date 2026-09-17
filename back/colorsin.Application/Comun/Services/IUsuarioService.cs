using Colorsin.Application.Comun.DTOs;

namespace Colorsin.Application.Comun.Services;

/// <summary>Consultas sobre los usuarios del sistema.</summary>
public interface IUsuarioService
{
    /// <summary>
    /// Usuarios de toda la red, o solo los de una sede si se pasa
    /// <paramref name="sucursalId"/>.
    /// </summary>
    Task<IReadOnlyList<UsuarioDto>> ListarAsync(
        int? sucursalId = null,
        CancellationToken cancellationToken = default);

    /// <summary>El usuario con ese id, o <c>null</c> si no existe.</summary>
    Task<UsuarioDto?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);
}
