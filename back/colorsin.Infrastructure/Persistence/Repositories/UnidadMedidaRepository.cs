using Colorsin.Application.Comun.DTOs;
using Colorsin.Application.Comun.Repositories;
using Colorsin.Domain.Inventario;
using Microsoft.EntityFrameworkCore;

namespace Colorsin.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IUnidadMedidaRepository"/>
public sealed class UnidadMedidaRepository : IUnidadMedidaRepository
{
    private readonly AppDbContext _db;

    public UnidadMedidaRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<UnidadMedida>> ObtenerTodasAsync(
        CancellationToken cancellationToken = default) =>
        await _db.UnidadesMedida
            .AsNoTracking()
            .OrderBy(u => u.Nombre)
            .ToListAsync(cancellationToken);

    public Task<UnidadMedida?> ObtenerPorIdAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        _db.UnidadesMedida
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<ResultadoFactorConversion> ObtenerFactorConversionLitrosAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        // Se proyecta a un tipo anonimo envolviendo el decimal? en vez de
        // seleccionar la columna directa. Si se hiciera
        //   .Select(u => u.FactorConversionLitros).FirstOrDefaultAsync()
        // un kilogramo (factor NULL) y un id inexistente devolverian los dos
        // null, y no habria forma de distinguirlos. Con el envoltorio, el
        // objeto nulo significa "no hay fila" y punto.
        var fila = await _db.UnidadesMedida
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new { u.FactorConversionLitros })
            .FirstOrDefaultAsync(cancellationToken);

        return fila is null
            ? ResultadoFactorConversion.NoEncontrada
            : new ResultadoFactorConversion(true, fila.FactorConversionLitros);
    }
}
