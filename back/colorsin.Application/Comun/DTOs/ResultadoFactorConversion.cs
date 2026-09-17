namespace Colorsin.Application.Comun.DTOs;

/// <summary>
/// Respuesta de consultar el factor de conversion a litros de una unidad.
///
/// Existe para separar dos casos que un simple <c>decimal?</c> confundiria:
///
///   - La unidad NO existe            -> <see cref="UnidadExiste"/> = false.
///   - La unidad existe pero no es de
///     volumen (kilogramo)            -> UnidadExiste = true, Factor = null.
///
/// Son cosas distintas: la primera es un id equivocado y debe responder 404;
/// la segunda es un dato valido del catalogo. Devolver <c>null</c> en ambos
/// casos haria que quien llama tratara un error como si fuera un kilogramo.
/// </summary>
/// <param name="UnidadExiste">Si el id corresponde a una unidad del catalogo.</param>
/// <param name="FactorLitros">Litros por unidad, o nulo si la unidad no es de volumen.</param>
public sealed record ResultadoFactorConversion(bool UnidadExiste, decimal? FactorLitros)
{
    /// <summary>Valor a devolver cuando el id no corresponde a ninguna unidad.</summary>
    public static readonly ResultadoFactorConversion NoEncontrada = new(false, null);
}
