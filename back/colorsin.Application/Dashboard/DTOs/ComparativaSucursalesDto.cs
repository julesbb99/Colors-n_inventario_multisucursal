namespace Colorsin.Application.Dashboard.DTOs;

/// <summary>Como le fue a una sede en el periodo.</summary>
/// <param name="SucursalId">Sede.</param>
/// <param name="SucursalNombre">Nombre de la sede.</param>
/// <param name="Ciudad">Ciudad, para distinguir sedes de nombre parecido.</param>
/// <param name="CantidadVentas">Ventas registradas en el periodo.</param>
/// <param name="TotalVendido">Suma de <c>ventas.total</c> en el periodo.</param>
/// <param name="TicketPromedio">
/// <paramref name="TotalVendido"/> entre <paramref name="CantidadVentas"/>. Cero
/// cuando no hubo ventas.
/// </param>
/// <param name="ParticipacionPorcentaje">
/// Que tajada de las ventas de la red se llevo esta sede, en %.
///
/// ES LA COLUMNA QUE HACE QUE ESTO SEA UNA COMPARATIVA y no tres informes
/// pegados: un total en pesos no dice si una sede va bien sin saber contra que.
/// Nulo cuando la red no vendio nada en el periodo, porque entonces no hay tarta
/// que repartir y un 0 % sugeriria que esta sede se quedo fuera de algo.
/// </param>
/// <param name="SaldoLitros">
/// Stock de la sede llevado a litros. Cada saldo se multiplica por el factor de
/// SU unidad base antes de sumarse; sumar <c>cantidad_base</c> a secas mezclaria
/// unidades.
/// </param>
/// <param name="ProductosEnAlerta">Saldos por debajo del minimo.</param>
/// <param name="TrasladosDespachados">Traslados que esta sede mando en el periodo.</param>
/// <param name="TrasladosRecibidos">Traslados que esta sede recibio en el periodo.</param>
/// <param name="VentaPorLitroEnBodega">
/// Pesos vendidos por cada litro que tiene almacenado.
///
/// Es una medida de PRODUCTIVIDAD DEL INVENTARIO, y sirve para lo que el total
/// no: una sede grande vende mas en absoluto que una pequena, siempre, y eso no
/// dice cual aprovecha mejor lo que tiene en el estante. Nulo cuando la sede no
/// tiene saldo convertible a litros.
/// </param>
public sealed record RendimientoSucursalDto(
    int SucursalId,
    string SucursalNombre,
    string? Ciudad,
    int CantidadVentas,
    decimal TotalVendido,
    decimal TicketPromedio,
    decimal? ParticipacionPorcentaje,
    decimal SaldoLitros,
    int ProductosEnAlerta,
    int TrasladosDespachados,
    int TrasladosRecibidos,
    decimal? VentaPorLitroEnBodega);

/// <summary>
/// Comparativa de rendimiento entre las sedes de la red.
///
/// NO APLICA EL AISLAMIENTO POR SEDE, y es la segunda consulta del sistema de la
/// que hay que decir esto -la otra es el listado de existencias-. Una
/// comparativa en la que cada gerente solo ve su propia fila no es una
/// comparativa: no hay contra que comparar. Por eso el administrador y los
/// gerentes ven aqui las cifras de todas las sedes.
///
/// LO QUE SI SIGUE PROTEGIDO: el operador no entra -la politica de supervision
/// lo deja fuera con 403- y lo que se expone son AGREGADOS. No hay ninguna venta
/// concreta, ningun cliente, ningun precio: quien vio esta tabla sabe que la
/// sede de Manizales vendio mas, no a quien ni a como.
/// </summary>
/// <param name="Desde">Primer dia del periodo, incluido.</param>
/// <param name="Hasta">Ultimo dia del periodo, incluido.</param>
/// <param name="Sucursales">
/// Una fila por sede de la red, de mayor a menor venta en el periodo.
///
/// Salen TODAS, tambien las que no vendieron nada: una sede que no aparece se
/// lee como que no existe, y lo que se quiere ver es justamente que se quedo en
/// cero.
/// </param>
/// <param name="TotalRedVendido">Suma de todas las sedes. Es el 100 % de la participacion.</param>
/// <param name="TotalRedVentas">Ventas de toda la red en el periodo.</param>
public sealed record ComparativaSucursalesDto(
    DateOnly Desde,
    DateOnly Hasta,
    IReadOnlyList<RendimientoSucursalDto> Sucursales,
    decimal TotalRedVendido,
    int TotalRedVentas);
