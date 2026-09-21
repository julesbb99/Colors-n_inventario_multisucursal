using Colorsin.Domain.Transferencias;

namespace Colorsin.Application.Transferencias.DTOs;

/// <summary>
/// Peticion para dejar constancia de un hallazgo sobre un traslado: latas
/// abolladas, un derrame, producto que no llego, un retraso.
///
/// NO mueve stock, a proposito. Una novedad documenta lo que se vio; lo que
/// ajusta el saldo es la cantidad que la sede destino declare al recibir.
/// Separarlos permite reportar una averia sin bloquear el cierre cuando el
/// total cuadra igual, que es justamente el caso de las latas abolladas cuyo
/// contenido si llego.
///
/// SI PUEDE CAMBIAR EL ESTADO DEL TRASLADO, y esa es la novedad respecto a la
/// version anterior. Depende de <paramref name="Tratamiento"/>: ver alli.
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
/// <param name="Tratamiento">
/// Que se va a hacer con lo reportado, y con ello si el traslado puede cerrarse.
///
///   Ninguno      solo se deja constancia
///   Reenvio      el origen vuelve a mandar lo que falto
///   Reclamacion  se le cobra a la transportadora
///   Asumido      se da por perdido: esto es la merma
///
/// Reenvio y Reclamacion dejan la novedad ABIERTA y el traslado sigue
/// pendiente: hay algo que esperar. Los otros dos la cierran en el acto, y un
/// traslado en 'RecibidaParcial' pasa entonces a 'Cerrada'.
/// </param>
/// <param name="Observaciones">Descripcion del hallazgo.</param>
public sealed record RegistrarNovedadDto(
    int TransferenciaId,
    TipoNovedad Tipo,
    decimal? CantidadAfectada = null,
    TratamientoNovedad Tratamiento = TratamientoNovedad.Ninguno,
    string? Observaciones = null);

/// <summary>
/// Peticion para cerrar una novedad que quedo esperando desenlace.
///
/// Es el final de un reenvio o de una reclamacion: llego lo que faltaba, la
/// transportadora respondio, o se dio por perdido. Cerrarla es lo que saca al
/// traslado de la lista de pendientes.
///
/// NO BORRA NADA. La novedad se conserva entera -tipo, cantidad, quien la
/// reporto, cuando- y se le anade el desenlace. Borrarla dejaria un traslado
/// que llego corto sin rastro de por que.
/// </summary>
/// <param name="Motivo">
/// El porque. OBLIGATORIO.
///
/// Es lo unico que queda para entender, dentro de seis meses, por que aquel
/// faltante no se le cobro a nadie. Sin exigirlo, la mitad de los cierres
/// quedarian en blanco y cerrar seria indistinguible de borrar.
/// </param>
public sealed record CerrarNovedadDto(string? Motivo = null);
