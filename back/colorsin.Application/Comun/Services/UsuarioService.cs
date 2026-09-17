using Colorsin.Application.Comun.DTOs;
using Colorsin.Application.Comun.Mapping;
using Colorsin.Application.Comun.Repositories;

namespace Colorsin.Application.Comun.Services;

/// <inheritdoc cref="IUsuarioService"/>
public sealed class UsuarioService : IUsuarioService
{
    private readonly IUsuarioRepository _repositorio;

    public UsuarioService(IUsuarioRepository repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<IReadOnlyList<UsuarioDto>> ListarAsync(
        int? sucursalId = null,
        CancellationToken cancellationToken = default)
    {
        var usuarios = sucursalId is null
            ? await _repositorio.ObtenerTodosAsync(cancellationToken)
            : await _repositorio.ObtenerPorSucursalAsync(sucursalId.Value, cancellationToken);

        return usuarios.Select(u => u.ToDto()).ToList();
    }

    public async Task<UsuarioDto?> ObtenerPorIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var usuario = await _repositorio.ObtenerPorIdAsync(id, cancellationToken);
        return usuario?.ToDto();
    }
}
