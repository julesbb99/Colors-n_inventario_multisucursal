namespace Colorsin.Application.Dashboard.DTOs;

/// <summary>Lo vendido en un dia del rango. Alimenta la grafica de linea.</summary>
/// <param name="Fecha">Dia.</param>
/// <param name="CantidadVentas">Cuantas ventas se registraron.</param>
/// <param name="Total">Suma de <c>ventas.total</c> de ese dia.</param>
public sealed record VentasPorDiaDto(
    DateOnly Fecha,
    int CantidadVentas,
    decimal Total);

/// <summary>Un producto dentro del ranking de mas vendidos.</summary>
/// <param name="ProductoId">Producto.</param>
/// <param name="ProductoNombre">Nombre del producto.</param>
/// <param name="Categoria">Categoria del catalogo. Puede venir nula.</param>
/// <param name="Lineas">En cuantas lineas de venta aparecio.</param>
/// <param name="CantidadBase">
/// Cuanto se vendio, en la UNIDAD BASE del producto.
///
/// Es obligatorio convertir antes de sumar: <c>detalle_venta.cantidad</c> esta
/// en la unidad que uso el vendedor, que cambia de linea a linea. Sumar esa
/// columna directamente daria "8" para una venta de 3 galones y otra de 5
/// litros, que no es ninguna cantidad real.
/// </param>
/// <param name="UnidadBaseSimbolo">
/// Unidad en que esta <paramref name="CantidadBase"/> ('L', 'gal'). Nula si el
/// producto no tiene unidad base definida.
/// </param>
/// <param name="TotalFacturado">
/// Dinero de ese producto en el rango: suma de
/// <c>cantidad x precio_unitario x (1 - descuento/100)</c> linea por linea.
///
/// No sale de <c>ventas.total</c> porque ese total es del encabezado y no se
/// puede repartir entre productos. La consecuencia es que la suma de esta
/// columna puede no cuadrar al peso con
/// <see cref="MetricasVentasDto.TotalVendido"/> si alguna venta vieja quedo con
/// el encabezado desfasado respecto de su detalle.
/// </param>
public sealed record ProductoMasVendidoDto(
    int ProductoId,
    string ProductoNombre,
    string? Categoria,
    int Lineas,
    decimal CantidadBase,
    string? UnidadBaseSimbolo,
    decimal TotalFacturado);

/// <summary>Un cliente dentro del ranking de los que mas compran.</summary>
/// <param name="ClienteId">Cliente.</param>
/// <param name="RazonSocial">Nombre o razon social.</param>
/// <param name="Documento">Cedula o NIT.</param>
/// <param name="CantidadVentas">Cuantas ventas se le hicieron en el rango.</param>
/// <param name="TotalComprado">Suma de <c>ventas.total</c> de ese cliente.</param>
/// <param name="UltimaCompra">
/// Fecha de su venta mas reciente DENTRO del rango, no en toda su historia.
/// </param>
public sealed record ClienteTopDto(
    int ClienteId,
    string RazonSocial,
    string Documento,
    int CantidadVentas,
    decimal TotalComprado,
    DateTime? UltimaCompra);

/// <summary>
/// Ventas de un periodo: el consolidado, la serie diaria y los dos rankings.
///
/// Todo el bloque responde al MISMO rango y al mismo filtro de sede, que viajan
/// de vuelta en el resultado para que la interfaz pueda rotularlo sin confiar
/// en lo que ella misma pidio.
/// </summary>
/// <param name="Desde">Primer dia del rango, incluido.</param>
/// <param name="Hasta">
/// Ultimo dia del rango, TAMBIEN incluido.
///
/// Importa al implementarlo: <c>ventas.fecha</c> es DATETIME, asi que filtrar
/// por <c>fecha &lt;= hasta</c> dejaria por fuera todo lo vendido ese dia
/// despues de medianoche. El filtro correcto es <c>fecha &lt; hasta + 1 dia</c>.
/// </param>
/// <param name="SucursalId">Sede consultada, o <c>null</c> si es toda la red.</param>
/// <param name="SucursalNombre">Nombre de esa sede. Nulo cuando el alcance es la red.</param>
/// <param name="CantidadVentas">Ventas registradas en el rango.</param>
/// <param name="TotalVendido">Suma de <c>ventas.total</c> en el rango.</param>
/// <param name="TicketPromedio">
/// <c>TotalVendido / CantidadVentas</c>. Cero cuando no hubo ventas: se calcula
/// aqui para que la interfaz no tenga que protegerse de la division por cero.
/// </param>
/// <param name="SerieDiaria">
/// Un elemento por dia CON ventas, en orden cronologico. Los dias sin ventas no
/// aparecen; rellenarlos con ceros es decision de quien dibuje la grafica.
/// </param>
/// <param name="ProductosMasVendidos">
/// Ranking por <see cref="ProductoMasVendidoDto.TotalFacturado"/>, de mayor a
/// menor. Por facturacion y no por cantidad, porque comparar cantidades entre
/// productos de distinta unidad base no significa nada.
/// </param>
/// <param name="TopClientes">
/// Ranking por <see cref="ClienteTopDto.TotalComprado"/>, de mayor a menor.
/// </param>
public sealed record MetricasVentasDto(
    DateOnly Desde,
    DateOnly Hasta,
    int? SucursalId,
    string? SucursalNombre,
    int CantidadVentas,
    decimal TotalVendido,
    decimal TicketPromedio,
    IReadOnlyList<VentasPorDiaDto> SerieDiaria,
    IReadOnlyList<ProductoMasVendidoDto> ProductosMasVendidos,
    IReadOnlyList<ClienteTopDto> TopClientes);
