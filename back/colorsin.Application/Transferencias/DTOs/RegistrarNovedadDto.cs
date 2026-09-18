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
///
/// NO LLEVA UsuarioId, a proposito: quien reporta es un parametro del metodo y
/// sale del token. Una novedad puede acabar en un reclamo a la transportadora,
/// y entonces quien la firmo importa de verdad; que lo pusiera el cliente en el
/// cuerpo del JSON la dejaria sin valor como evidencia.
/// </summary>
/// <param name="TransferenciaId">Traslado sobre el que se reporta.</param>
/// <param name="Tipo">'Faltante', 'Averia', 'Sobrante' o 'Retraso'.</param>
/// <param name="CantidadAfectada">
/// Cuanto producto involucra, en la unidad del traslado. Opcional e informativa:
/// no se descuenta de ningun lado. El CHECK de la base solo exige que no sea
/// negativa.
/// </param>
/// <param name="Observaciones">Descripcion del hallazgo.</param>
public sealed record RegistrarNovedadDto(
    int TransferenciaId,
    TipoNovedad Tipo,
    decimal? CantidadAfectada = null,
    string? Observaciones = null);
