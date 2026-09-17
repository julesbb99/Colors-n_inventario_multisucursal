using Colorsin.Application.Transferencias.Repositories;
using Colorsin.Domain.Transferencias;
using Microsoft.EntityFrameworkCore;

namespace Colorsin.Infrastructure.Persistence.Repositories.Transferencias;

/// <inheritdoc cref="ITransportadoraRepository"/>
public sealed class TransportadoraRepository : ITransportadoraRepository
{
    private readonly AppDbContext _db;

    public TransportadoraRepository(AppDbContext db)
    {
        _db = db;
    }

    // Sin Include de Transferencias: el DTO no las expone, y cargar el historial
    // de traslados de cada transportadora para listar el catalogo traeria la
    // tabla entera.
    public async Task<IReadOnlyList<Transportadora>> ObtenerTodasAsync(
        CancellationToken cancellationToken = default) =>
        await _db.Transportadoras
            .AsNoTracking()
            .OrderBy(t => t.Nombre)
            .ToListAsync(cancellationToken);

    public Task<Transportadora?> ObtenerPorIdAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        _db.Transportadoras
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    // AnyAsync se traduce a un EXISTS: no trae ninguna fila.
    public Task<bool> ExisteAsync(int id, CancellationToken cancellationToken = default) =>
        _db.Transportadoras.AnyAsync(t => t.Id == id, cancellationToken);
}
