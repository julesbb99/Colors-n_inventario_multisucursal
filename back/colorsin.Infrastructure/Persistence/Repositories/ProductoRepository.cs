using Colorsin.Application.Inventario.Repositories;
using Colorsin.Domain.Inventario;
using Microsoft.EntityFrameworkCore;

namespace Colorsin.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IProductoRepository"/>
public sealed class ProductoRepository : IProductoRepository
{
    private readonly AppDbContext _db;

    public ProductoRepository(AppDbContext db)
    {
        _db = db;
    }

    // Include(UnidadBase) en todas: el DTO expone nombre y simbolo de la unidad,
    // y el servicio de inventario necesita la unidad base para convertir. Es un
    // LEFT JOIN porque unidad_base_id es NULLABLE.
    public async Task<IReadOnlyList<Producto>> ObtenerTodosAsync(
        CancellationToken cancellationToken = default) =>
        await _db.Productos
            .AsNoTracking()
            .Include(p => p.UnidadBase)
            .OrderBy(p => p.Nombre)
            .ToListAsync(cancellationToken);

    public Task<Producto?> ObtenerPorIdAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        _db.Productos
            .AsNoTracking()
            .Include(p => p.UnidadBase)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Producto>> ObtenerPorCategoriaAsync(
        string categoria,
        CancellationToken cancellationToken = default) =>
        await _db.Productos
            .AsNoTracking()
            .Include(p => p.UnidadBase)
            .Where(p => p.Categoria == categoria)
            .OrderBy(p => p.Nombre)
            .ToListAsync(cancellationToken);
}
