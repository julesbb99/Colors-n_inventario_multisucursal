using Colorsin.Application.Ventas.DTOs;

namespace Colorsin.Application.Ventas.Services;

/// <summary>Operaciones del modulo de ventas.</summary>
public interface IVentasService
{
    /// <summary>Clientes del catalogo, ordenados por razon social.</summary>
    Task<IReadOnlyList<ClienteDto>> ObtenerClientesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>El cliente con ese id, o <c>null</c> si no existe.</summary>
    Task<ClienteDto?> ObtenerClientePorIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// El cliente con ese documento (cedula o NIT), o <c>null</c>. Sirve para
    /// buscarlo en el mostrador, donde se tiene el documento y no el id.
    /// </summary>
    Task<ClienteDto?> ObtenerClientePorDocumentoAsync(
        string documento,
        CancellationToken cancellationToken = default);

    /// <summary>Ventas, de la mas reciente a la mas antigua. Sin detalle.</summary>
    Task<IReadOnlyList<VentaDto>> ObtenerVentasAsync(
        int? sucursalId = null,
        int? clienteId = null,
        DateTime? desde = null,
        DateTime? hasta = null,
        int limite = 100,
        CancellationToken cancellationToken = default);

    /// <summary>La venta con ese id y su detalle completo, o <c>null</c>.</summary>
    Task<VentaDto?> ObtenerVentaPorIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra una venta ya concretada y descuenta el stock.
    ///
    /// En una sola transaccion: valida que la sede tenga existencias, crea la
    /// venta con su detalle, descuenta el saldo de la sede, descuenta los lotes
    /// (por FEFO o por el lote que indique cada linea), anexa un movimiento de
    /// Retiro/Venta al libro mayor por cada lote consumido y deja el evento de
    /// auditoria. O queda todo, o no queda nada.
    ///
    /// A diferencia de Inventario y Compras, las reglas de negocio de este
    /// modulo se LANZAN como excepcion en vez de devolverse: hay que envolver
    /// la llamada en try/catch sobre <see cref="VentaException"/>. La de mayor
    /// interes es <see cref="StockInsuficienteException"/>, que lleva el
    /// disponible y el solicitado como propiedades.
    /// </summary>
    Task<VentaRegistradaDto> RegistrarVentaAsync(
        CrearVentaDto peticion,
        CancellationToken cancellationToken = default);
}
