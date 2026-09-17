namespace Colorsin.Application.Ventas.DTOs;

/// <summary>Venta con su detalle.</summary>
/// <param name="Id">Clave primaria.</param>
/// <param name="ClienteId">Cliente.</param>
/// <param name="ClienteRazonSocial">Nombre o razon social del cliente.</param>
/// <param name="ClienteDocumento">Cedula o NIT del cliente.</param>
/// <param name="SucursalId">Sede que despacha, y de cuyo stock se descuenta.</param>
/// <param name="SucursalNombre">Nombre de esa sede.</param>
/// <param name="UsuarioId">Quien registro la venta.</param>
/// <param name="UsuarioNombre">Nombre de esa persona.</param>
/// <param name="Fecha">Momento de la venta.</param>
/// <param name="Total">
/// Suma de los subtotales netos, tal como quedo ALMACENADA en `ventas.total`.
/// Puede ser nula en ventas creadas por fuera de este servicio.
/// </param>
/// <param name="Detalles">
/// Lineas de la venta. Viene vacia en los listados, que no cargan el detalle
/// para no traer toda la historia de ventas en cada consulta.
/// </param>
public sealed record VentaDto(
    int Id,
    int ClienteId,
    string ClienteRazonSocial,
    string ClienteDocumento,
    int SucursalId,
    string SucursalNombre,
    int UsuarioId,
    string UsuarioNombre,
    DateTime? Fecha,
    decimal? Total,
    IReadOnlyList<DetalleVentaDto> Detalles);
