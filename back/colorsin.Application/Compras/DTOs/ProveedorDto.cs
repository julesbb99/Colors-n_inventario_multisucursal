namespace Colorsin.Application.Compras.DTOs;

/// <summary>Proveedor del catalogo.</summary>
/// <param name="Id">Clave primaria.</param>
/// <param name="Nombre">Razon social.</param>
/// <param name="Contacto">Persona de contacto. Opcional en la base.</param>
/// <param name="Telefono">Telefono. Obligatorio.</param>
/// <param name="ProductosQueSurte">
/// Cuantos productos del catalogo tiene asociados en `producto_proveedor`.
/// Nulo cuando la consulta no pidio ese conteo, que NO es lo mismo que cero:
/// cero significa que no surte ninguno.
/// </param>
public sealed record ProveedorDto(
    int Id,
    string Nombre,
    string? Contacto,
    string Telefono,
    int? ProductosQueSurte = null);
