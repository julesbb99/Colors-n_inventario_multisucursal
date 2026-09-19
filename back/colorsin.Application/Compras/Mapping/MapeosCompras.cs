using Colorsin.Application.Compras.DTOs;
using Colorsin.Domain.Compras;

namespace Colorsin.Application.Compras.Mapping;

/// <summary>Conversion de entidades del modulo Compras a sus DTOs.</summary>
public static class MapeosCompras
{
    /// <summary>
    /// Decimales de los importes: `precio_unitario` es DECIMAL(12,2).
    ///
    /// El calculo del descuento se redondea a esa misma escala para que la suma
    /// de las lineas coincida con el total al centavo. Sin redondear, un
    /// descuento del 7,5% sobre un precio con centavos arrastra decimales que
    /// no existen en pesos.
    /// </summary>
    private const int DecimalesImporte = 2;

    public static ProveedorDto ToDto(this Proveedor proveedor, bool contarProductos = false) => new(
        proveedor.Id,
        proveedor.Nombre,
        proveedor.Contacto,
        proveedor.Telefono,
        // Nulo, no cero, cuando la consulta no pidio el conteo: cero significa
        // "no surte ninguno", que es una respuesta distinta de "no se sabe".
        contarProductos ? proveedor.Productos.Count : null,
        proveedor.Activo);

    /// <summary>Importe bruto de una linea, antes de descuento.</summary>
    private static decimal Bruto(OrdenCompraDetalle detalle) =>
        Redondear((detalle.Cantidad ?? 0m) * (detalle.PrecioUnitario ?? 0m));

    /// <summary>
    /// `descuento` es un PORCENTAJE (0..100), no un importe. Tratarlo como
    /// importe rebajaria 15 pesos donde debe rebajar el 15%.
    /// </summary>
    private static decimal ValorDescuento(OrdenCompraDetalle detalle) =>
        Redondear(Bruto(detalle) * detalle.Descuento / 100m);

    /// <summary>
    /// Lo que falta por recibir de una linea.
    ///
    /// Nunca negativo: el CHECK `chk_ocd_cantidad_recibida` impide que lo
    /// recibido supere lo pedido, pero el Max protege igual del caso en que
    /// `cantidad` sea nula.
    /// </summary>
    private static decimal Pendiente(OrdenCompraDetalle detalle) =>
        Math.Max(0m, (detalle.Cantidad ?? 0m) - detalle.CantidadRecibida);

    public static DetalleOrdenCompraDto ToDto(this OrdenCompraDetalle detalle)
    {
        var bruto = Bruto(detalle);
        var valorDescuento = ValorDescuento(detalle);
        var pendiente = Pendiente(detalle);

        return new DetalleOrdenCompraDto(
            detalle.Id,
            detalle.OrdenCompraId,
            detalle.ProductoId,
            detalle.Producto?.Nombre ?? string.Empty,
            detalle.Cantidad,
            detalle.CantidadRecibida,
            pendiente,
            pendiente == 0m,
            detalle.UnidadId,
            detalle.Unidad?.Simbolo ?? string.Empty,
            detalle.PrecioUnitario,
            detalle.Descuento,
            bruto,
            valorDescuento,
            bruto - valorDescuento);
    }

    /// <summary>
    /// Mapea la orden. <paramref name="incluirDetalle"/> en <c>false</c> para
    /// los listados: cargar el detalle de cada orden en una lista de cien
    /// convierte una consulta en ciento una.
    /// </summary>
    public static OrdenCompraDto ToDto(this OrdenCompra orden, bool incluirDetalle = true)
    {
        var detalles = incluirDetalle
            ? orden.Detalles.Select(d => d.ToDto()).ToList()
            : [];

        // EL TOTAL Y LAS LINEAS PENDIENTES SE CALCULAN SOBRE orden.Detalles, NO
        // SOBRE `detalles`.
        //
        // Parecen lo mismo y no lo son: en el listado `detalles` va vacio a
        // proposito -devolver las lineas de cien ordenes es una carga que nadie
        // mira- mientras que orden.Detalles si viene cargado. Cuando esto
        // dependia de la lista recortada, TODAS las ordenes del listado salian
        // con total 0 y sin lineas pendientes, asi que la columna Total mostraba
        // $0 siempre y la pestana "Por recibir" no encontraba nada nunca.
        var total = orden.Detalles.Sum(d => Bruto(d) - ValorDescuento(d));
        var lineasPendientes = orden.Detalles.Count(d => Pendiente(d) > 0m);

        return new OrdenCompraDto(
            orden.Id,
            orden.ProveedorId,
            orden.Proveedor?.Nombre ?? string.Empty,
            orden.SucursalId,
            orden.Sucursal?.Nombre ?? string.Empty,
            orden.UsuarioId,
            orden.Usuario?.Nombre ?? string.Empty,
            orden.Fecha,
            orden.Estado?.ToString(),
            orden.PlazoPagoDias,
            detalles,
            total,
            lineasPendientes,
            orden.Detalles.Count);
    }

    private static decimal Redondear(decimal valor) =>
        // AwayFromZero para no usar el redondeo bancario de .NET, que en 0,125
        // daria 0,12 y no 0,13. En importes se espera lo segundo.
        Math.Round(valor, DecimalesImporte, MidpointRounding.AwayFromZero);
}
