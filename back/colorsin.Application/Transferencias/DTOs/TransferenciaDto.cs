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
/// <param name="Observaciones">Descripcion libre del hallazgo.</param>
/// <param name="Fecha">Momento del reporte.</param>
public sealed record NovedadTransferenciaDto(
    int Id,
    int TransferenciaId,
    int UsuarioId,
    string UsuarioNombre,
    string? Tipo,
    decimal? CantidadAfectada,
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
/// <param name="CantidadRecibida">
/// Lo que llego, en la misma unidad. Nulo hasta que la sede destino confirma.
/// Menor que lo solicitado significa que algo se perdio en transito.
/// </param>
/// <param name="UnidadId">Unidad del traslado, que puede no ser la unidad base del producto.</param>
/// <param name="UnidadSimbolo">Abreviatura de esa unidad.</param>
/// <param name="Estado">'Solicitada', 'EnTransito', 'Completada', 'RecibidaParcial', 'Rechazada' o 'Cancelada'.</param>
/// <param name="Urgencia">'Baja', 'Media' o 'Alta'.</param>
/// <param name="FechaSolicitud">Cuando se pidio.</param>
/// <param name="FechaEstimadaLlegada">Cuando se espera que llegue. Se fija al despachar.</param>
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
    decimal? CantidadRecibida,
    int UnidadId,
    string UnidadSimbolo,
    string? Estado,
    string? Urgencia,
    DateTime? FechaSolicitud,
    DateTime? FechaEstimadaLlegada,
    IReadOnlyList<DetalleTransferenciaDto> Movimientos,
    IReadOnlyList<NovedadTransferenciaDto> Novedades);
