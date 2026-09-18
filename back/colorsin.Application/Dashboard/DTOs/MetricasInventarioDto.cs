using Colorsin.Application.Inventario.DTOs;

namespace Colorsin.Application.Dashboard.DTOs;

/// <summary>Como esta parada una sede: cuanto tiene, cuanto vale y que le falta.</summary>
/// <param name="SucursalId">Sede.</param>
/// <param name="SucursalNombre">Nombre de la sede.</param>
/// <param name="Ciudad">Ciudad donde esta.</param>
/// <param name="ProductosConSaldo">
/// Productos con saldo mayor que cero. No es el tamano del catalogo: una fila
/// de <c>inventario_sucursal</c> en cero significa que la sede maneja el
/// producto pero esta agotado.
/// </param>
/// <param name="ProductosEnAlerta">
/// Cuantos cumplen <c>cantidad_base &lt;= stock_minimo</c>, el mismo criterio
/// que usa el modulo de inventario.
/// </param>
/// <param name="SaldoLitros">
/// Stock de la sede llevado a litros. Cada saldo se multiplica por el factor de
/// su unidad base antes de sumarse; ver
/// <see cref="ResumenGeneralDto.SaldoInventarioLitros"/>.
/// </param>
/// <param name="ProductosSinConversionALitros">
/// Saldos excluidos de <paramref name="SaldoLitros"/> por no tener factor a
/// litros. Cero con el catalogo actual.
/// </param>
/// <param name="ValorInventario">
/// <c>cantidad_base x costo_promedio</c> sumado sobre la sede, en pesos.
///
/// Esta suma SI es segura sin convertir: el costo promedio ya esta expresado
/// por unidad base de cada producto, asi que cada termino queda en pesos antes
/// de sumarse. Es la unica cifra de inventario que no necesita el factor.
/// </param>
public sealed record StockPorSucursalDto(
    int SucursalId,
    string SucursalNombre,
    string Ciudad,
    int ProductosConSaldo,
    int ProductosEnAlerta,
    decimal SaldoLitros,
    int ProductosSinConversionALitros,
    decimal ValorInventario);

/// <summary>
/// Lote que esta por caducar, o que ya caduco.
///
/// Existe aparte de <see cref="LoteDto"/> porque este panel es de toda la red:
/// <see cref="LoteDto"/> trae el <c>SucursalId</c> pero no el nombre de la
/// sede, y una lista de vencimientos sin decir en que bodega esta cada lote
/// obliga a la interfaz a resolver el nombre fila por fila.
/// </summary>
/// <param name="LoteId">Lote.</param>
/// <param name="ProductoId">Producto.</param>
/// <param name="ProductoNombre">Nombre del producto.</param>
/// <param name="SucursalId">Sede donde esta fisicamente.</param>
/// <param name="SucursalNombre">Nombre de esa sede.</param>
/// <param name="NumeroLote">Identificador impreso por el fabricante.</param>
/// <param name="FechaVencimiento">
/// Caducidad. Aqui NO es nula: los lotes que no caducan quedan fuera de esta
/// lista, porque un panel de vencimientos proximos no tiene que decir nada de
/// ellos.
/// </param>
/// <param name="DiasParaVencer">
/// Dias que faltan, contra la fecha de corte. Negativo si ya vencio.
/// </param>
/// <param name="CantidadBase">Saldo del lote, en unidad base del producto.</param>
/// <param name="UnidadBaseSimbolo">Unidad de esa cantidad.</param>
/// <param name="Vencido">
/// Atajo de <c>DiasParaVencer &lt; 0</c>. Un lote vencido con saldo es un
/// problema distinto de uno por vencer: ya no se puede despachar.
/// </param>
public sealed record LotePorVencerDto(
    int LoteId,
    int ProductoId,
    string ProductoNombre,
    int SucursalId,
    string SucursalNombre,
    string NumeroLote,
    DateOnly FechaVencimiento,
    int DiasParaVencer,
    decimal CantidadBase,
    string? UnidadBaseSimbolo,
    bool Vencido);

/// <summary>
/// Inventario visto desde arriba: reparto por sede, vencimientos que se vienen
/// y ultima actividad del libro mayor.
/// </summary>
/// <param name="SucursalId">Sede consultada, o <c>null</c> si es toda la red.</param>
/// <param name="SucursalNombre">Nombre de esa sede. Nulo cuando el alcance es la red.</param>
/// <param name="DiasHorizonteVencimiento">
/// Ventana usada para <paramref name="LotesProximosAVencer"/>. Viaja de vuelta
/// porque cambia el significado de la lista: "12 lotes por vencer" no dice nada
/// sin saber si es a 30 o a 180 dias.
/// </param>
/// <param name="SaldoTotalLitros">Suma de <see cref="StockPorSucursalDto.SaldoLitros"/>.</param>
/// <param name="ProductosSinConversionALitros">Saldos excluidos de esa suma.</param>
/// <param name="ValorInventario">Suma de <see cref="StockPorSucursalDto.ValorInventario"/>.</param>
/// <param name="StockPorSucursal">
/// Una fila por sede, de mayor a menor saldo. Con el filtro de sede puesto trae
/// una sola fila.
/// </param>
/// <param name="LotesProximosAVencer">
/// Lotes CON SALDO que vencen dentro del horizonte, en orden FEFO: primero el
/// que vence antes, incluidos los ya vencidos, que van de primeros por tener
/// dias negativos. Los lotes agotados y los que no caducan no aparecen.
/// </param>
/// <param name="MovimientosRecientes">
/// Ultimas filas del libro mayor, de la mas nueva a la mas vieja.
///
/// Reusa el DTO del modulo de inventario en vez de definir uno recortado: es la
/// misma fila, y dos formas distintas de representarla terminan divergiendo en
/// cuanto una de las dos cambie.
/// </param>
public sealed record MetricasInventarioDto(
    int? SucursalId,
    string? SucursalNombre,
    int DiasHorizonteVencimiento,
    decimal SaldoTotalLitros,
    int ProductosSinConversionALitros,
    decimal ValorInventario,
    IReadOnlyList<StockPorSucursalDto> StockPorSucursal,
    IReadOnlyList<LotePorVencerDto> LotesProximosAVencer,
    IReadOnlyList<MovimientoInventarioDto> MovimientosRecientes);
