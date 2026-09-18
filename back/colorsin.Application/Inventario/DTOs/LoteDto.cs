namespace Colorsin.Application.Inventario.DTOs;

/// <summary>Lote de un producto en una sede.</summary>
/// <param name="Id">Clave primaria.</param>
/// <param name="ProductoId">Producto.</param>
/// <param name="ProductoNombre">Nombre del producto.</param>
/// <param name="SucursalId">Sede donde esta fisicamente el lote.</param>
/// <param name="SucursalNombre">
/// Nombre de esa sede. Importa desde que el listado de lotes puede abarcar toda
/// la red: un administrador viendo doce lotes con `sucursalId` 1, 2 o 3 y sin
/// nombre tendria que resolverlos fila por fila contra el catalogo de sedes.
/// </param>
/// <param name="NumeroLote">Identificador impreso por el fabricante.</param>
/// <param name="FechaVencimiento">
/// Caducidad. Nula en productos que no caducan: eso los manda al final de la
/// cola FEFO, porque primero se despacha lo que si tiene fecha.
/// </param>
/// <param name="CantidadBase">
/// Saldo del lote, en unidad base del producto. Cero -o nulo- en un lote recien
/// creado: el lote se abre vacio y la mercancia entra por un movimiento o por
/// una recepcion de compra. Ver <see cref="CrearLoteDto"/>.
/// </param>
/// <param name="UnidadBaseSimbolo">
/// Unidad en que esta expresada <paramref name="CantidadBase"/>. Sin ella la
/// cifra no se puede leer: `cantidad_base` NO esta en litros sino en la unidad
/// base de cada producto.
/// </param>
/// <param name="FechaIngreso">Cuando entro a la sede. Desempata el orden FEFO.</param>
/// <param name="DiasParaVencer">
/// Dias que faltan para la caducidad, contra la fecha de hoy. Negativo si ya
/// vencio, nulo si el lote no caduca. Se calcula aqui para que la interfaz no
/// tenga que repetir la aritmetica de fechas ni lidiar con zonas horarias.
/// </param>
/// <param name="Vencido">
/// Atajo de <c>DiasParaVencer &lt; 0</c>, falso cuando el lote no caduca. Un lote
/// vencido CON saldo es un problema distinto de uno por vencer: ese ya no se
/// puede despachar y lo que toca es darlo de baja con un movimiento de ajuste.
/// </param>
public sealed record LoteDto(
    int Id,
    int ProductoId,
    string ProductoNombre,
    int SucursalId,
    string SucursalNombre,
    string NumeroLote,
    DateOnly? FechaVencimiento,
    decimal? CantidadBase,
    string? UnidadBaseSimbolo,
    DateTime? FechaIngreso,
    int? DiasParaVencer,
    bool Vencido);
