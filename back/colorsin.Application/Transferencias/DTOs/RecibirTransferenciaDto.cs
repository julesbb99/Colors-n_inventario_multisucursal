namespace Colorsin.Application.Transferencias.DTOs;

/// <summary>
/// Peticion para confirmar que un traslado llego a la sede destino.
///
/// En una sola transaccion sube el saldo del destino, recrea alli los lotes que
/// salieron del origen -con su mismo numero y vencimiento-, anexa un movimiento
/// de Ingreso/Transferencia por cada lote y cierra el traslado.
/// </summary>
/// <param name="TransferenciaId">Traslado a recibir. Debe estar 'EnTransito'.</param>
/// <param name="UsuarioId">
/// Quien recibe. Queda en cada movimiento de inventario y en la auditoria.
/// </param>
/// <param name="CantidadRecibida">
/// Lo que llego, en la unidad del traslado.
///
/// Nulo significa "llego todo lo que se despacho", que es el caso normal. Si se
/// indica menos, el traslado queda 'RecibidaParcial' y la diferencia se da por
/// perdida en transito: salio del origen y nunca entro al destino, asi que el
/// libro mayor la refleja como una baja neta de la red. Conviene acompanarla de
/// una novedad de tipo Faltante para dejar constancia del porque.
///
/// No admite mas de lo despachado: recibir de mas suele ser un error de
/// digitacion, y aceptarlo crearia stock de la nada.
/// </param>
/// <param name="Observaciones">Nota que se copia a cada movimiento generado.</param>
public sealed record RecibirTransferenciaDto(
    int TransferenciaId,
    int UsuarioId,
    decimal? CantidadRecibida = null,
    string? Observaciones = null);
