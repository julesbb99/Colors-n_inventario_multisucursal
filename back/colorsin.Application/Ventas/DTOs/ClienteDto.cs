namespace Colorsin.Application.Ventas.DTOs;

/// <summary>Cliente de Colorsin.</summary>
/// <param name="Id">Clave primaria.</param>
/// <param name="RazonSocial">Nombre o razon social.</param>
/// <param name="TipoPersona">'Natural' o 'Juridica'.</param>
/// <param name="Documento">
/// Cedula si es persona natural, NIT si es juridica. Unico en toda la base.
/// </param>
/// <param name="Telefono">Telefono. Opcional.</param>
/// <param name="Email">Correo. Opcional.</param>
/// <param name="Direccion">Direccion. Opcional.</param>
public sealed record ClienteDto(
    int Id,
    string RazonSocial,
    string TipoPersona,
    string Documento,
    string? Telefono,
    string? Email,
    string? Direccion);
