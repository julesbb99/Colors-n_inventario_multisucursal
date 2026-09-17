using Colorsin.Application.Ventas.DTOs;
using Colorsin.Domain.Ventas;

namespace Colorsin.Application.Ventas.Mapping;

/// <summary>Conversion de entidades del modulo Ventas a sus DTOs.</summary>
public static class MapeosVentas
{
    /// <summary>
    /// Decimales de los importes: `precio_unitario` es DECIMAL(12,2) y
    /// `ventas.total` es DECIMAL(14,2).
    /// </summary>
    public const int DecimalesImporte = 2;

    /// <summary>
    /// Decimales de `venta_detalle.cantidad`: DECIMAL(12,2).
    ///
    /// Son 2, no 4 como en el resto del modelo. El servicio redondea a esta
    /// escala antes de convertir a unidad base, para que el stock descontado
    /// derive del mismo numero que queda almacenado en la linea.
    /// </summary>
    public const int DecimalesCantidadVenta = 2;

    public static ClienteDto ToDto(this Cliente cliente) => new(
        cliente.Id,
        cliente.RazonSocial,
        // 'Natural' y 'Juridica' se escriben igual en el enum y en la base, asi
        // que ToString() basta y no hay tabla de equivalencias que mantener.
        cliente.TipoPersona.ToString(),
        cliente.Documento,
        cliente.Telefono,
        cliente.Email,
        cliente.Direccion);

    public static DetalleVentaDto ToDto(this VentaDetalle detalle)
    {
        var cantidad = detalle.Cantidad ?? 0m;
        var precio = detalle.PrecioUnitario ?? 0m;

        var bruto = RedondearImporte(cantidad * precio);
        // `descuento` es un PORCENTAJE (0..100), no un importe. Tratarlo como
        // importe rebajaria 15 pesos donde debe rebajar el 15%.
        var valorDescuento = RedondearImporte(bruto * detalle.Descuento / 100m);

        return new DetalleVentaDto(
            detalle.Id,
            detalle.VentaId,
            detalle.ProductoId,
            detalle.Producto?.Nombre ?? string.Empty,
            detalle.Cantidad,
            detalle.UnidadId,
            detalle.Unidad?.Simbolo ?? string.Empty,
            detalle.PrecioUnitario,
            detalle.Descuento,
            bruto,
            valorDescuento,
            bruto - valorDescuento);
    }

    /// <summary>
    /// Mapea la venta. <paramref name="incluirDetalle"/> en <c>false</c> para
    /// los listados: cargar el detalle de cada venta en una lista de cien
    /// convierte una consulta en ciento una.
    /// </summary>
    public static VentaDto ToDto(this Venta venta, bool incluirDetalle = true) => new(
        venta.Id,
        venta.ClienteId,
        venta.Cliente?.RazonSocial ?? string.Empty,
        venta.Cliente?.Documento ?? string.Empty,
        venta.SucursalId,
        venta.Sucursal?.Nombre ?? string.Empty,
        venta.UsuarioId,
        venta.Usuario?.Nombre ?? string.Empty,
        venta.Fecha,
        // El total ALMACENADO, no uno recalculado: si difiere de la suma del
        // detalle, eso es justamente lo que hay que poder ver.
        venta.Total,
        incluirDetalle ? venta.Detalles.Select(d => d.ToDto()).ToList() : []);

    /// <summary>
    /// Subtotal neto de una linea que todavia no existe en la base. Lo usa el
    /// servicio para calcular el total antes de guardar, con la misma
    /// aritmetica que usara luego la lectura.
    /// </summary>
    public static decimal CalcularSubtotalNeto(decimal cantidad, decimal precio, decimal descuento)
    {
        var bruto = RedondearImporte(cantidad * precio);
        return bruto - RedondearImporte(bruto * descuento / 100m);
    }

    /// <summary>Redondea a la escala de `venta_detalle.cantidad`.</summary>
    public static decimal RedondearCantidad(decimal valor) =>
        Math.Round(valor, DecimalesCantidadVenta, MidpointRounding.AwayFromZero);

    private static decimal RedondearImporte(decimal valor) =>
        // AwayFromZero para no usar el redondeo bancario de .NET, que en 0,125
        // daria 0,12 y no 0,13. En importes se espera lo segundo.
        Math.Round(valor, DecimalesImporte, MidpointRounding.AwayFromZero);
}
