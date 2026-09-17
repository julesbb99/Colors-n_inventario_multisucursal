using Colorsin.Domain.Transferencias;

namespace Colorsin.Application.Transferencias.DTOs;

/// <summary>
/// Peticion para solicitar un traslado entre sedes.
///
/// Nace en estado 'Solicitada' y NO mueve stock: es una intencion. Las
/// existencias solo cambian al despachar, y no se reserva nada mientras tanto,
/// asi que el stock puede haberse agotado cuando llegue el momento. La
/// validacion de disponibilidad va en el despacho, no aqui.
/// </summary>
/// <param name="ProductoId">Producto a trasladar.</param>
/// <param name="SucursalOrigenId">Sede que despacha, de cuyo stock saldra.</param>
/// <param name="SucursalDestinoId">
/// Sede que recibe. Debe ser distinta del origen; tambien lo exige el CHECK
/// `chk_transf_sedes_distintas`.
/// </param>
/// <param name="UsuarioId">
/// Quien solicita. Obligatorio: se guarda en `transferencias.usuario_id` y
/// ademas queda en el evento de auditoria. Debe existir en `usuarios`; la FK
/// `fk_transf_usuario` lo impone.
///
/// Es el que PIDE el producto. Quien despacha y quien recibe se registran
/// aparte, en los movimientos de inventario de cada paso.
/// </param>
/// <param name="Cantidad">
/// Cantidad a trasladar, en <paramref name="UnidadId"/>. Debe ser mayor que
/// cero; tambien lo exige el CHECK `chk_transf_cantidades`.
/// </param>
/// <param name="UnidadId">
/// Unidad del traslado. Puede diferir de la unidad base del producto: se pide
/// en galones y el stock se lleva en litros. El servicio convierte al despachar.
/// </param>
/// <param name="Urgencia">Prioridad. 'Media' si no se indica.</param>
public sealed record CrearTransferenciaDto(
    int ProductoId,
    int SucursalOrigenId,
    int SucursalDestinoId,
    int UsuarioId,
    decimal Cantidad,
    int UnidadId,
    Urgencia Urgencia = Urgencia.Media);
