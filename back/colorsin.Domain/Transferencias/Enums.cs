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

    /// <summary>Llego menos de lo despachado. La diferencia se perdio en transito.</summary>
    RecibidaParcial,

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
