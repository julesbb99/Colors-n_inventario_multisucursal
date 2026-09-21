using Colorsin.Domain.Transferencias;

namespace Colorsin.Application.Transferencias.DTOs;

/// <summary>
/// Por que se rechazo una operacion de traslados.
///
/// Enum y no texto libre, igual que en Inventario y Compras: la capa HTTP
/// traduce cada caso a su codigo (404 lo que no existe, 409 el conflicto de
/// estado o de stock, 400 lo mal digitado) sin comparar mensajes.
/// </summary>
public enum ErrorTransferencia
{
    /// <summary>Sin error.</summary>
    Ninguno = 0,

    /// <summary>La cantidad venia en cero o negativa.</summary>
    CantidadInvalida,

    /// <summary>No vino el usuario responsable.</summary>
    UsuarioNoIndicado,

    /// <summary>Origen y destino son la misma sede.</summary>
    SedesIguales,

    /// <summary>El traslado no existe.</summary>
    TransferenciaNoEncontrada,

    /// <summary>El producto no existe.</summary>
    ProductoNoEncontrado,

    /// <summary>El producto no tiene unidad base: no hay a que convertir.</summary>
    ProductoSinUnidadBase,

    /// <summary>La unidad del traslado no existe.</summary>
    UnidadNoEncontrada,

    /// <summary>
    /// La unidad del traslado y la unidad base del producto no son convertibles
    /// entre si: alguna no tiene factor a litros.
    /// </summary>
    ConversionImposible,

    /// <summary>Convertida a unidad base, la cantidad se redondea a cero.</summary>
    CantidadBaseCero,

    /// <summary>La transportadora no existe.</summary>
    TransportadoraNoEncontrada,

    /// <summary>
    /// La transportadora esta retirada del catalogo.
    ///
    /// Distinto de que no exista: existe y tiene historia, pero se dejo de
    /// trabajar con ella. La pantalla ya no la ofrece; esto cierra la via de
    /// quien llame al endpoint con un id viejo.
    /// </summary>
    TransportadoraRetirada,

    /// <summary>Falta el numero de guia.</summary>
    GuiaNoIndicada,

    /// <summary>
    /// Falta la fecha estimada de llegada.
    ///
    /// Es obligatoria desde que las transportadoras declaran sus dias de
    /// entrega: sin ella no hay a partir de cuando decir que el traslado va
    /// tarde, ni con que comparar cuando llegue.
    /// </summary>
    FechaEstimadaNoIndicada,

    /// <summary>La fecha estimada de llegada es anterior a hoy.</summary>
    FechaEstimadaInvalida,

    /// <summary>El traslado no esta en un estado que admita esta operacion.</summary>
    EstadoNoPermiteOperacion,

    /// <summary>La sede origen no tiene saldo suficiente para despachar.</summary>
    StockInsuficiente,

    /// <summary>No hay fila de saldo para esa pareja (sede origen, producto).</summary>
    SaldoNoEncontrado,

    /// <summary>
    /// La cantidad recibida supera la despachada. Aceptarlo crearia stock de la
    /// nada, asi que se rechaza en vez de recortarse en silencio.
    /// </summary>
    RecibidaExcedeDespachada,

    /// <summary>
    /// Se intenta despachar mas de lo que se pidio.
    ///
    /// El ajuste del origen existe para mandar MENOS. De mas seria stock que el
    /// destino no pidio y que su bodega no espera.
    /// </summary>
    DespachadaExcedeSolicitada,

    /// <summary>
    /// Se intenta recibir antes de la fecha estimada de llegada.
    ///
    /// La recepcion no se habilita hasta ese dia: contar mercancia que segun la
    /// guia todavia viaja solo puede salir de un conteo a ojo.
    /// </summary>
    RecepcionAnticipada,

    /// <summary>La novedad no existe.</summary>
    NovedadNoEncontrada,

    /// <summary>La novedad ya estaba cerrada: no hay desenlace que registrar.</summary>
    NovedadYaCerrada,

    /// <summary>
    /// Se intenta cerrar una novedad sin decir por que.
    ///
    /// El motivo es obligatorio a proposito: sin el, cerrar seria
    /// indistinguible de borrar, y lo que se guarda es justamente para poder
    /// revisarlo despues.
    /// </summary>
    MotivoCierreNoIndicado
}

/// <summary>Desenlace de una operacion sobre un traslado.</summary>
/// <param name="Exito">Si la operacion se completo.</param>
/// <param name="Error">Motivo del rechazo.</param>
/// <param name="Mensaje">Explicacion legible, con los numeros concretos del caso.</param>
/// <param name="TransferenciaId">Traslado afectado.</param>
/// <param name="Estado">Como quedo el traslado.</param>
/// <param name="CantidadBase">
/// Cantidad movida en unidad base del producto. Nula en las operaciones que no
/// mueven stock (crear, rechazar, cancelar, registrar novedad).
/// </param>
/// <param name="SaldoResultante">Saldo de la sede afectada tras la operacion.</param>
/// <param name="Movimientos">Lo que quedo en el libro mayor, lote por lote.</param>
public sealed record ResultadoTransferencia(
    bool Exito,
    ErrorTransferencia Error,
    string Mensaje,
    int? TransferenciaId = null,
    string? Estado = null,
    decimal? CantidadBase = null,
    decimal? SaldoResultante = null,
    IReadOnlyList<DetalleTransferenciaDto>? Movimientos = null)
{
    public static ResultadoTransferencia Fallo(ErrorTransferencia error, string mensaje) =>
        new(false, error, mensaje);

    public static ResultadoTransferencia Ok(
        int transferenciaId,
        EstadoTransferencia estado,
        string mensaje,
        decimal? cantidadBase = null,
        decimal? saldoResultante = null,
        IReadOnlyList<DetalleTransferenciaDto>? movimientos = null) =>
        new(true, ErrorTransferencia.Ninguno, mensaje,
            transferenciaId, estado.ToString(), cantidadBase, saldoResultante, movimientos);
}

/// <summary>Desenlace de registrar una novedad.</summary>
/// <param name="Exito">Si quedo registrada.</param>
/// <param name="Error">Motivo del rechazo.</param>
/// <param name="Mensaje">Explicacion legible.</param>
/// <param name="NovedadId">Id de la novedad creada.</param>
public sealed record ResultadoNovedad(
    bool Exito,
    ErrorTransferencia Error,
    string Mensaje,
    int? NovedadId = null)
{
    public static ResultadoNovedad Fallo(ErrorTransferencia error, string mensaje) =>
        new(false, error, mensaje);

    /// <param name="cerroElTraslado">
    /// Si ademas paso el traslado a <c>Cerrada</c>, que ocurre cuando venia de
    /// <c>RecibidaParcial</c> y la novedad no dejo nada pendiente.
    ///
    /// El mensaje TIENE que decirlo. Antes decia siempre "no afecta el estado
    /// del traslado", y desde que la novedad lo cierra eso era falso justo en el
    /// caso mas frecuente.
    /// </param>
    /// <param name="quedaAbierta">
    /// Si la novedad espera desenlace -un reenvio o una reclamacion-. En ese
    /// caso el traslado NO se cierra: sigue pendiente hasta que se resuelva.
    /// </param>
    public static ResultadoNovedad Ok(
        int novedadId,
        TipoNovedad tipo,
        bool cerroElTraslado,
        bool quedaAbierta = false) =>
        new(true, ErrorTransferencia.Ninguno,
            quedaAbierta
                ? $"Novedad de tipo {tipo} registrada y PENDIENTE de desenlace. El traslado sigue " +
                  "por recibir hasta que se cierre. No se movio saldo."
                : cerroElTraslado
                    ? $"Novedad de tipo {tipo} registrada. El traslado queda CERRADO: llego corto " +
                      "y ya se dio cuenta del faltante. No se movio saldo."
                    : $"Novedad de tipo {tipo} registrada. No afecta el saldo ni el estado del " +
                      "traslado.",
            novedadId);

    /// <summary>Desenlace de cerrar una novedad que estaba pendiente.</summary>
    /// <param name="novedadId">La novedad cerrada.</param>
    /// <param name="cerroElTraslado">
    /// Si con esta se acabaron las pendientes y el traslado paso a
    /// <c>Cerrada</c>. Falso cuando todavia le quedan otras abiertas.
    /// </param>
    public static ResultadoNovedad Cerrada(int novedadId, bool cerroElTraslado) =>
        new(true, ErrorTransferencia.Ninguno,
            cerroElTraslado
                ? "Novedad cerrada con su motivo. Era la ultima pendiente, asi que el traslado " +
                  "queda CERRADO. No se borro nada: el reporte original sigue con su tipo, su " +
                  "cantidad y quien lo firmo."
                : "Novedad cerrada con su motivo. El traslado sigue pendiente porque le quedan " +
                  "otras novedades abiertas.",
            novedadId);
}
