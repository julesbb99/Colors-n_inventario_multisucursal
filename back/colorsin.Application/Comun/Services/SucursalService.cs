using Colorsin.Application.Comun.DTOs;
using Colorsin.Application.Comun.Mapping;
using Colorsin.Application.Comun.Repositories;

namespace Colorsin.Application.Comun.Services;

/// <inheritdoc cref="ISucursalService"/>
public sealed class SucursalService : ISucursalService
{
    private readonly ISucursalRepository _repositorio;

    public SucursalService(ISucursalRepository repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<IReadOnlyList<SucursalDto>> ListarAsync(
        CancellationToken cancellationToken = default)
    {
        var sucursales = await _repositorio.ObtenerTodasAsync(cancellationToken);
        return sucursales.Select(s => s.ToDto()).ToList();
    }

    public async Task<SucursalDto?> ObtenerPorIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var sucursal = await _repositorio.ObtenerPorIdAsync(id, cancellationToken);
        return sucursal?.ToDto();
    }
}
