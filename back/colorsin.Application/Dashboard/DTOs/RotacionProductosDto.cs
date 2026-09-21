namespace Colorsin.Application.Dashboard.DTOs;

/// <summary>
/// Como de rapido se mueve un producto.
///
/// Se clasifica por DIAS DE COBERTURA y no por cantidad vendida, y esa es la
/// decision que hace util la tabla: "se vendieron 400 litros" no dice si eso es
/// mucho o poco sin saber cuanto hay en bodega. Los dias de cobertura si, y
/// ademas son comparables entre productos de unidades distintas, porque son
/// dias y no volumen.
/// </summary>
public enum ClaseRotacion
{
    /// <summary>
    /// Se vende mas rapido de lo que dura el stock. Hay que reponer pronto.
    /// </summary>
    Alta,

    /// <summary>Ritmo normal.</summary>
    Media,

    /// <summary>
    /// Hay stock para mucho tiempo al ritmo actual. Es plata quieta en el
    /// estante, y en pintura ademas es producto que puede caducar antes de
    /// venderse.
    /// </summary>
    Baja,

    /// <summary>
    /// No se vendio NADA en el periodo. Es distinto de rotacion baja y merece su
    /// propia clase: la baja se calcula, esta no se puede calcular -no hay
    /// ritmo- y es la que hay que mirar primero.
    /// </summary>
    SinMovimiento
}

/// <summary>Un producto en la tabla de rotacion.</summary>
/// <param name="ProductoId">Producto.</param>
/// <param name="ProductoNombre">Nombre del producto.</param>
/// <param name="Categoria">Categoria del catalogo. Puede venir nula.</param>
/// <param name="UnidadBaseSimbolo">
/// Unidad de las dos cantidades ('L', 'gal'). Nula si el producto no tiene
/// unidad base definida.
/// </param>
/// <param name="CantidadVendidaBase">
/// Lo vendido en el periodo, EN LA UNIDAD BASE del producto. Se convierte antes
/// de sumar: <c>venta_detalle.cantidad</c> esta en la unidad que uso el
/// vendedor, que cambia de linea a linea.
/// </param>
/// <param name="SaldoActualBase">
/// Lo que hay AHORA en bodega, en la misma unidad. No es el saldo promedio del
/// periodo -que seria mas ortodoxo- porque el sistema no guarda fotos del stock:
/// reconstruirlo exigiria recorrer el libro mayor hacia atras en cada consulta.
/// Con un saldo puntual, un producto que se acaba de reponer parece de rotacion
/// mas lenta de lo que es.
/// </param>
/// <param name="TotalFacturado">Dinero que dejo ese producto en el periodo.</param>
/// <param name="VecesQueRoto">
/// <paramref name="CantidadVendidaBase"/> entre
/// <paramref name="SaldoActualBase"/>: cuantas veces se vendio el equivalente
/// del stock actual durante el periodo.
///
/// Nulo cuando no hay saldo: dividir entre cero no da "rotacion infinita", da
/// que no se puede medir. Un producto agotado que se vendio mucho sale con
/// cobertura cero, que es la senal util.
/// </param>
/// <param name="DiasCobertura">
/// Cuantos dias aguanta el stock actual al ritmo del periodo.
///
/// Nulo si no hubo ventas: sin ritmo no hay cobertura que calcular, y poner
/// "infinito" o un numero enorme lo mezclaria con los de rotacion baja, que si
/// se venden.
/// </param>
/// <param name="Clase">'Alta', 'Media', 'Baja' o 'SinMovimiento'.</param>
public sealed record RotacionProductoDto(
    int ProductoId,
    string ProductoNombre,
    string? Categoria,
    string? UnidadBaseSimbolo,
    decimal CantidadVendidaBase,
    decimal SaldoActualBase,
    decimal TotalFacturado,
    decimal? VecesQueRoto,
    decimal? DiasCobertura,
    string Clase);

/// <summary>
/// La rotacion de todo el catalogo en un periodo, ya clasificada.
/// </summary>
/// <param name="Desde">Primer dia del periodo, incluido.</param>
/// <param name="Hasta">Ultimo dia del periodo, incluido.</param>
/// <param name="Dias">Dias del periodo. Es el divisor de la cobertura.</param>
/// <param name="SucursalId">Sede consultada, o <c>null</c> si es toda la red.</param>
/// <param name="SucursalNombre">Nombre de esa sede.</param>
/// <param name="UmbralDiasAlta">
/// Por debajo de estos dias de cobertura, la rotacion es Alta.
/// </param>
/// <param name="UmbralDiasBaja">
/// Por encima de estos dias de cobertura, la rotacion es Baja. Entre los dos
/// umbrales, Media.
/// </param>
/// <param name="Productos">
/// Todos los productos con saldo o con ventas en el periodo, de mayor a menor
/// demanda: primero los de menos cobertura, y los sin movimiento al final.
///
/// VIENEN TODOS EN UNA LISTA y no repartidos en dos, aunque la pantalla muestre
/// "alta" y "baja" por separado. Partirlos aqui obligaria a fijar en el servidor
/// cuantos entran en cada lado, y esa es una decision de la pantalla: el mismo
/// dato alimenta la tabla completa y los dos recuadros.
/// </param>
/// <param name="ConAlta">Cuantos productos quedaron en rotacion Alta.</param>
/// <param name="ConMedia">Cuantos en Media.</param>
/// <param name="ConBaja">Cuantos en Baja.</param>
/// <param name="SinMovimiento">Cuantos no se vendieron en el periodo.</param>
public sealed record RotacionProductosDto(
    DateOnly Desde,
    DateOnly Hasta,
    int Dias,
    int? SucursalId,
    string? SucursalNombre,
    int UmbralDiasAlta,
    int UmbralDiasBaja,
    IReadOnlyList<RotacionProductoDto> Productos,
    int ConAlta,
    int ConMedia,
    int ConBaja,
    int SinMovimiento);
