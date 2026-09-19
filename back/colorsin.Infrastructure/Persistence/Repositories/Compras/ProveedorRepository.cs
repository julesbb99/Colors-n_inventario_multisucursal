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
        bool incluirInactivos = false,
        CancellationToken cancellationToken = default)
    {
        var consulta = _db.Proveedores
            .AsNoTracking()
            .Include(p => p.Productos)
            .AsQueryable();

        if (!incluirInactivos)
        {
            consulta = consulta.Where(p => p.Activo);
        }

        return await consulta
            // Los activos primero cuando se piden los dos: un retirado es la
            // excepcion y no tiene por que mezclarse con el catalogo del dia.
            .OrderByDescending(p => p.Activo)
            .ThenBy(p => p.Nombre)
            .ToListAsync(cancellationToken);
    }

    public Task<Proveedor?> ObtenerParaEditarAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        _db.Proveedores
            .Include(p => p.Productos)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    // EF traduce el ToLower() a una comparacion del servidor. La collation del
    // esquema ya es insensible a mayusculas, pero dejarlo explicito evita que un
    // cambio de collation abra la puerta a "ACME" y "Acme" como dos proveedores.
    public Task<bool> ExisteNombreAsync(
        string nombre,
        int? excluyendoId,
        CancellationToken cancellationToken = default)
    {
        var buscado = nombre.Trim().ToLower();

        return _db.Proveedores.AnyAsync(
            p => p.Nombre.ToLower() == buscado && (excluyendoId == null || p.Id != excluyendoId),
            cancellationToken);
    }

    public void Agregar(Proveedor proveedor) => _db.Proveedores.Add(proveedor);

    public Task<ProductoProveedor?> ObtenerPrecioAsync(
        int productoId,
        int proveedorId,
        CancellationToken cancellationToken = default) =>
        _db.ProductoProveedores
            .FirstOrDefaultAsync(
                pp => pp.ProductoId == productoId && pp.ProveedorId == proveedorId,
                cancellationToken);

    public async Task<IReadOnlyList<ProductoProveedor>> ObtenerPreciosDeProductoAsync(
        int productoId,
        CancellationToken cancellationToken = default) =>
        await _db.ProductoProveedores
            .AsNoTracking()
            .Include(pp => pp.Proveedor)
            // Sin precio no informa de nada: la fila solo dice que ese proveedor
            // surte el producto, no a cuanto.
            .Where(pp => pp.ProductoId == productoId && pp.PrecioReferencia != null)
            .OrderBy(pp => pp.PrecioReferencia)
            .ToListAsync(cancellationToken);

    public void AgregarPrecio(ProductoProveedor precio) => _db.ProductoProveedores.Add(precio);

    public void QuitarPrecio(ProductoProveedor precio) => _db.ProductoProveedores.Remove(precio);

    public Task<int> GuardarCambiosAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);

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
