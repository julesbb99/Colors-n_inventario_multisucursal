namespace Colorsin.Application.Inventario.DTOs;

/// <summary>Lote de un producto en una sede.</summary>
/// <param name="Id">Clave primaria.</param>
/// <param name="ProductoId">Producto.</param>
/// <param name="ProductoNombre">Nombre del producto.</param>
/// <param name="SucursalId">Sede donde esta fisicamente el lote.</param>
/// <param name="NumeroLote">Identificador impreso por el fabricante.</param>
/// <param name="FechaVencimiento">
/// Caducidad. Nula en productos que no caducan: eso los manda al final de la
/// cola FEFO, porque primero se despacha lo que si tiene fecha.
/// </param>
/// <param name="CantidadBase">Saldo del lote, en unidad base del producto.</param>
/// <param name="FechaIngreso">Cuando entro a la sede. Desempata el orden FEFO.</param>
/// <param name="DiasParaVencer">
/// Dias que faltan para la caducidad, contra la fecha de hoy. Negativo si ya
/// vencio, nulo si el lote no caduca. Se calcula aqui para que la interfaz no
/// tenga que repetir la aritmetica de fechas ni lidiar con zonas horarias.
/// </param>
public sealed record LoteDto(
    int Id,
    int ProductoId,
    string ProductoNombre,
    int SucursalId,
    string NumeroLote,
    DateOnly? FechaVencimiento,
    decimal? CantidadBase,
    DateTime? FechaIngreso,
    int? DiasParaVencer);
