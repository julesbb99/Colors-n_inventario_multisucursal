namespace Colorsin.Application.Ventas.DTOs;

/// <summary>
/// A cuanto se vende un producto, con todo lo que hace falta para decidirlo.
///
/// SON TRES CIFRAS DISTINTAS Y NO SE FUNDEN EN UNA:
///
///   PrecioVenta    lo que la administracion fijo. POR UNIDAD BASE.
///   UltimaVenta    lo que de verdad se cobro la ultima vez, EN LA UNIDAD EN QUE
///                  SE COTIZO, sin normalizar, para que la cifra coincida con la
///                  de la factura.
///   CostoPromedio  lo que el producto ha costado en bodega. POR UNIDAD BASE.
///
/// Fundirlas obligaria a elegir cual gana y quien vende perderia las dos senales
/// que importan: que el precio fijado se quedo corto frente al costo, o que la
/// ultima venta se salio de la lista.
///
/// EL COSTO NO ES UN PRECIO DE VENTA y por eso viaja aparte y con su nombre:
/// vender al costo es vender sin margen. Esta aqui para comparar, no para
/// copiarlo al campo sin mirar.
/// </summary>
/// <param name="ProductoId">Producto consultado.</param>
/// <param name="ProductoNombre">Su nombre, para no consultarlo aparte.</param>
/// <param name="UnidadBaseSimbolo">
/// Unidad en la que vienen <paramref name="PrecioVenta"/> y
/// <paramref name="CostoPromedio"/>. Nula si el producto no tiene unidad base,
/// en cuyo caso tampoco se puede convertir nada.
/// </param>
/// <param name="PrecioVenta">
/// Lo fijado por unidad base, o nulo si nadie lo ha fijado. NO ES CERO: cero
/// seria regalarlo.
/// </param>
/// <param name="UltimaVenta">La ultima vez que se vendio, o nulo si nunca.</param>
/// <param name="CostoPromedio">
/// Lo que ha costado de media en las existencias activas de la red, por unidad
/// base. Nulo si el producto no tiene existencias con costo.
/// </param>
/// <param name="MargenPorcentaje">
/// Cuanto deja el precio fijado sobre el costo, en porcentaje. Nulo si falta
/// cualquiera de los dos. NEGATIVO SIGNIFICA VENDER CON PERDIDA, que es
/// exactamente lo que hay que ver antes de confirmar.
/// </param>
public sealed record PrecioVentaDto(
    int ProductoId,
    string ProductoNombre,
    string? UnidadBaseSimbolo,
    decimal? PrecioVenta,
    UltimaVentaDto? UltimaVenta,
    decimal? CostoPromedio,
    decimal? MargenPorcentaje);

/// <summary>
/// La ultima vez que se vendio el producto, tal como quedo en el papel.
/// </summary>
/// <param name="VentaId">Numero de la venta.</param>
/// <param name="Fecha">Cuando fue.</param>
/// <param name="Cantidad">Cuanto se vendio, en <paramref name="UnidadSimbolo"/>.</param>
/// <param name="UnidadId">Unidad en que se cotizo.</param>
/// <param name="UnidadSimbolo">Su abreviatura ('L', 'gal', 'cn5').</param>
/// <param name="PrecioUnitario">Lo cobrado por esa unidad, sin normalizar.</param>
/// <param name="Descuento">Descuento aplicado, en porcentaje.</param>
public sealed record UltimaVentaDto(
    int VentaId,
    DateTime? Fecha,
    decimal? Cantidad,
    int UnidadId,
    string? UnidadSimbolo,
    decimal? PrecioUnitario,
    decimal Descuento);

/// <summary>
/// Fija o quita el precio de venta de un producto.
///
/// Un precio nulo LO BORRA de la lista, que es distinto de ponerlo en cero: sin
/// precio, la venta que no traiga uno propio se rechaza; en cero se registraria
/// regalada. La base lo refuerza con <c>chk_productos_precio_venta</c>.
/// </summary>
/// <param name="PrecioVenta">El precio por unidad base, o nulo para quitarlo.</param>
public sealed record GuardarPrecioVentaDto(decimal? PrecioVenta);
