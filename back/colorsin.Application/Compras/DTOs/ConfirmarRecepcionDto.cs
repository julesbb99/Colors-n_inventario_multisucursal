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
///
/// NO LLEVA UsuarioId, a proposito: quien recibe es un parametro del metodo y
/// sale del token. Aqui importa el doble, porque esta es la operacion que mueve
/// stock de verdad: quien firmo una entrada de mercancia tiene que ser quien
/// realmente estaba en la bodega.
/// </summary>
/// <param name="OrdenCompraId">Orden que se recibe.</param>
/// <param name="Lineas">
/// Que llego en esta entrega. OBLIGATORIA, y con al menos una linea.
///
/// Antes, vacia significaba "llego todo lo que faltaba". Ese atajo ya no cabe:
/// cada linea tiene que traer su numero de lote y su caducidad, y ninguno de los
/// dos se puede deducir de la orden -se leen de la etiqueta del envase cuando el
/// camion descarga-. Una entrega sin lineas se rechaza con
/// <see cref="ErrorCompra.NumeroLoteRequerido"/>.
///
/// SOLO se reciben las lineas listadas: las que no aparezcan quedan pendientes
/// para una entrega posterior. Es lo que permite decir "de las tres lineas solo
/// llego la primera".
/// </param>
/// <param name="Observaciones">Nota que se copia a cada movimiento generado.</param>
public sealed record ConfirmarRecepcionDto(
    int OrdenCompraId,
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
/// Numero impreso por el fabricante. OBLIGATORIO.
///
/// Sin el, la entrega subiria el saldo de la sede sin quedar atada a ninguna
/// fila de `lotes`, y con eso se pierden de golpe la trazabilidad hacia el
/// fabricante y el sitio donde vive la caducidad. Una entrega sin numero se
/// rechaza con <see cref="ErrorCompra.NumeroLoteRequerido"/>.
///
/// Si la sede ya tiene ese numero para ese producto, se le suma la cantidad en
/// vez de crear otra fila: un mismo numero de lote es el mismo lote, llegue en
/// uno o en tres despachos. El indice unico
/// `ux_lotes_producto_sucursal_numero` lo garantiza a nivel de motor.
/// </param>
/// <param name="FechaVencimiento">
/// Caducidad del lote. OBLIGATORIA.
///
/// Todo lo que vende Colorsin caduca, y esta es la clave con la que se ordena la
/// cola FEFO y se disparan las alertas. Se rechaza con
/// <see cref="ErrorCompra.FechaVencimientoRequerida"/>, y la propia columna
/// `lotes.fecha_vencimiento` es NOT NULL desde
/// `20_lote_vencimiento_obligatorio.sql`.
///
/// Si el lote ya existia, NO se sobrescribe: ver <c>RegistrarLoteAsync</c>.
///
/// SIGUEN SIENDO ANULABLES EN EL TIPO aunque el negocio las exija. Un
/// `DateOnly` no anulable haria que una peticion sin fecha reventara al
/// deserializar y volviera como un 400 generico de model binding; asi llega
/// vacia y se contesta con el motivo y el nombre del producto.
/// </param>
public sealed record LineaRecepcionDto(
    int DetalleId,
    decimal? Cantidad = null,
    string? NumeroLote = null,
    DateOnly? FechaVencimiento = null);
