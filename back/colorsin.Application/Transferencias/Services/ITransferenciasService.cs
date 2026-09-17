using Colorsin.Application.Transferencias.DTOs;
using Colorsin.Domain.Transferencias;

namespace Colorsin.Application.Transferencias.Services;

/// <summary>Operaciones del modulo de traslados entre sedes.</summary>
public interface ITransferenciasService
{
    /// <summary>Transportadoras del catalogo, ordenadas por nombre.</summary>
    Task<IReadOnlyList<TransportadoraDto>> ObtenerTransportadorasAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Traslados, del mas reciente al mas antiguo. Sin movimientos ni novedades.</summary>
    Task<IReadOnlyList<TransferenciaDto>> ObtenerTransferenciasAsync(
        int? sucursalOrigenId = null,
        int? sucursalDestinoId = null,
        EstadoTransferencia? estado = null,
        int limite = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// El traslado con ese id, con su detalle de movimientos lote por lote y
    /// sus novedades. <c>null</c> si no existe.
    /// </summary>
    Task<TransferenciaDto?> ObtenerTransferenciaPorIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Solicita un traslado. Nace 'Solicitada' y NO mueve stock ni lo reserva:
    /// las existencias se validan y se descuentan al despachar.
    /// </summary>
    Task<ResultadoTransferencia> CrearAsync(
        CrearTransferenciaDto peticion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Despacha el traslado: la mercancia sale de la sede origen.
    ///
    /// En una sola transaccion valida disponibilidad, descuenta el saldo del
    /// origen, reparte el descuento entre sus lotes por FEFO, anexa un
    /// movimiento de Retiro/Transferencia por cada lote consumido, asigna
    /// transportadora y guia, y pasa el traslado a 'EnTransito'. O queda todo,
    /// o no queda nada.
    /// </summary>
    Task<ResultadoTransferencia> DespacharAsync(
        DespacharTransferenciaDto peticion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirma la llegada a la sede destino.
    ///
    /// En una sola transaccion sube el saldo del destino, recrea alli los lotes
    /// que salieron del origen con su mismo numero y vencimiento, anexa un
    /// movimiento de Ingreso/Transferencia por cada uno y cierra el traslado
    /// como 'Completada' o 'RecibidaParcial' segun lo que haya llegado.
    /// </summary>
    Task<ResultadoTransferencia> RecibirAsync(
        RecibirTransferenciaDto peticion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rechaza un traslado solicitado: la sede origen no lo atiende. Solo desde
    /// 'Solicitada', porque despues del despacho la mercancia ya salio.
    /// </summary>
    Task<ResultadoTransferencia> RechazarAsync(
        int transferenciaId,
        int usuarioId,
        string? motivo = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Anula un traslado solicitado, a peticion de quien lo pidio. Solo desde
    /// 'Solicitada', por la misma razon.
    /// </summary>
    Task<ResultadoTransferencia> CancelarAsync(
        int transferenciaId,
        int usuarioId,
        string? motivo = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deja constancia de un hallazgo sobre un traslado. No mueve stock ni
    /// cambia el estado: se puede registrar en cualquier momento, incluso
    /// despues de cerrado.
    /// </summary>
    Task<ResultadoNovedad> RegistrarNovedadAsync(
        RegistrarNovedadDto peticion,
        CancellationToken cancellationToken = default);
}
