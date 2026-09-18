using Colorsin.Domain.Transferencias;

namespace Colorsin.Application.Transferencias.DTOs;

/// <summary>
/// Peticion para solicitar un traslado entre sedes.
///
/// Nace en estado 'Solicitada' y NO mueve stock: es una intencion. Las
/// existencias solo cambian al despachar, y no se reserva nada mientras tanto,
/// asi que el stock puede haberse agotado cuando llegue el momento. La
/// validacion de disponibilidad va en el despacho, no aqui.
///
/// NO LLEVA UsuarioId, a proposito: quien solicita es un parametro del metodo y
/// sale del token. Ojo con no confundir los tres responsables del ciclo -pide,
/// despacha, recibe-: cada uno se toma del token de SU propia peticion, que es
/// lo que hace que los tres sean de verdad distinguibles.
/// </summary>
/// <param name="ProductoId">Producto a trasladar.</param>
/// <param name="SucursalOrigenId">Sede que despacha, de cuyo stock saldra.</param>
/// <param name="SucursalDestinoId">
/// Sede que recibe. Debe ser distinta del origen; tambien lo exige el CHECK
/// `chk_transf_sedes_distintas`.
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
    decimal Cantidad,
    int UnidadId,
    Urgencia Urgencia = Urgencia.Media);
