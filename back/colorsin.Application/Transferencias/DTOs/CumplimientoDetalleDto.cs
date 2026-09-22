namespace Colorsin.Application.Transferencias.DTOs;

/// <summary>
/// Como le fue a un traslado frente a su plazo.
///
/// SON CINCO CASOS Y NO DOS, y agruparlos en "cumplio / no cumplio" perderia lo
/// que hace falta para actuar: un traslado que aun no ha salido no es un
/// incumplimiento, y uno sin fecha estimada no se puede juzgar.
/// </summary>
public enum EstadoPlazo
{
    /// <summary>
    /// Todavia no ha salido del origen. No hay plazo que medir: la fecha
    /// estimada se fija al despachar.
    /// </summary>
    SinDespachar,

    /// <summary>Salio y aun no llega. El plazo sigue corriendo.</summary>
    EnTransito,

    /// <summary>Llego el dia previsto o antes.</summary>
    ATiempo,

    /// <summary>Llego despues del dia previsto.</summary>
    Tarde,

    /// <summary>
    /// Llego, pero sin fecha estimada con la que comparar.
    ///
    /// Son los traslados despachados antes de que la fecha fuera obligatoria.
    /// Se marcan asi y NO se cuentan ni a tiempo ni tarde: meterlos en
    /// cualquiera de los dos lados falsearia el porcentaje.
    /// </summary>
    SinPlazo,

    /// <summary>Se rechazo o se cancelo: nunca hubo viaje.</summary>
    NoAplica
}

/// <summary>
/// Una novedad, con lo justo para una celda de tabla.
///
/// NO SE REUSA <see cref="NovedadTransferenciaDto"/> a proposito: aquel lleva
/// quien la reporto, quien la cerro y las dos fechas, y la consulta del informe
/// no carga esas navegaciones. Devolverlo con los nombres en blanco diria "sin
/// usuario" donde en realidad dice "no se pregunto".
/// </summary>
/// <param name="Tipo">'Faltante', 'Averia', 'Sobrante' o 'Retraso'.</param>
/// <param name="CantidadAfectada">Cuanto producto involucra. En unidad del traslado.</param>
/// <param name="Tratamiento">'Ninguno', 'Reenvio', 'Reclamacion' o 'Asumido'.</param>
/// <param name="Estado">'Abierta' o 'Cerrada'.</param>
public sealed record NovedadResumenDto(
    string? Tipo,
    decimal? CantidadAfectada,
    string Tratamiento,
    string Estado);

/// <summary>
/// Un traslado en el informe de cumplimiento, con su producto, sus lotes y sus
/// fechas.
///
/// POR QUE EXISTE ADEMAS DEL AGREGADO. El informe por sede y por ruta contesta
/// "como vamos"; este contesta "cual fallo". Un 66 % de cumplimiento de plazo no
/// dice que traslado llego tarde, con que transportadora ni con que guia, y esos
/// tres datos son los que hacen falta para reclamar.
///
/// POR QUE NO ES <see cref="TransferenciaDto"/>. Aquel es el documento operativo
/// -lo que la pantalla de traslados necesita para ofrecer acciones- y trae
/// movimientos, urgencia, ids de usuario. Esto es una fila de informe: lleva las
/// cifras ya derivadas -dias de transito, desviacion, si llego completo- que
/// alli habria que recalcular en cada pintado.
/// </summary>
/// <param name="Id">Numero del traslado.</param>
/// <param name="Estado">Estado del traslado.</param>
/// <param name="ProductoNombre">Producto trasladado. Un traslado mueve uno solo.</param>
/// <param name="SucursalOrigenId">Sede que despacha.</param>
/// <param name="SucursalOrigenNombre">Nombre de esa sede.</param>
/// <param name="SucursalDestinoId">Sede que recibe.</param>
/// <param name="SucursalDestinoNombre">Nombre de esa sede.</param>
/// <param name="TransportadoraNombre">Quien lo llevo. Nulo si no llego a despacharse.</param>
/// <param name="Guia">
/// Numero de guia. Es con lo que se reclama, asi que en un informe de
/// cumplimiento no es un adorno: sin ella el faltante no se le puede cobrar a
/// nadie.
/// </param>
/// <param name="UnidadSimbolo">Unidad del traslado, la de las tres cantidades.</param>
/// <param name="UnidadBaseSimbolo">
/// Unidad base del producto, la de las cantidades de <paramref name="Lotes"/>.
/// Puede no ser la del traslado: 5 galones son 18,93 litros de lote.
/// </param>
/// <param name="CantidadSolicitada">Lo que pidio el destino.</param>
/// <param name="CantidadDespachada">Lo que salio de verdad. Nulo sin despachar.</param>
/// <param name="CantidadRecibida">Lo que llego. Nulo sin recibir.</param>
/// <param name="FechaSolicitud">Cuando se pidio.</param>
/// <param name="FechaDespacho">Cuando salio.</param>
/// <param name="FechaEstimadaLlegada">Cuando se esperaba.</param>
/// <param name="FechaRecepcion">Cuando llego.</param>
/// <param name="DiasTransito">
/// Dias reales entre despacho y recepcion, con un decimal. Nulo si falta alguna
/// de las dos fechas.
/// </param>
/// <param name="DiasDesviacion">
/// Dias de diferencia entre lo previsto y lo real: POSITIVO es tarde, negativo
/// es que llego antes. Nulo si no hay con que comparar.
///
/// Se cuenta por DIA de calendario y no por horas: la fecha estimada se guarda
/// a medianoche, asi que restar instantes daria medio dia de retraso a todo lo
/// que llega por la tarde del dia previsto.
/// </param>
/// <param name="Plazo">Como le fue frente a su fecha estimada.</param>
/// <param name="LlegoCompleto">
/// Si llego todo LO DESPACHADO. Nulo mientras no se reciba.
///
/// Contra lo despachado y no contra lo solicitado: si el origen ajusto el envio
/// a 3 de los 5 pedidos y llegaron los 3, el traslado llego completo. Lo que el
/// origen no mando esta en <paramref name="AjustadoEnOrigen"/>, que es otra cosa
/// y tiene otro responsable.
/// </param>
/// <param name="AjustadoEnOrigen">
/// Si el origen despacho menos de lo pedido. NO es una perdida: esa mercancia
/// nunca salio, sigue en su estante.
/// </param>
/// <param name="Lotes">Los lotes que viajaron, en orden FEFO.</param>
/// <param name="Novedades">Las novedades reportadas, de la mas reciente a la mas antigua.</param>
/// <param name="NovedadesAbiertas">Cuantas siguen esperando desenlace.</param>
public sealed record CumplimientoTrasladoDto(
    int Id,
    string? Estado,
    string ProductoNombre,
    int SucursalOrigenId,
    string SucursalOrigenNombre,
    int SucursalDestinoId,
    string SucursalDestinoNombre,
    string? TransportadoraNombre,
    string? Guia,
    string UnidadSimbolo,
    string? UnidadBaseSimbolo,
    decimal? CantidadSolicitada,
    decimal? CantidadDespachada,
    decimal? CantidadRecibida,
    DateTime? FechaSolicitud,
    DateTime? FechaDespacho,
    DateTime? FechaEstimadaLlegada,
    DateTime? FechaRecepcion,
    decimal? DiasTransito,
    int? DiasDesviacion,
    string Plazo,
    bool? LlegoCompleto,
    bool AjustadoEnOrigen,
    IReadOnlyList<LoteTrasladadoDto> Lotes,
    IReadOnlyList<NovedadResumenDto> Novedades,
    int NovedadesAbiertas);

/// <summary>
/// El informe de cumplimiento traslado por traslado.
///
/// LLEVA TOPE DE FILAS, a diferencia del agregado. Aquel devuelve un punado de
/// grupos por muchos traslados que haya, asi que puede recorrer el periodo
/// entero sin limite; este devuelve una fila por traslado y con sus lotes, y sin
/// tope una consulta de "todo el historico" crecerian sin freno.
/// </summary>
/// <param name="Desde">Inicio del periodo, o nulo si no se acoto.</param>
/// <param name="Hasta">Fin del periodo, o nulo si no se acoto.</param>
/// <param name="SucursalId">Sede consultada, o nulo si es toda la red.</param>
/// <param name="Traslados">
/// Del mas reciente al mas antiguo, por fecha de solicitud. Es el orden en que
/// se buscan las cosas: lo de ayer antes que lo del mes pasado.
/// </param>
/// <param name="Limite">Tope aplicado.</param>
/// <param name="HayMas">
/// Si el tope dejo traslados fuera.
///
/// Va explicito y no se deduce de <c>Traslados.Count == Limite</c>: esa
/// comparacion falla justo cuando el total coincide con el tope, y entonces
/// avisaria de filas que no existen.
/// </param>
public sealed record CumplimientoDetalleDto(
    DateTime? Desde,
    DateTime? Hasta,
    int? SucursalId,
    IReadOnlyList<CumplimientoTrasladoDto> Traslados,
    int Limite,
    bool HayMas);
