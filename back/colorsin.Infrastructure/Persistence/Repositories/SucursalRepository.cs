using Colorsin.Application.Comun.Repositories;
using Colorsin.Domain.Comun;
using Microsoft.EntityFrameworkCore;

namespace Colorsin.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="ISucursalRepository"/>
public sealed class SucursalRepository : ISucursalRepository
{
    private readonly AppDbContext _db;

    public SucursalRepository(AppDbContext db)
    {
        _db = db;
    }

    // AsNoTracking en todas las consultas: son lecturas de catalogo, nadie va a
    // modificar estas entidades en la misma unidad de trabajo. Sin esto EF
    // guarda una copia de cada fila en el change tracker para nada.
    public async Task<IReadOnlyList<Sucursal>> ObtenerTodasAsync(
        CancellationToken cancellationToken = default) =>
        await _db.Sucursales
            .AsNoTracking()
            .OrderBy(s => s.Nombre)
            .ToListAsync(cancellationToken);

    public Task<Sucursal?> ObtenerPorIdAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        _db.Sucursales
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
}
