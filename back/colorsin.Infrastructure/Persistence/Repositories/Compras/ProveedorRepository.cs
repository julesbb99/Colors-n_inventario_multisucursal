using Colorsin.Application.Compras.Repositories;
using Colorsin.Domain.Compras;
using Microsoft.EntityFrameworkCore;

namespace Colorsin.Infrastructure.Persistence.Repositories.Compras;

/// <inheritdoc cref="IProveedorRepository"/>
public sealed class ProveedorRepository : IProveedorRepository
{
    private readonly AppDbContext _db;

    public ProveedorRepository(AppDbContext db)
    {
        _db = db;
    }

    // Include(Productos) porque el DTO cuenta cuantos productos surte cada
    // proveedor. Sin el, la coleccion llega vacia y el conteo saldria cero
    // para todos, que es peor que no traerlo: seria un dato falso.
    public async Task<IReadOnlyList<Proveedor>> ObtenerTodosAsync(
        CancellationToken cancellationToken = default) =>
        await _db.Proveedores
            .AsNoTracking()
            .Include(p => p.Productos)
            .OrderBy(p => p.Nombre)
            .ToListAsync(cancellationToken);

    public Task<Proveedor?> ObtenerPorIdAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        _db.Proveedores
            .AsNoTracking()
            .Include(p => p.Productos)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    // AnyAsync y no ObtenerPorIdAsync: se traduce a un EXISTS, que no trae
    // ninguna fila ni recorre la tabla puente.
    public Task<bool> ExisteAsync(int id, CancellationToken cancellationToken = default) =>
        _db.Proveedores.AnyAsync(p => p.Id == id, cancellationToken);
}
