namespace Colorsin.Application.Transferencias.DTOs;

/// <summary>Por que se rechazo un alta o una edicion de transportadora.</summary>
public enum ErrorTransportadora
{
    /// <summary>Sin error.</summary>
    Ninguno = 0,

    /// <summary>La transportadora no existe.</summary>
    NoEncontrada,

    /// <summary>Falta el nombre.</summary>
    NombreNoIndicado,

    /// <summary>Ya hay otra transportadora con ese nombre.</summary>
    NombreDuplicado,

    /// <summary>El tipo de servicio no es 'urgente' ni 'estandar'.</summary>
    TipoServicioInvalido,

    /// <summary>
    /// El plazo de entrega es menor que un dia.
    ///
    /// Cero no describe a nadie que necesite camion, y la base lo rechaza igual
    /// con el CHECK <c>chk_transportadoras_dias_entrega</c>.
    /// </summary>
    DiasEntregaInvalido,

    /// <summary>
    /// Se pidio retirar una que ya estaba retirada, o reactivar una que ya
    /// estaba activa.
    ///
    /// Se responde en vez de callar para que la pantalla no diga "hecho" cuando
    /// no hizo nada: lo normal cuando pasa es que se pulso dos veces o que otra
    /// persona se adelanto.
    /// </summary>
    EstadoSinCambio
}

/// <summary>
/// Desenlace de un alta o una edicion de transportadora.
///
/// Sigue el patron de resultado del resto del modulo -y no excepciones- porque
/// un nombre repetido es un desenlace corriente de un formulario, no una
/// condicion excepcional.
/// </summary>
/// <param name="Exito">Si la operacion se completo.</param>
/// <param name="Error">Motivo del rechazo.</param>
/// <param name="Mensaje">Explicacion legible, con los datos concretos del caso.</param>
/// <param name="Transportadora">La fila resultante. Nula cuando hubo rechazo.</param>
public sealed record ResultadoTransportadora(
    bool Exito,
    ErrorTransportadora Error,
    string Mensaje,
    TransportadoraDto? Transportadora)
{
    public static ResultadoTransportadora Ok(TransportadoraDto transportadora, string mensaje) =>
        new(true, ErrorTransportadora.Ninguno, mensaje, transportadora);

    public static ResultadoTransportadora Fallo(ErrorTransportadora error, string mensaje) =>
        new(false, error, mensaje, null);
}
