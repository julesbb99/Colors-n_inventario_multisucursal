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
        bool incluirRetiradas = false,
        CancellationToken cancellationToken = default)
    {
        var consulta = _db.Transportadoras.AsNoTracking();

        if (!incluirRetiradas)
        {
            consulta = consulta.Where(t => t.Activo);
        }

        return await consulta
            // Las activas primero cuando se piden las dos: una retirada es la
            // excepcion y no tiene por que mezclarse con el listado del dia.
            .OrderByDescending(t => t.Activo)
            .ThenBy(t => t.Nombre)
            .ToListAsync(cancellationToken);
    }

    public Task<Transportadora?> ObtenerPorIdAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        _db.Transportadoras
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    // AnyAsync se traduce a un EXISTS: no trae ninguna fila.
    public Task<bool> ExisteAsync(int id, CancellationToken cancellationToken = default) =>
        _db.Transportadoras.AnyAsync(t => t.Id == id, cancellationToken);

    // SIN AsNoTracking, al contrario que las de arriba: esta fila se modifica.
    public Task<Transportadora?> ObtenerParaActualizarAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        _db.Transportadoras.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<bool> ExisteNombreAsync(
        string nombre,
        int? exceptoId = null,
        CancellationToken cancellationToken = default)
    {
        var buscado = nombre.Trim();

        // La comparacion la hace MySQL con la intercalacion de la columna, que
        // en esta base no distingue mayusculas: "Redetrans" y "REDETRANS" chocan
        // sin necesidad de ToLower(), que ademas impediria usar un indice.
        var consulta = _db.Transportadoras.Where(t => t.Nombre == buscado);

        if (exceptoId is int id)
        {
            consulta = consulta.Where(t => t.Id != id);
        }

        return consulta.AnyAsync(cancellationToken);
    }

    public void Agregar(Transportadora transportadora) =>
        _db.Transportadoras.Add(transportadora);

    public Task<int> GuardarCambiosAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
