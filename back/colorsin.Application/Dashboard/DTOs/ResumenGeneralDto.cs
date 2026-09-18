namespace Colorsin.Application.Dashboard.DTOs;

/// <summary>
/// Los numeros grandes de la pantalla de inicio: lo vendido, lo que hay en
/// bodega, lo que va en camino y lo que falta reponer.
///
/// Es una FOTO, no un documento: nada de lo que sale aqui se guarda. Cada
/// lectura se recalcula contra las tablas operativas, asi que dos llamadas
/// seguidas pueden dar cifras distintas si alguien vendio en el intermedio.
/// Por eso viaja <paramref name="GeneradoEn"/>: la interfaz puede mostrar a que
/// momento corresponden los datos.
/// </summary>
/// <param name="SucursalId">
/// Sede a la que corresponden las cifras, o <c>null</c> si son de toda la red.
/// </param>
/// <param name="SucursalNombre">Nombre de esa sede. Nulo cuando el alcance es la red.</param>
/// <param name="FechaCorte">
/// Dia contra el que se calcularon "del dia" y "del mes". Es un parametro y no
/// un <c>DateTime.Today</c> escondido en el servicio para que el resultado sea
/// reproducible: el mismo corte da siempre la misma cifra.
/// </param>
/// <param name="VentasDelDia">
/// Suma de <c>ventas.total</c> de <paramref name="FechaCorte"/>.
///
/// Sale del total ALMACENADO en el encabezado, no de recalcular el detalle. Las
/// dos cifras deberian coincidir, pero si una venta antigua quedo con el total
/// desfasado, el dashboard tiene que mostrar lo mismo que muestra la venta.
/// </param>
/// <param name="CantidadVentasDelDia">Cuantas ventas se registraron ese dia.</param>
/// <param name="VentasDelMes">
/// Suma de <c>ventas.total</c> del mes calendario de <paramref name="FechaCorte"/>,
/// desde el dia 1 hasta el corte inclusive. No son los ultimos 30 dias.
/// </param>
/// <param name="CantidadVentasDelMes">Cuantas ventas lleva el mes.</param>
/// <param name="SaldoInventarioLitros">
/// Todo el stock llevado a litros.
///
/// OJO: <c>inventario_sucursal.cantidad_base</c> NO esta en litros, sino en la
/// unidad base de CADA producto. Sumar esa columna a secas mezclaria litros con
/// lo que no sea litro. Aqui cada saldo se multiplica por el
/// <c>factor_conversion_litros</c> de su unidad base antes de sumarse.
/// </param>
/// <param name="ProductosSinConversionALitros">
/// Cuantos saldos quedaron FUERA de <paramref name="SaldoInventarioLitros"/>
/// por no tener como convertirse: producto sin unidad base, o unidad base sin
/// factor a litros (un kilogramo no se convierte a litros sin conocer la
/// densidad).
///
/// Va explicito, y no descartado en silencio, porque un total que omite filas
/// sin decirlo se lee como si fuera el inventario completo. Con el catalogo
/// sembrado hoy es cero: las tres unidades son de volumen.
/// </param>
/// <param name="TransferenciasEnTransito">
/// Traslados despachados que todavia no se reciben (estado 'EnTransito'). Es
/// mercancia que ya salio del origen y aun no suma en el destino.
/// </param>
/// <param name="AlertasStockBajo">
/// Cuantos saldos cumplen <c>cantidad_base &lt;= stock_minimo</c>, el mismo
/// criterio de <c>IInventarioService.ObtenerAlertasStockBajoAsync</c>. Aqui va
/// solo el conteo; el detalle se pide a ese metodo, que ya existe.
/// </param>
/// <param name="AlertasVencimiento">
/// Cuantos lotes CON SALDO caducan dentro de
/// <paramref name="DiasUmbralVencimiento"/> dias, INCLUIDOS los que ya vencieron.
///
/// Los vencidos entran a proposito y son el caso mas urgente de los dos: un lote
/// caducado con existencias no se puede despachar y lo que toca es darlo de baja.
/// Dejarlos fuera del contador porque su fecha quedo "antes de la ventana" seria
/// esconder justamente lo que ya se salio de control.
///
/// Es la contraparte de <paramref name="AlertasStockBajo"/>: uno avisa de lo que
/// falta, este de lo que sobra y se va a perder. El detalle se pide a
/// <c>GET /api/dashboard/inventario</c> o a
/// <c>GET /api/inventario/lotes/proximos-a-vencer</c>.
/// </param>
/// <param name="DiasUmbralVencimiento">
/// Ventana usada para <paramref name="AlertasVencimiento"/>, en dias. Viaja de
/// vuelta porque sin ella el numero no se puede interpretar: "7 lotes por vencer"
/// no dice lo mismo a 30 dias que a 180. Sale de
/// <c>AlertasInventario:DiasUmbralVencimiento</c> salvo que se pida otro.
/// </param>
/// <param name="GeneradoEn">Momento en que se leyeron los datos.</param>
public sealed record ResumenGeneralDto(
    int? SucursalId,
    string? SucursalNombre,
    DateOnly FechaCorte,
    decimal VentasDelDia,
    int CantidadVentasDelDia,
    decimal VentasDelMes,
    int CantidadVentasDelMes,
    decimal SaldoInventarioLitros,
    int ProductosSinConversionALitros,
    int TransferenciasEnTransito,
    int AlertasStockBajo,
    int AlertasVencimiento,
    int DiasUmbralVencimiento,
    DateTime GeneradoEn);
