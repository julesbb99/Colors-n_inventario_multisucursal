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
        contarProductos ? proveedor.Productos.Count : null);

    public static DetalleOrdenCompraDto ToDto(this OrdenCompraDetalle detalle)
    {
        var cantidad = detalle.Cantidad ?? 0m;
        var precio = detalle.PrecioUnitario ?? 0m;

        var bruto = Redondear(cantidad * precio);
        // `descuento` es un PORCENTAJE (0..100), no un importe. Tratarlo como
        // importe rebajaria 15 pesos donde debe rebajar el 15%.
        var valorDescuento = Redondear(bruto * detalle.Descuento / 100m);

        // Nunca negativo: el CHECK `chk_ocd_cantidad_recibida` impide que lo
        // recibido supere lo pedido, pero el Max protege igual del caso en que
        // `cantidad` sea nula.
        var pendiente = Math.Max(0m, cantidad - detalle.CantidadRecibida);

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
            detalles.Sum(d => d.SubtotalNeto),
            detalles.Count(d => !d.Completa));
    }

    private static decimal Redondear(decimal valor) =>
        // AwayFromZero para no usar el redondeo bancario de .NET, que en 0,125
        // daria 0,12 y no 0,13. En importes se espera lo segundo.
        Math.Round(valor, DecimalesImporte, MidpointRounding.AwayFromZero);
}
