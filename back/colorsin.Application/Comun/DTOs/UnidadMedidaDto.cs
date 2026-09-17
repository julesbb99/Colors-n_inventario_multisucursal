namespace Colorsin.Application.Comun.DTOs;

/// <summary>Unidad de medida del catalogo.</summary>
/// <param name="Id">Clave primaria.</param>
/// <param name="Nombre">Nombre de la unidad ('Litro', 'Galon', 'Kilogramo').</param>
/// <param name="Simbolo">Abreviatura ('L', 'gal', 'kg').</param>
/// <param name="FactorConversionLitros">
/// Litros que equivalen a una unidad de esta medida. Nulo cuando la unidad no
/// es de volumen (el kilogramo, por ejemplo): eso NO es un dato faltante, es
/// que la conversion no aplica.
///
/// Van 6 decimales a proposito. Un galon son 3.785410 L; con 2 decimales
/// quedaria en 3.79 y el error se acumularia en cada conversion del sistema.
/// </param>
public sealed record UnidadMedidaDto(
    int Id,
    string Nombre,
    string Simbolo,
    decimal? FactorConversionLitros);
