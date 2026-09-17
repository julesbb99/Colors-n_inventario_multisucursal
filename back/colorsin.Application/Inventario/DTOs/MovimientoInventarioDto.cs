namespace Colorsin.Application.Inventario.DTOs;

/// <summary>
/// Una fila del libro mayor de inventario.
///
/// Lleva la cantidad dos veces a proposito: <paramref name="Cantidad"/> con la
/// unidad que uso el operario y <paramref name="CantidadBase"/> ya convertida.
/// Guardar solo la convertida perderia lo que la persona realmente digito, y
/// con ello la posibilidad de auditar una conversion mal hecha.
/// </summary>
/// <param name="Id">Clave primaria.</param>
/// <param name="SucursalId">Sede.</param>
/// <param name="SucursalNombre">Nombre de la sede.</param>
/// <param name="ProductoId">Producto.</param>
/// <param name="ProductoNombre">Nombre del producto.</param>
/// <param name="UsuarioId">Quien lo registro.</param>
/// <param name="UsuarioNombre">Nombre de esa persona.</param>
/// <param name="Tipo">'Ingreso' o 'Retiro'.</param>
/// <param name="Motivo">'Compra', 'Venta', 'Ajuste', 'Transferencia', 'Merma' o 'Devolucion'.</param>
/// <param name="Cantidad">Cantidad digitada, siempre positiva. El signo lo da el tipo.</param>
/// <param name="UnidadId">Unidad en que se digito.</param>
/// <param name="UnidadSimbolo">Abreviatura de esa unidad.</param>
/// <param name="CantidadBase">La misma cantidad en unidad base del producto.</param>
/// <param name="LoteId">Lote imputado, si lo hubo.</param>
/// <param name="NumeroLote">Numero de ese lote.</param>
/// <param name="Observaciones">Nota del operario.</param>
/// <param name="Fecha">Momento del registro.</param>
public sealed record MovimientoInventarioDto(
    int Id,
    int SucursalId,
    string SucursalNombre,
    int ProductoId,
    string ProductoNombre,
    int UsuarioId,
    string UsuarioNombre,
    string? Tipo,
    string? Motivo,
    decimal? Cantidad,
    int UnidadId,
    string UnidadSimbolo,
    decimal? CantidadBase,
    int? LoteId,
    string? NumeroLote,
    string? Observaciones,
    DateTime? Fecha);
