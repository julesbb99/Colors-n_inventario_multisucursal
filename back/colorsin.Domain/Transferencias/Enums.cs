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

/// <summary>Ciclo de vida de un traslado entre sedes.</summary>
public enum EstadoTransferencia
{
    Solicitada,
    EnPreparacion,
    EnTransito,
    RecibidaCompleta,
    RecibidaParcial
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
