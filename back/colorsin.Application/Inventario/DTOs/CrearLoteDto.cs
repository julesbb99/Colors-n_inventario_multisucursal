namespace Colorsin.Application.Inventario.DTOs;

/// <summary>
/// Alta de un lote.
///
/// NO LLEVA CANTIDAD, y esa es la decision de diseno importante de este DTO.
///
/// El saldo de un producto en una sede vive en DOS sitios que tienen que
/// cuadrar: `inventario_sucursal.cantidad_base`, que es el consolidado, y
/// `lotes.cantidad_base`, que es el desglose. Si crear un lote pudiera traer
/// cantidad, esa cantidad entraria SOLO en el desglose: el lote diria 200 L, el
/// consolidado de la sede seguiria igual, y nada avisaria de la diferencia. Peor
/// aun, seria stock aparecido de la nada, sin fila en el libro mayor que diga de
/// donde salio.
///
/// Por eso un lote nace VACIO, como un envase etiquetado y todavia sin
/// contenido. La mercancia entra despues, por una de las dos vias que si mueven
/// las dos tablas a la vez y dejan rastro:
///
///   POST /api/inventario/movimientos  con `loteId`, para un ingreso manual
///   POST /api/compras/ordenes/{id}/recepciones, que crea o alimenta el lote
///
/// Mientras este en cero no aparece en la cola FEFO ni en las alertas de
/// vencimiento, que es lo correcto: un lote sin existencias no se puede
/// despachar y su caducidad no le importa a nadie.
/// </summary>
/// <param name="ProductoId">Producto al que pertenece. Debe existir.</param>
/// <param name="SucursalId">
/// Sede donde queda fisicamente. Debe existir, y quien crea tiene que tener
/// acceso a ella: un gerente no puede abrir lotes en la bodega de otra sede.
/// </param>
/// <param name="NumeroLote">
/// El identificador impreso por el fabricante. Unico dentro de la pareja
/// (producto, sede): si el mismo lote vuelve a llegar se le suma cantidad, no se
/// abre otra fila. Lo garantiza el indice `ux_lotes_producto_sucursal_numero`.
/// </param>
/// <param name="FechaVencimiento">
/// Caducidad. NULA en los productos que no caducan, y es un caso legitimo, no un
/// campo olvidado: un lote sin fecha queda fuera de las alertas de vencimiento y
/// va al final de la cola FEFO.
///
/// Se admite una fecha ya pasada: cargar al sistema mercancia vieja que ya estaba
/// en bodega es un caso real. El lote nace vencido y sale marcado como tal.
/// </param>
/// <param name="FechaIngreso">
/// Cuando llego a la sede. Omitida, la pone la base con la hora del servidor.
/// Solo desempata el orden FEFO entre lotes que caducan el mismo dia.
/// </param>
public sealed record CrearLoteDto(
    int ProductoId,
    int SucursalId,
    string NumeroLote,
    DateOnly? FechaVencimiento = null,
    DateTime? FechaIngreso = null);
