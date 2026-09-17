using Colorsin.Domain.Ventas;

namespace Colorsin.Application.Ventas.Repositories;

/// <summary>Acceso de lectura al catalogo de clientes.</summary>
public interface IClienteRepository
{
    /// <summary>Todos los clientes, ordenados por razon social.</summary>
    Task<IReadOnlyList<Cliente>> ObtenerTodosAsync(CancellationToken cancellationToken = default);

    /// <summary>El cliente con ese id, o <c>null</c> si no existe.</summary>
    Task<Cliente?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// El cliente con ese documento (cedula o NIT), o <c>null</c>. El documento
    /// es unico en la base, asi que devuelve como maximo uno.
    /// </summary>
    Task<Cliente?> ObtenerPorDocumentoAsync(
        string documento,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Si existe un cliente con ese id, sin traer la fila. Se usa al validar
    /// una venta, donde solo hace falta saber si esta o no.
    /// </summary>
    Task<bool> ExisteAsync(int id, CancellationToken cancellationToken = default);
}
