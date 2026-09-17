using Colorsin.Domain.Comun;

namespace Colorsin.Application.Comun.Repositories;

/// <summary>
/// Acceso de lectura a las sedes de la red.
///
/// Devuelve entidades de dominio, no DTOs: quien traduce a DTO es el servicio
/// de aplicacion. Asi la misma consulta sirve para la API y para las reglas de
/// negocio que vendran despues (inventario, transferencias), que necesitan la
/// entidad completa.
/// </summary>
public interface ISucursalRepository
{
    /// <summary>Todas las sedes, ordenadas por nombre. Lista vacia si no hay ninguna.</summary>
    Task<IReadOnlyList<Sucursal>> ObtenerTodasAsync(CancellationToken cancellationToken = default);

    /// <summary>La sede con ese id, o <c>null</c> si no existe.</summary>
    Task<Sucursal?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);
}
