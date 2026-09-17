using Colorsin.Domain.Transferencias;

namespace Colorsin.Application.Transferencias.DTOs;

/// <summary>
/// Peticion para dejar constancia de un hallazgo sobre un traslado: latas
/// abolladas, un derrame, producto que no llego, un retraso.
///
/// NO mueve stock ni cambia el estado del traslado, a proposito. Una novedad
/// documenta lo que se vio; lo que ajusta el saldo es la cantidad que la sede
/// destino declare al recibir. Separarlos permite reportar una averia sin
/// bloquear el cierre cuando el total cuadra igual, que es justamente el caso
/// de las latas abolladas cuyo contenido si llego.
///
/// Se puede registrar en cualquier estado, incluso despues de cerrado: los
/// danos se descubren al abrir las cajas, no en el andén.
/// </summary>
/// <param name="TransferenciaId">Traslado sobre el que se reporta.</param>
/// <param name="UsuarioId">
/// Quien reporta. Queda en `novedades_transferencia.usuario_id`, que tiene FK
/// obligatoria: una novedad sin responsable no sirve para reclamar.
/// </param>
/// <param name="Tipo">'Faltante', 'Averia', 'Sobrante' o 'Retraso'.</param>
/// <param name="CantidadAfectada">
/// Cuanto producto involucra, en la unidad del traslado. Opcional e informativa:
/// no se descuenta de ningun lado. El CHECK de la base solo exige que no sea
/// negativa.
/// </param>
/// <param name="Observaciones">Descripcion del hallazgo.</param>
public sealed record RegistrarNovedadDto(
    int TransferenciaId,
    int UsuarioId,
    TipoNovedad Tipo,
    decimal? CantidadAfectada = null,
    string? Observaciones = null);
