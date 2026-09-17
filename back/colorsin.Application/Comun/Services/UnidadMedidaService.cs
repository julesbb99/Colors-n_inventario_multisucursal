using Colorsin.Application.Comun.DTOs;
using Colorsin.Application.Comun.Mapping;
using Colorsin.Application.Comun.Repositories;

namespace Colorsin.Application.Comun.Services;

/// <inheritdoc cref="IUnidadMedidaService"/>
public sealed class UnidadMedidaService : IUnidadMedidaService
{
    private readonly IUnidadMedidaRepository _repositorio;

    public UnidadMedidaService(IUnidadMedidaRepository repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<IReadOnlyList<UnidadMedidaDto>> ListarAsync(
        CancellationToken cancellationToken = default)
    {
        var unidades = await _repositorio.ObtenerTodasAsync(cancellationToken);
        return unidades.Select(u => u.ToDto()).ToList();
    }

    public async Task<UnidadMedidaDto?> ObtenerPorIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var unidad = await _repositorio.ObtenerPorIdAsync(id, cancellationToken);
        return unidad?.ToDto();
    }

    public Task<ResultadoFactorConversion> ObtenerFactorConversionLitrosAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        _repositorio.ObtenerFactorConversionLitrosAsync(id, cancellationToken);
}
