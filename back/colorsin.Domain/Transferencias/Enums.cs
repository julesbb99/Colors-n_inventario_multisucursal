namespace Colorsin.Domain.Transferencias;

/// <summary>
/// Tipo de servicio de la transportadora.
/// En MySQL los valores van en minuscula ('urgente', 'estandar'); la
/// traduccion se hace con un convertidor en Infrastructure.
/// </summary>
public enum TipoServicio
{
    Urgente,
    Estandar
}

/// <summary>
/// Ciclo de vida de un traslado entre sedes.
///
///   Solicitada --despacho--> EnTransito --recepcion--> Completada
///                                       \-recepcion--> RecibidaParcial
///   Solicitada --------------------------------------> Rechazada
///   Solicitada --------------------------------------> Cancelada
///
/// Rechazada y Cancelada solo salen de Solicitada: despues del despacho la
/// mercancia ya salio del origen y anular el traslado dejaria stock sin dueno.
///
/// CAMBIOS RESPECTO A LA VERSION ANTERIOR
///   'RecibidaCompleta' paso a llamarse 'Completada'.
///   'EnPreparacion' se retiro: ningun flujo lo usaba.
///   'RecibidaParcial' SE CONSERVA aunque no estuviera en la lista pedida.
///   Sin el, una recepcion corta no tendria como representarse: marcarla
///   Completada seria mentir y dejarla EnTransito tambien. La tabla tiene
///   `cantidad_recibida` aparte de `cantidad_solicitada` justamente para eso.
///
/// El orden sigue al del ENUM en MySQL, y las dos listas deben coincidir valor
/// por valor: un estado que falte en la base falla con el error 1265.
/// </summary>
public enum EstadoTransferencia
{
    /// <summary>Pedida por la sede destino. Todavia no se movio nada.</summary>
    Solicitada,

    /// <summary>Despachada: el stock ya salio del origen y viaja.</summary>
    EnTransito,

    /// <summary>Llego todo lo que se pidio.</summary>
    Completada,

    /// <summary>
    /// Llego menos de lo despachado y todavia falta dar cuenta del faltante.
    /// La diferencia ya salio del inventario de la red.
    /// </summary>
    RecibidaParcial,

    /// <summary>
    /// Llego corto y ya se dio cuenta del faltante con una novedad: se acabo.
    ///
    /// NO ES <see cref="Completada"/>, y la distincion importa: aquella afirma
    /// que llego todo. Esta dice que se termino de gestionar aunque faltara
    /// mercancia, y <c>CantidadRecibida</c> sigue guardando cuanto llego.
    ///
    /// Existe porque sin ella un traslado corto se quedaba en
    /// <see cref="RecibidaParcial"/> para siempre, contando como trabajo en
    /// curso cuando ya no habia nada que hacer con el.
    /// </summary>
    Cerrada,

    /// <summary>La sede origen no la atiende.</summary>
    Rechazada,

    /// <summary>Anulada por quien la pidio, antes de despacharse.</summary>
    Cancelada
}

/// <summary>Prioridad del traslado.</summary>
public enum Urgencia
{
    Baja,
    Media,
    Alta
}

/// <summary>Clase de incidencia reportada sobre un traslado.</summary>
public enum TipoNovedad
{
    Faltante,
    Averia,
    Sobrante,
    Retraso
}

/// <summary>
/// Que se va a hacer con lo que se reporto en una novedad.
///
/// ES LO QUE DECIDE SI EL TRASLADO PUEDE CERRARSE. Antes una novedad cerraba
/// siempre el traslado que llego corto, y eso daba por terminado lo que todavia
/// estaba en curso: si el origen va a reenviar lo que falto, o si se le esta
/// reclamando a la transportadora, queda trabajo por delante y el traslado
/// sigue siendo un pendiente.
///
/// El orden sigue al del ENUM de MySQL y las dos listas deben coincidir valor
/// por valor.
/// </summary>
public enum TratamientoNovedad
{
    /// <summary>
    /// Solo se deja constancia. Un retraso que ya paso, una lata abollada cuyo
    /// contenido llego entero: no hay nada que esperar.
    /// </summary>
    Ninguno,

    /// <summary>El origen vuelve a mandar lo que falto. El traslado sigue pendiente.</summary>
    Reenvio,

    /// <summary>Se le cobra a la transportadora. El traslado sigue pendiente.</summary>
    Reclamacion,

    /// <summary>
    /// Se da por perdido. ESTO es la merma: la mercancia salio del origen, nunca
    /// llego y nadie la va a responder. Cierra el traslado.
    /// </summary>
    Asumido
}

/// <summary>
/// Si la novedad sigue esperando un desenlace.
///
/// Va en la NOVEDAD y no solo en el traslado porque un mismo traslado puede
/// acumular varias -llego corto y ademas una lata rota- y cada una se resuelve
/// por su lado. El traslado cierra cuando no le queda ninguna abierta.
/// </summary>
public enum EstadoNovedad
{
    Abierta,
    Cerrada
}
