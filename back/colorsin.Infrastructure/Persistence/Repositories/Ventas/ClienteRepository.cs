using Colorsin.Application.Ventas.Repositories;
using Colorsin.Domain.Ventas;
using Microsoft.EntityFrameworkCore;

namespace Colorsin.Infrastructure.Persistence.Repositories.Ventas;

/// <inheritdoc cref="IClienteRepository"/>
public sealed class ClienteRepository : IClienteRepository
{
    private readonly AppDbContext _db;

    public ClienteRepository(AppDbContext db)
    {
        _db = db;
    }

    // Sin Include de Ventas: el DTO del cliente no las expone, y cargar el
    // historial completo de cada cliente para listar el catalogo traeria la
    // tabla de ventas entera.
    public async Task<IReadOnlyList<Cliente>> ObtenerTodosAsync(
        CancellationToken cancellationToken = default) =>
        await _db.Clientes
            .AsNoTracking()
            .OrderBy(c => c.RazonSocial)
            .ToListAsync(cancellationToken);

    public Task<Cliente?> ObtenerPorIdAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        _db.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    // `documento` tiene indice unico en la base, asi que esta consulta usa el
    // indice y devuelve como maximo una fila.
    public Task<Cliente?> ObtenerPorDocumentoAsync(
        string documento,
        CancellationToken cancellationToken = default) =>
        _db.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Documento == documento, cancellationToken);

    // AnyAsync se traduce a un EXISTS: no trae ninguna fila.
    public Task<bool> ExisteAsync(int id, CancellationToken cancellationToken = default) =>
        _db.Clientes.AnyAsync(c => c.Id == id, cancellationToken);
}
