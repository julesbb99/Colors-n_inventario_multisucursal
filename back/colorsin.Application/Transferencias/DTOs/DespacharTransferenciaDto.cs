namespace Colorsin.Application.Transferencias.DTOs;

/// <summary>
/// Peticion para despachar un traslado: la mercancia sale de la sede origen.
///
/// Es la operacion que mueve stock de verdad. En una sola transaccion valida
/// disponibilidad, descuenta el saldo del origen, reparte el descuento entre
/// sus lotes por FEFO, anexa un movimiento de Retiro/Transferencia al libro
/// mayor por cada lote consumido, asigna transportadora y guia, y pasa el
/// traslado a 'EnTransito'.
/// </summary>
/// <param name="TransferenciaId">Traslado a despachar. Debe estar 'Solicitada'.</param>
/// <param name="UsuarioId">
/// Quien despacha. Queda en cada movimiento de inventario y en la auditoria, y
/// debe existir en `usuarios`: las dos tablas tienen FK obligatoria.
/// </param>
/// <param name="TransportadoraId">Quien lleva la carga. Debe existir en `transportadoras`.</param>
/// <param name="Guia">
/// Numero de guia del transportador, maximo 50 caracteres. Es con lo que se
/// reclama si la carga llega mal o no llega.
/// </param>
/// <param name="FechaEstimadaLlegada">Cuando se espera que llegue. Opcional.</param>
/// <param name="Observaciones">Nota que se copia a cada movimiento generado.</param>
public sealed record DespacharTransferenciaDto(
    int TransferenciaId,
    int UsuarioId,
    int TransportadoraId,
    string Guia,
    DateTime? FechaEstimadaLlegada = null,
    string? Observaciones = null);
