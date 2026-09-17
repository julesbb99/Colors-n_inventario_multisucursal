using Colorsin.Domain.Inventario;

namespace Colorsin.Application.Inventario.DTOs;

/// <summary>
/// Peticion para registrar una entrada o salida de stock.
///
/// Nota sobre <paramref name="Cantidad"/> y <paramref name="UnidadId"/>: van
/// juntas y en la unidad que el operario tenga a mano. El servicio convierte a
/// la unidad base del producto; no hay que convertir antes de llamar. Pedir la
/// cantidad ya convertida seria trasladar al operario la aritmetica que causa
/// los errores que este modulo existe para evitar.
/// </summary>
/// <param name="SucursalId">Sede donde ocurre el movimiento.</param>
/// <param name="ProductoId">Producto afectado.</param>
/// <param name="UsuarioId">Responsable. Queda en el movimiento y en la auditoria.</param>
/// <param name="TipoMovimiento">Si entra o sale stock.</param>
/// <param name="Motivo">Que lo origino.</param>
/// <param name="Cantidad">
/// Cantidad digitada, en <paramref name="UnidadId"/>. Debe ser mayor que cero:
/// el signo lo aporta <paramref name="TipoMovimiento"/>, no el numero. La base
/// tambien lo exige, con un CHECK.
/// </param>
/// <param name="UnidadId">Unidad en que viene <paramref name="Cantidad"/>.</param>
/// <param name="LoteId">
/// Lote al que se imputa. Opcional.
///
/// Si se indica en un Retiro, ademas del saldo de la sede se descuenta el saldo
/// de ESE lote, y se valida que alcance y que el lote pertenezca a la misma
/// sede y producto. Si se omite, el retiro afecta solo el saldo consolidado y
/// se pierde la trazabilidad por lote; para eso esta
/// <c>ObtenerLotesPorVencimientoAsync</c>, que dice cual tomar segun FEFO.
/// </param>
/// <param name="Observaciones">Nota libre del operario. Maximo 255 caracteres.</param>
public sealed record RegistrarMovimientoDto(
    int SucursalId,
    int ProductoId,
    int UsuarioId,
    TipoMovimiento TipoMovimiento,
    MotivoMovimiento Motivo,
    decimal Cantidad,
    int UnidadId,
    int? LoteId = null,
    string? Observaciones = null);
