namespace Colorsin.Application.Transferencias.DTOs;

/// <summary>Incidencia reportada sobre un traslado.</summary>
/// <param name="Id">Clave primaria.</param>
/// <param name="TransferenciaId">Traslado sobre el que se reporto.</param>
/// <param name="UsuarioId">Quien la reporto.</param>
/// <param name="UsuarioNombre">Nombre de esa persona.</param>
/// <param name="Tipo">'Faltante', 'Averia', 'Sobrante' o 'Retraso'.</param>
/// <param name="CantidadAfectada">
/// Cuanto producto involucra, cuando aplica. Es informativa: registrar una
/// novedad NO mueve stock. Lo que cambia el saldo es la cantidad recibida.
/// </param>
/// <param name="Tratamiento">
/// 'Ninguno', 'Reenvio', 'Reclamacion' o 'Asumido'. Los dos del medio dejan el
/// traslado pendiente; los otros dos no.
/// </param>
/// <param name="Estado">'Abierta' o 'Cerrada'.</param>
/// <param name="MotivoCierre">El porque del cierre. Nulo mientras sigue abierta.</param>
/// <param name="FechaCierre">Cuando se cerro.</param>
/// <param name="UsuarioCierreNombre">
/// Quien la cerro. Distinto de quien la reporto: entre las dos cosas suelen
/// pasar dias, y a veces no es la misma persona ni la misma sede.
/// </param>
/// <param name="Observaciones">Descripcion libre del hallazgo.</param>
/// <param name="Fecha">Momento del reporte.</param>
public sealed record NovedadTransferenciaDto(
    int Id,
    int TransferenciaId,
    int UsuarioId,
    string UsuarioNombre,
    string? Tipo,
    decimal? CantidadAfectada,
    string Tratamiento,
    string Estado,
    string? MotivoCierre,
    DateTime? FechaCierre,
    string? UsuarioCierreNombre,
    string? Observaciones,
    DateTime? Fecha);

/// <summary>Traslado de un producto entre dos sedes.</summary>
/// <param name="Id">Clave primaria.</param>
/// <param name="ProductoId">Producto trasladado.</param>
/// <param name="ProductoNombre">Nombre del producto.</param>
/// <param name="SucursalOrigenId">Sede que despacha.</param>
/// <param name="SucursalOrigenNombre">Nombre de esa sede.</param>
/// <param name="SucursalDestinoId">Sede que recibe.</param>
/// <param name="SucursalDestinoNombre">Nombre de esa sede.</param>
/// <param name="UsuarioId">
/// Quien SOLICITO el traslado. Quien despacho y quien recibio no salen aqui:
/// quedan en el `usuario_id` de sus movimientos, en <paramref name="Movimientos"/>.
/// </param>
/// <param name="UsuarioNombre">Nombre de esa persona.</param>
/// <param name="TransportadoraId">Quien lo mueve. Nulo mientras esta 'Solicitada'.</param>
/// <param name="TransportadoraNombre">Razon social de la transportadora.</param>
/// <param name="Guia">Numero de guia. Nulo hasta el despacho.</param>
/// <param name="CantidadSolicitada">Lo pedido, en <paramref name="UnidadId"/>.</param>
/// <param name="CantidadDespachada">
/// Lo que de verdad salio del origen. Nulo hasta el despacho. Menor que lo
/// solicitado significa que el origen no tenia todo; esa diferencia NO es una
/// perdida, sigue en su estante.
/// </param>
/// <param name="CantidadRecibida">
/// Lo que llego, en la misma unidad. Nulo hasta que la sede destino confirma.
/// Menor que lo DESPACHADO significa que algo se perdio en transito.
/// </param>
/// <param name="UnidadId">Unidad del traslado, que puede no ser la unidad base del producto.</param>
/// <param name="UnidadSimbolo">Abreviatura de esa unidad.</param>
/// <param name="UnidadBaseSimbolo">
/// Abreviatura de la unidad BASE del producto, que puede no ser la del traslado.
///
/// HACE FALTA PARA ROTULAR LOS LOTES. Las cantidades de
/// <paramref name="Lotes"/> van en unidad base -asi las guarda el libro mayor-
/// mientras que <paramref name="CantidadSolicitada"/> y las otras dos van en la
/// unidad del traslado. Un traslado de 5 GALONES mueve 18,93 LITROS de lote, y
/// rotular ese 18,93 con el simbolo del traslado diria "18,93 gal": un error de
/// un factor 3,785 en la cifra que alguien va a cotejar contra el envase.
///
/// Nula si el producto no tiene unidad base definida.
/// </param>
/// <param name="Estado">'Solicitada', 'EnTransito', 'Completada', 'RecibidaParcial', 'Rechazada' o 'Cancelada'.</param>
/// <param name="Urgencia">'Baja', 'Media' o 'Alta'.</param>
/// <param name="FechaSolicitud">Cuando se pidio.</param>
/// <param name="FechaDespacho">Cuando salio del origen. Nula mientras esta 'Solicitada'.</param>
/// <param name="FechaEstimadaLlegada">Cuando se espera que llegue. Se fija al despachar.</param>
/// <param name="FechaRecepcion">
/// Cuando la conto el destino. Contra la estimada dice si llego tarde; menos la
/// de despacho, el transito real.
/// </param>
/// <param name="NovedadesAbiertas">
/// Cuantas novedades siguen esperando desenlace.
///
/// VIAJA EN EL LISTADO aunque <paramref name="Novedades"/> venga vacia: la
/// pantalla necesita saber si el traslado tiene algo pendiente sin cargar el
/// detalle de las cien filas.
/// </param>
/// <param name="Lotes">
/// Los lotes que viajan, con lo que salio de cada uno.
///
/// SI VIAJA EN EL LISTADO, a diferencia de <paramref name="Movimientos"/>: son
/// pocos por traslado y son lo que permite cotejar el camion contra el papel.
/// Sin ellos la tabla solo puede decir "Pintura Epoxica", que no distingue una
/// tanda de otra ni dice que vence.
///
/// Vacia mientras el traslado esta 'Solicitada': hasta el despacho no ha salido
/// nada, asi que no hay lote que nombrar.
/// </param>
/// <param name="Movimientos">
/// Lo que el traslado movio en el stock, lote por lote. Viene vacia en los
/// listados, que no cargan el libro mayor de cada fila.
/// </param>
/// <param name="Novedades">Incidencias reportadas. Vacia en los listados.</param>
public sealed record TransferenciaDto(
    int Id,
    int ProductoId,
    string ProductoNombre,
    int SucursalOrigenId,
    string SucursalOrigenNombre,
    int SucursalDestinoId,
    string SucursalDestinoNombre,
    int UsuarioId,
    string UsuarioNombre,
    int? TransportadoraId,
    string? TransportadoraNombre,
    string? Guia,
    decimal? CantidadSolicitada,
    decimal? CantidadDespachada,
    decimal? CantidadRecibida,
    int UnidadId,
    string UnidadSimbolo,
    string? UnidadBaseSimbolo,
    string? Estado,
    string? Urgencia,
    DateTime? FechaSolicitud,
    DateTime? FechaDespacho,
    DateTime? FechaEstimadaLlegada,
    DateTime? FechaRecepcion,
    int NovedadesAbiertas,
    IReadOnlyList<LoteTrasladadoDto> Lotes,
    IReadOnlyList<DetalleTransferenciaDto> Movimientos,
    IReadOnlyList<NovedadTransferenciaDto> Novedades);
