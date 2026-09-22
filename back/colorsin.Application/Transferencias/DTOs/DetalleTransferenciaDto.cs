namespace Colorsin.Application.Transferencias.DTOs;

/// <summary>
/// Una linea del movimiento de stock que genero un traslado: cuanto salio o
/// entro, y de que lote.
///
/// OJO con el nombre: `transferencias` NO tiene tabla de detalle. Un traslado
/// mueve UN producto, no una lista. Lo que si tiene varias lineas es el
/// movimiento de inventario que lo respalda: FEFO puede repartir la cantidad
/// entre varios lotes, y cada reparto deja su propia fila en el libro mayor.
/// Eso es lo que representa este DTO, y es el detalle que de verdad hace falta
/// para reconstruir un traslado.
/// </summary>
/// <param name="MovimientoId">Fila del libro mayor.</param>
/// <param name="Tipo">'Ingreso' o 'Retiro'. El despacho genera retiros; la recepcion, ingresos.</param>
/// <param name="SucursalId">Sede afectada: el origen al despachar, el destino al recibir.</param>
/// <param name="SucursalNombre">Nombre de esa sede.</param>
/// <param name="LoteId">Lote afectado. Nulo si la cantidad salio sin lote asignado.</param>
/// <param name="NumeroLote">
/// Numero del lote. En la recepcion es el MISMO que en el despacho: el destino
/// recrea el lote con el numero y el vencimiento del origen para no perder la
/// trazabilidad del fabricante.
/// </param>
/// <param name="FechaVencimiento">Caducidad de ese lote.</param>
/// <param name="CantidadBase">Cantidad movida, en unidad base del producto.</param>
/// <param name="Fecha">Momento del movimiento.</param>
public sealed record DetalleTransferenciaDto(
    int MovimientoId,
    string? Tipo,
    int SucursalId,
    string SucursalNombre,
    int? LoteId,
    string? NumeroLote,
    DateOnly? FechaVencimiento,
    decimal? CantidadBase,
    DateTime? Fecha);

/// <summary>
/// Un lote que viaja en el traslado, con lo que salio de el.
///
/// POR QUE EXISTE APARTE DE <see cref="DetalleTransferenciaDto"/>. Aquel es una
/// fila del libro mayor: un traslado deja DOS por lote -el retiro del origen y
/// el ingreso del destino- y viene con su id de movimiento, su sede y su fecha.
/// Eso sirve para reconstruir el traslado, no para rotularlo.
///
/// Esto es lo que hace falta en una tabla: el numero que lleva impreso el
/// envase, para que quien descargue el camion pueda cotejar. Se deriva de los
/// movimientos de DESPACHO, que son los que dicen que salio de verdad; el
/// destino recrea esos mismos numeros al recibir, asi que no hay dos listas.
///
/// VIAJA EN EL LISTADO, a diferencia de los movimientos, que vienen vacios
/// alli: son pocos por traslado -lo normal es uno o dos lotes- y sin ellos la
/// tabla solo puede decir "Pintura Epoxica", que no distingue una tanda de otra.
/// </summary>
/// <param name="LoteId">
/// Lote del ORIGEN. Nulo cuando la cantidad salio sin lote asignado, que pasa
/// si el saldo consolidado de la sede supera la suma de sus lotes.
/// </param>
/// <param name="NumeroLote">
/// El numero del fabricante. Nulo en el mismo caso que <paramref name="LoteId"/>,
/// y la pantalla lo muestra como "sin lote" en vez de esconder la fila: esa
/// cantidad viaja igual.
/// </param>
/// <param name="FechaVencimiento">Caducidad del lote. Nula si el producto no caduca.</param>
/// <param name="CantidadBase">
/// Cuanto salio de ESE lote, en unidad base del producto. La suma de todos es
/// la cantidad despachada.
/// </param>
public sealed record LoteTrasladadoDto(
    int? LoteId,
    string? NumeroLote,
    DateOnly? FechaVencimiento,
    decimal CantidadBase);
