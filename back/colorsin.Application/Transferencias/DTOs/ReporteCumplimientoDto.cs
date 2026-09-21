namespace Colorsin.Application.Transferencias.DTOs;

/// <summary>
/// Cumplimiento logistico de un grupo de traslados: una sede, o una ruta.
///
/// TODO SON CONTEOS, NUNCA VOLUMENES, y es una decision deliberada y no una
/// simplificacion. Un traslado lleva litros, otro galones y otro canecas, y
/// cada producto tiene su propia unidad base: sumar sus cantidades daria un
/// numero con unidades mezcladas que no significa nada. Lo que si se puede
/// comparar entre traslados distintos es cuantos llegaron completos y cuantos
/// llegaron a tiempo.
///
/// LOS TRES CUMPLIMIENTOS SON DISTINTOS Y NO SE FUNDEN EN UNO:
///
///   atencion   de lo que le pidieron al origen, cuanto despacho. Mide a la
///              BODEGA: si rechaza la mitad de las peticiones, aqui se ve.
///   cantidad   de lo que salio, cuanto llego entero. Mide el TRANSPORTE en
///              mercancia.
///   plazo      de lo que llego, cuanto llego dentro de la fecha estimada. Mide
///              el TRANSPORTE en tiempo.
///
/// Una transportadora puede entregar siempre a tiempo y perder producto en cada
/// viaje; una bodega puede despacharlo todo y que nunca llegue. Un solo
/// porcentaje escondería cual de las tres cosas esta fallando.
/// </summary>
/// <param name="Clave">Identificador estable del grupo, para la tabla.</param>
/// <param name="Etiqueta">Como se lee: el nombre de la sede o «Origen → Destino».</param>
/// <param name="SucursalOrigenId">Sede origen. Nula en el agrupado por sede.</param>
/// <param name="SucursalDestinoId">Sede destino. Nula en el agrupado por sede.</param>
/// <param name="Solicitados">Traslados del grupo, sea cual sea su desenlace.</param>
/// <param name="Despachados">Los que llegaron a salir del origen.</param>
/// <param name="Rechazados">Los que el origen no atendio.</param>
/// <param name="Cancelados">Los que retiro quien los pidio.</param>
/// <param name="EnCurso">
/// Todavia vivos: solicitados sin despachar, en transito, o recibidos con una
/// novedad sin resolver.
/// </param>
/// <param name="Recibidos">Los que la sede destino ya conto.</param>
/// <param name="Completos">De los recibidos, los que llegaron enteros.</param>
/// <param name="Parciales">De los recibidos, los que llegaron cortos.</param>
/// <param name="ATiempo">De los recibidos, los que llegaron dentro de la fecha estimada.</param>
/// <param name="Tarde">De los recibidos, los que se pasaron.</param>
/// <param name="AjustadosEnOrigen">
/// Despachos en los que el origen mando menos de lo pedido. No es un
/// incumplimiento del transporte: es la bodega diciendo que no tenia todo.
/// </param>
/// <param name="ConNovedad">Traslados con al menos una novedad registrada.</param>
/// <param name="NovedadesAbiertas">Novedades del grupo que siguen esperando desenlace.</param>
/// <param name="DiasTransitoPromedio">
/// Media de dias entre despacho y recepcion, solo sobre los recibidos. Nula si
/// no hay ninguno.
/// </param>
/// <param name="CumplimientoAtencion">Despachados / (solicitados − cancelados), en %. Nulo sin base.</param>
/// <param name="CumplimientoCantidad">Completos / recibidos, en %. Nulo sin recibidos.</param>
/// <param name="CumplimientoPlazo">A tiempo / recibidos, en %. Nulo sin recibidos.</param>
public sealed record CumplimientoGrupoDto(
    string Clave,
    string Etiqueta,
    int? SucursalOrigenId,
    int? SucursalDestinoId,
    int Solicitados,
    int Despachados,
    int Rechazados,
    int Cancelados,
    int EnCurso,
    int Recibidos,
    int Completos,
    int Parciales,
    int ATiempo,
    int Tarde,
    int AjustadosEnOrigen,
    int ConNovedad,
    int NovedadesAbiertas,
    decimal? DiasTransitoPromedio,
    decimal? CumplimientoAtencion,
    decimal? CumplimientoCantidad,
    decimal? CumplimientoPlazo);

/// <summary>
/// El informe completo: el total, el desglose por sede y el desglose por ruta.
/// </summary>
/// <param name="Desde">Inicio del periodo, o nulo si no se acoto.</param>
/// <param name="Hasta">Fin del periodo, o nulo si no se acoto.</param>
/// <param name="Total">Todo junto, para tener contra que comparar cada fila.</param>
/// <param name="PorSucursal">
/// Una fila por sede, contando los traslados que DESPACHA.
///
/// Se agrupa por el ORIGEN y no por el destino porque es el origen quien
/// responde por atender la peticion y por entregar lo que dijo. El destino solo
/// cuenta lo que le llega; medirlo por ahi seria calificar a una sede por el
/// trabajo de otra.
/// </param>
/// <param name="PorRuta">Una fila por pareja origen → destino.</param>
public sealed record ReporteCumplimientoDto(
    DateTime? Desde,
    DateTime? Hasta,
    CumplimientoGrupoDto Total,
    IReadOnlyList<CumplimientoGrupoDto> PorSucursal,
    IReadOnlyList<CumplimientoGrupoDto> PorRuta);
