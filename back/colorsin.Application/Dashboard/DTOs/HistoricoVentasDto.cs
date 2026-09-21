namespace Colorsin.Application.Dashboard.DTOs;

/// <summary>Lo vendido en un mes calendario.</summary>
/// <param name="Anio">Ano.</param>
/// <param name="Mes">Mes, de 1 a 12.</param>
/// <param name="CantidadVentas">Cuantas ventas se registraron.</param>
/// <param name="Total">Suma de <c>ventas.total</c> de ese mes.</param>
public sealed record VentasPorMesDto(
    int Anio,
    int Mes,
    int CantidadVentas,
    decimal Total);

/// <summary>
/// Las ventas MES A MES y el acumulado de toda la historia.
///
/// POR QUE HACIA FALTA. El resumen de la pantalla de inicio solo decia "ventas
/// del dia" y "ventas del mes", y el mes en curso siempre arranca en cero: el
/// dia 1 el tablero parecia el de una empresa que nunca ha vendido nada. Con
/// esto se ve de donde viene el mes actual y cuanto lleva vendido el negocio.
///
/// EL ACUMULADO NO SE SUMA DE <paramref name="Meses"/>. Esa lista puede venir
/// recortada a los ultimos N meses, asi que sumarla daria el acumulado de la
/// ventana y no el de la historia. Son dos consultas distintas a proposito.
/// </summary>
/// <param name="SucursalId">Sede consultada, o <c>null</c> si es toda la red.</param>
/// <param name="SucursalNombre">Nombre de esa sede. Nulo cuando el alcance es la red.</param>
/// <param name="Meses">
/// Un elemento por mes CON ventas, del mas antiguo al mas reciente. Los meses
/// sin ninguna venta no aparecen; rellenarlos con ceros es decision de quien
/// dibuje la grafica.
/// </param>
/// <param name="MesesSolicitados">
/// Cuantos meses hacia atras se pidieron. Viaja de vuelta porque sin el no se
/// puede interpretar un hueco: "no hay nada en marzo" y "marzo quedo fuera de la
/// ventana" se ven igual en la lista.
/// </param>
/// <param name="CantidadHistorica">Ventas registradas en TODA la historia.</param>
/// <param name="TotalHistorico">
/// Suma de <c>ventas.total</c> de toda la historia, sin acotar por fecha. Es "la
/// suma de todas las ventas realizadas".
/// </param>
/// <param name="TicketPromedioHistorico">
/// <paramref name="TotalHistorico"/> entre <paramref name="CantidadHistorica"/>.
/// Cero cuando no hay ventas, para que la interfaz no tenga que protegerse de la
/// division por cero.
/// </param>
/// <param name="PrimeraVenta">Fecha de la venta mas antigua. Nula si no hay ninguna.</param>
/// <param name="UltimaVenta">Fecha de la venta mas reciente. Nula si no hay ninguna.</param>
/// <param name="PromedioMensual">
/// Media mensual sobre los meses QUE TUVIERON VENTAS, no sobre los solicitados.
///
/// La distincion importa: dividir entre 12 meses cuando el negocio lleva dos
/// abiertos daria una media falsamente baja, y esa cifra se usa para comparar
/// contra el mes en curso.
/// </param>
public sealed record HistoricoVentasDto(
    int? SucursalId,
    string? SucursalNombre,
    int MesesSolicitados,
    IReadOnlyList<VentasPorMesDto> Meses,
    int CantidadHistorica,
    decimal TotalHistorico,
    decimal TicketPromedioHistorico,
    DateTime? PrimeraVenta,
    DateTime? UltimaVenta,
    decimal PromedioMensual);
