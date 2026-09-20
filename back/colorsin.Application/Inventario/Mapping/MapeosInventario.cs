using Colorsin.Application.Inventario.DTOs;
using Colorsin.Domain.Inventario;

namespace Colorsin.Application.Inventario.Mapping;

/// <summary>Conversion de entidades del modulo Inventario a sus DTOs.</summary>
public static class MapeosInventario
{
    public static ProductoDto ToDto(this Producto producto) => new(
        producto.Id,
        producto.Nombre,
        producto.Categoria,
        producto.Descripcion,
        producto.UnidadBaseId,
        producto.UnidadBase?.Nombre,
        producto.UnidadBase?.Simbolo,
        producto.PrecioVenta);

    public static InventarioSucursalDto ToDto(this InventarioSucursal saldo) => new(
        saldo.Id,
        saldo.SucursalId,
        saldo.Sucursal?.Nombre ?? string.Empty,
        saldo.ProductoId,
        saldo.Producto?.Nombre ?? string.Empty,
        saldo.Producto?.UnidadBase?.Simbolo,
        saldo.CantidadBase,
        saldo.StockMinimo,
        saldo.CostoPromedio,
        // Una existencia deshabilitada no alerta nunca, este como este su saldo.
        saldo.Activo && saldo.CantidadBase <= saldo.StockMinimo,
        saldo.Activo);

    public static StockAlertaDto ToAlertaDto(this InventarioSucursal saldo) => new(
        saldo.ProductoId,
        saldo.Producto?.Nombre ?? string.Empty,
        saldo.SucursalId,
        saldo.Sucursal?.Nombre ?? string.Empty,
        saldo.CantidadBase,
        saldo.StockMinimo,
        saldo.Producto?.UnidadBase?.Simbolo,
        // Nunca negativo: si el saldo superara el minimo no habria alerta.
        Math.Max(0m, saldo.StockMinimo - saldo.CantidadBase));

    /// <summary>
    /// Mapea un lote. <paramref name="hoy"/> entra por parametro y no se lee de
    /// <c>DateTime.Today</c> aqui adentro para que el calculo de dias sea
    /// verificable: una funcion que consulta el reloj no se puede probar.
    ///
    /// Exige que vengan cargadas las navegaciones Producto (con su UnidadBase) y
    /// Sucursal. Sin ellas los nombres salen vacios, que es peor que fallar
    /// porque no se nota hasta que alguien mira la pantalla.
    /// </summary>
    public static LoteDto ToDto(this Lote lote, DateOnly hoy)
    {
        // Negativo si ya vencio; nulo si el producto no caduca.
        var dias = lote.FechaVencimiento is null
            ? (int?)null
            : lote.FechaVencimiento.Value.DayNumber - hoy.DayNumber;

        return new LoteDto(
            lote.Id,
            lote.ProductoId,
            lote.Producto?.Nombre ?? string.Empty,
            lote.SucursalId,
            lote.Sucursal?.Nombre ?? string.Empty,
            lote.NumeroLote,
            lote.FechaVencimiento,
            lote.CantidadBase,
            lote.Producto?.UnidadBase?.Simbolo,
            lote.FechaIngreso,
            dias,
            // Un lote sin fecha nunca esta vencido, no "vencido hace null dias".
            dias < 0);
    }

    public static MovimientoInventarioDto ToDto(this MovimientoInventario movimiento) => new(
        movimiento.Id,
        movimiento.SucursalId,
        movimiento.Sucursal?.Nombre ?? string.Empty,
        movimiento.ProductoId,
        movimiento.Producto?.Nombre ?? string.Empty,
        movimiento.UsuarioId,
        movimiento.Usuario?.Nombre ?? string.Empty,
        movimiento.Tipo?.ToString(),
        movimiento.Motivo?.ToString(),
        movimiento.Cantidad,
        movimiento.UnidadId,
        movimiento.Unidad?.Simbolo ?? string.Empty,
        movimiento.CantidadBase,
        movimiento.LoteId,
        movimiento.Lote?.NumeroLote,
        movimiento.Observaciones,
        movimiento.Fecha);
}
