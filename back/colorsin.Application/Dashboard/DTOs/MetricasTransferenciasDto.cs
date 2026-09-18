namespace Colorsin.Application.Dashboard.DTOs;

/// <summary>
/// Incidencia reciente sobre un traslado, con el contexto necesario para
/// entenderla sin abrir el traslado.
///
/// Existe aparte de <c>NovedadTransferenciaDto</c> porque aquel se lee DENTRO
/// de una transferencia, donde producto y sedes ya estan en pantalla. En un
/// muro de novedades de toda la red no hay ese contexto: "Faltante, 12 L" no
/// sirve sin saber de que producto y entre que dos bodegas.
/// </summary>
/// <param name="Id">Clave primaria de la novedad.</param>
/// <param name="TransferenciaId">Traslado sobre el que se reporto.</param>
/// <param name="ProductoId">Producto trasladado.</param>
/// <param name="ProductoNombre">Nombre del producto.</param>
/// <param name="SucursalOrigenId">Sede que despacho.</param>
/// <param name="SucursalOrigenNombre">Nombre de esa sede.</param>
/// <param name="SucursalDestinoId">Sede que recibe.</param>
/// <param name="SucursalDestinoNombre">Nombre de esa sede.</param>
/// <param name="Tipo">'Faltante', 'Averia', 'Sobrante' o 'Retraso'.</param>
/// <param name="CantidadAfectada">
/// Cuanto producto involucra, cuando aplica. Es informativa: una novedad NO
/// mueve stock, lo que cambia el saldo es la cantidad recibida.
/// </param>
/// <param name="Observaciones">Descripcion libre del hallazgo.</param>
/// <param name="UsuarioId">Quien la reporto.</param>
/// <param name="UsuarioNombre">Nombre de esa persona.</param>
/// <param name="Fecha">Momento del reporte.</param>
public sealed record NovedadRecienteDto(
    int Id,
    int TransferenciaId,
    int ProductoId,
    string ProductoNombre,
    int SucursalOrigenId,
    string SucursalOrigenNombre,
    int SucursalDestinoId,
    string SucursalDestinoNombre,
    string? Tipo,
    decimal? CantidadAfectada,
    string? Observaciones,
    int UsuarioId,
    string UsuarioNombre,
    DateTime? Fecha);

/// <summary>
/// Estado de la logistica: cuantos traslados hay en cada etapa y que se ha
/// reportado ultimamente.
///
/// Los conteos van como propiedades con nombre y no como una lista
/// (estado, cantidad) a proposito. La lista de estados esta cerrada y ya vive
/// duplicada en dos sitios que deben moverse juntos, el enum
/// <c>EstadoTransferencia</c> y el ENUM de MySQL; agregar un estado obliga a
/// tocar los dos, y asi obliga tambien a tocar este record, con el compilador
/// senalando donde. A cambio, quien consume no tiene que buscar una clave en
/// una lista ni decidir que hacer si falta.
/// </summary>
/// <param name="SucursalId">
/// Sede consultada, o <c>null</c> si es toda la red. Un traslado se cuenta si
/// la sede es su ORIGEN o su DESTINO: los dos lados tienen algo pendiente.
/// </param>
/// <param name="SucursalNombre">Nombre de esa sede. Nulo cuando el alcance es la red.</param>
/// <param name="Total">
/// Traslados considerados. Es la suma de los siete conteos que siguen.
/// </param>
/// <param name="Solicitadas">Pedidas y sin despachar. Esperan al origen.</param>
/// <param name="EnTransito">Despachadas y sin recibir. El stock ya salio del origen.</param>
/// <param name="Completadas">Llego todo lo que se despacho.</param>
/// <param name="RecibidasParcial">Llego menos de lo despachado.</param>
/// <param name="Rechazadas">El origen no las atendio.</param>
/// <param name="Canceladas">Anuladas por quien las pidio, antes del despacho.</param>
/// <param name="SinEstado">
/// Filas con <c>estado</c> NULL.
///
/// La columna admite nulos, asi que sin este conteo <paramref name="Total"/> no
/// cuadraria con la suma de los otros seis y nadie sabria por que. En la
/// practica deberia ser cero: el servicio siempre crea los traslados en
/// 'Solicitada'.
/// </param>
/// <param name="NovedadesRecientes">
/// Ultimas incidencias reportadas, de la mas nueva a la mas vieja.
/// </param>
public sealed record MetricasTransferenciasDto(
    int? SucursalId,
    string? SucursalNombre,
    int Total,
    int Solicitadas,
    int EnTransito,
    int Completadas,
    int RecibidasParcial,
    int Rechazadas,
    int Canceladas,
    int SinEstado,
    IReadOnlyList<NovedadRecienteDto> NovedadesRecientes);
