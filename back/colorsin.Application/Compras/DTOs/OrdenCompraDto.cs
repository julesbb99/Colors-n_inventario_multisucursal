namespace Colorsin.Application.Compras.DTOs;

/// <summary>Orden de compra con su detalle.</summary>
/// <param name="Id">Clave primaria.</param>
/// <param name="ProveedorId">Proveedor al que se pide.</param>
/// <param name="ProveedorNombre">Razon social del proveedor.</param>
/// <param name="SucursalId">Sede que recibira la mercancia.</param>
/// <param name="SucursalNombre">Nombre de esa sede.</param>
/// <param name="UsuarioId">Quien creo la orden.</param>
/// <param name="UsuarioNombre">Nombre de esa persona.</param>
/// <param name="Fecha">Cuando se creo la orden.</param>
/// <param name="Estado">'Pendiente', 'ParcialmenteRecibida', 'Recibida' o 'Cancelada'.</param>
/// <param name="PlazoPagoDias">Dias de credito pactados. Cero es contado.</param>
/// <param name="Detalles">
/// Lineas de la orden. Viene vacia en los listados, que no cargan el detalle
/// para no traer toda la historia de compras en cada consulta.
/// </param>
/// <param name="Total">Suma de los subtotales netos de las lineas.</param>
/// <param name="LineasPendientes">
/// Cuantas lineas siguen sin completarse. Cero en una orden totalmente
/// recibida. Vale cero tambien cuando el detalle no se cargo, asi que solo
/// significa algo junto a <paramref name="Detalles"/>.
/// </param>
public sealed record OrdenCompraDto(
    int Id,
    int ProveedorId,
    string ProveedorNombre,
    int SucursalId,
    string SucursalNombre,
    int UsuarioId,
    string UsuarioNombre,
    DateTime? Fecha,
    string? Estado,
    int? PlazoPagoDias,
    IReadOnlyList<DetalleOrdenCompraDto> Detalles,
    decimal Total,
    int LineasPendientes);
