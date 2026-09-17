using Colorsin.Application.Comun.DTOs;

namespace Colorsin.Application.Comun.Services;

/// <summary>Consultas del catalogo de unidades de medida.</summary>
public interface IUnidadMedidaService
{
    Task<IReadOnlyList<UnidadMedidaDto>> ListarAsync(CancellationToken cancellationToken = default);

    /// <summary>La unidad con ese id, o <c>null</c> si no existe.</summary>
    Task<UnidadMedidaDto?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Factor de conversion a litros de una unidad. Revisa
    /// <see cref="ResultadoFactorConversion.UnidadExiste"/> antes de usar el
    /// factor: un nulo con UnidadExiste = true significa que la unidad no es de
    /// volumen, no que falte el dato.
    /// </summary>
    Task<ResultadoFactorConversion> ObtenerFactorConversionLitrosAsync(
        int id,
        CancellationToken cancellationToken = default);
}
