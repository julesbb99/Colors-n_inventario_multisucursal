namespace Colorsin.Application.Ventas.DTOs;

/// <summary>
/// Alta de un cliente desde el mostrador.
///
/// NO LLEVA ID: lo asigna la base. Y no lleva sede, porque un cliente le compra
/// a la red, no a una bodega; lo que pertenece a una sede es la venta.
/// </summary>
/// <param name="RazonSocial">Nombre de la persona o razon social de la empresa.</param>
/// <param name="TipoPersona">
/// 'Natural' o 'Juridica'. Va como cadena y no como enum porque es lo que ya
/// hace <see cref="ClienteDto"/> al salir, y mezclar las dos direcciones -entrar
/// como numero y salir como texto- es de donde salen los desajustes.
/// </param>
/// <param name="Documento">
/// Cedula o NIT. Unico en toda la base: es por lo que se busca a alguien en el
/// mostrador, donde se tiene el documento que trae la persona y no el id.
/// </param>
/// <param name="Telefono">Telefono. Opcional.</param>
/// <param name="Email">Correo. Opcional.</param>
/// <param name="Direccion">Direccion. Opcional.</param>
public sealed record CrearClienteDto(
    string RazonSocial,
    string TipoPersona,
    string Documento,
    string? Telefono = null,
    string? Email = null,
    string? Direccion = null);
