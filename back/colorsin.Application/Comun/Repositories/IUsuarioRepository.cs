using Colorsin.Domain.Comun;

namespace Colorsin.Application.Comun.Repositories;

/// <summary>Acceso de lectura a los usuarios del sistema.</summary>
public interface IUsuarioRepository
{
    /// <summary>
    /// Todos los usuarios, ordenados por nombre.
    /// Trae cargada la sede (<c>Usuario.Sucursal</c>) para que el mapeo a DTO
    /// no dispare una consulta por fila.
    /// </summary>
    Task<IReadOnlyList<Usuario>> ObtenerTodosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Usuarios de una sede concreta. Sirve para que un Gerente de Sucursal
    /// vea solo su equipo y no toda la red.
    /// </summary>
    Task<IReadOnlyList<Usuario>> ObtenerPorSucursalAsync(
        int sucursalId,
        CancellationToken cancellationToken = default);

    /// <summary>El usuario con ese id, o <c>null</c> si no existe. Incluye la sede.</summary>
    Task<Usuario?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);
}
