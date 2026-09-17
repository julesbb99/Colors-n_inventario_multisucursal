using Colorsin.Application.Comun.Repositories;
using Colorsin.Domain.Comun;
using Microsoft.EntityFrameworkCore;

namespace Colorsin.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IUsuarioRepository"/>
public sealed class UsuarioRepository : IUsuarioRepository
{
    private readonly AppDbContext _db;

    public UsuarioRepository(AppDbContext db)
    {
        _db = db;
    }

    // Include(Sucursal) en las tres consultas porque el DTO expone el nombre de
    // la sede. Sin el, con lazy loading apagado el nombre saldria siempre nulo;
    // y si algun dia se enciende, seria una consulta por usuario (N+1).
    // Es un LEFT JOIN: SucursalId es opcional en el Administrador General.
    public async Task<IReadOnlyList<Usuario>> ObtenerTodosAsync(
        CancellationToken cancellationToken = default) =>
        await _db.Usuarios
            .AsNoTracking()
            .Include(u => u.Sucursal)
            .OrderBy(u => u.Nombre)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Usuario>> ObtenerPorSucursalAsync(
        int sucursalId,
        CancellationToken cancellationToken = default) =>
        await _db.Usuarios
            .AsNoTracking()
            .Include(u => u.Sucursal)
            .Where(u => u.SucursalId == sucursalId)
            .OrderBy(u => u.Nombre)
            .ToListAsync(cancellationToken);

    public Task<Usuario?> ObtenerPorIdAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        _db.Usuarios
            .AsNoTracking()
            .Include(u => u.Sucursal)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
}
