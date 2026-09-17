using Colorsin.Application.Comun.DTOs;
using Colorsin.Domain.Inventario;

namespace Colorsin.Application.Comun.Repositories;

/// <summary>Acceso de lectura al catalogo de unidades de medida.</summary>
public interface IUnidadMedidaRepository
{
    /// <summary>Todas las unidades, ordenadas por nombre.</summary>
    Task<IReadOnlyList<UnidadMedida>> ObtenerTodasAsync(CancellationToken cancellationToken = default);

    /// <summary>La unidad con ese id, o <c>null</c> si no existe.</summary>
    Task<UnidadMedida?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Factor de conversion a litros de una unidad.
    ///
    /// Es una consulta aparte de <see cref="ObtenerPorIdAsync"/> porque la usan
    /// los calculos de inventario, que solo necesitan el numero y se llaman
    /// muchas veces; traer la entidad entera para leer una columna es gasto
    /// innecesario.
    ///
    /// Ver <see cref="ResultadoFactorConversion"/>: distingue "la unidad no
    /// existe" de "la unidad existe pero no es de volumen".
    /// </summary>
    Task<ResultadoFactorConversion> ObtenerFactorConversionLitrosAsync(
        int id,
        CancellationToken cancellationToken = default);
}
