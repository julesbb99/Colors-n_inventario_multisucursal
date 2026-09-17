namespace Colorsin.Application.Compras.DTOs;

/// <summary>
/// Peticion para registrar que llego mercancia de una orden.
///
/// Es la operacion que mueve stock de verdad: sube las existencias, anexa un
/// movimiento de Ingreso por linea, registra o engrosa los lotes recibidos y
/// deja el estado de la orden segun lo que falte.
///
/// Admite entregas parciales y se puede llamar varias veces sobre la misma
/// orden. Cada llamada ACUMULA sobre lo ya recibido; cuando todas las lineas
/// completan lo pedido, la orden pasa a 'Recibida' y deja de admitir mas.
/// </summary>
/// <param name="OrdenCompraId">Orden que se recibe.</param>
/// <param name="UsuarioId">
/// Quien recibe. Queda en cada movimiento de inventario y en la auditoria, y
/// debe existir en `usuarios`: las dos tablas tienen FK obligatoria.
/// </param>
/// <param name="Lineas">
/// Que llego en esta entrega.
///
/// Vacia o nula significa "llego todo lo que faltaba", que es el caso comun de
/// una entrega completa. Si se indica, SOLO se reciben las lineas listadas: las
/// que no aparezcan quedan pendientes para una entrega posterior. Es lo que
/// permite decir "de las tres lineas solo llego la primera".
/// </param>
/// <param name="Observaciones">Nota que se copia a cada movimiento generado.</param>
public sealed record ConfirmarRecepcionDto(
    int OrdenCompraId,
    int UsuarioId,
    IReadOnlyList<LineaRecepcionDto>? Lineas = null,
    string? Observaciones = null);

/// <summary>
/// Una linea de la orden dentro de una entrega concreta.
///
/// La informacion de lote va aqui y no en la orden a proposito: los numeros de
/// lote y las fechas de vencimiento se leen de las etiquetas cuando el camion
/// descarga, no cuando se hace el pedido. Y como cada entrega puede traer un
/// lote distinto, tiene que viajar por entrega, no por linea de la orden.
/// </summary>
/// <param name="DetalleId">Linea de la orden que se esta recibiendo.</param>
/// <param name="Cantidad">
/// Cuanto llego en esta entrega, en la unidad de compra de la linea (la misma
/// de `cantidad`, no la unidad base).
///
/// Nulo significa "todo lo que falta de esta linea". Si se indica, debe ser
/// mayor que cero y no superar lo pendiente; recibir de mas se rechaza en vez
/// de recortarse en silencio, porque suele significar que se digito la linea
/// equivocada.
/// </param>
/// <param name="NumeroLote">
/// Numero impreso por el fabricante. Opcional: sin el, la entrega sube el saldo
/// de la sede sin trazabilidad por lote.
///
/// Si la sede ya tiene ese numero para ese producto, se le suma la cantidad en
/// vez de crear otra fila: un mismo numero de lote es el mismo lote, llegue en
/// uno o en tres despachos. El indice unico
/// `ux_lotes_producto_sucursal_numero` lo garantiza a nivel de motor.
/// </param>
/// <param name="FechaVencimiento">
/// Caducidad del lote. Nula en productos que no caducan, lo que los manda al
/// final de la cola FEFO. Si el lote ya existia, NO se sobrescribe.
/// </param>
public sealed record LineaRecepcionDto(
    int DetalleId,
    decimal? Cantidad = null,
    string? NumeroLote = null,
    DateOnly? FechaVencimiento = null);
