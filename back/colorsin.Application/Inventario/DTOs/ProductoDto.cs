namespace Colorsin.Application.Inventario.DTOs;

/// <summary>Producto del catalogo.</summary>
/// <param name="Id">Clave primaria.</param>
/// <param name="Nombre">Nombre comercial.</param>
/// <param name="Categoria">Familia del producto. Opcional en la base.</param>
/// <param name="Descripcion">Texto libre. Opcional.</param>
/// <param name="UnidadBaseId">
/// Unidad en la que se lleva el stock. Es NULLABLE en la base, y un producto
/// sin ella no se puede mover: no hay a que convertir la cantidad. El servicio
/// de inventario lo rechaza con <c>ProductoSinUnidadBase</c>.
/// </param>
/// <param name="UnidadBaseNombre">Nombre de esa unidad, para no consultarla aparte.</param>
/// <param name="UnidadBaseSimbolo">Abreviatura de esa unidad ('L', 'gal').</param>
public sealed record ProductoDto(
    int Id,
    string Nombre,
    string? Categoria,
    string? Descripcion,
    int? UnidadBaseId,
    string? UnidadBaseNombre,
    string? UnidadBaseSimbolo);
