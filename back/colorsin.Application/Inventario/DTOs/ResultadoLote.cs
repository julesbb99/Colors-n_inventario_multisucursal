namespace Colorsin.Application.Inventario.DTOs;

/// <summary>
/// Por que se rechazo el alta o la edicion de un lote.
///
/// Enum y no texto libre, por lo mismo que <see cref="ErrorMovimiento"/>: la capa
/// HTTP traduce cada caso a su codigo sin tener que comparar mensajes.
/// </summary>
public enum ErrorLote
{
    /// <summary>Sin error.</summary>
    Ninguno = 0,

    /// <summary>El numero de lote llego vacio o solo con espacios.</summary>
    NumeroLoteVacio,

    /// <summary>
    /// La correccion llego sin fecha de caducidad.
    ///
    /// Es obligatoria desde que todo lo que vende Colorsin caduca. Importa
    /// cerrarlo tambien aqui y no solo en la recepcion: si no, bastaria con
    /// crear el lote con fecha y borrarsela despues por este camino, y el lote
    /// acabaria fuera de las alertas y al final de la cola FEFO.
    /// </summary>
    VencimientoRequerido,

    /// <summary>El producto no existe.</summary>
    ProductoNoEncontrado,

    /// <summary>La sede no existe.</summary>
    SucursalNoEncontrada,

    /// <summary>El lote que se quiere editar no existe.</summary>
    LoteNoEncontrado,

    /// <summary>
    /// Ya hay otro lote con ese numero para la misma pareja (producto, sede).
    /// Es un 409: la peticion esta bien formada, lo que no admite la operacion es
    /// el estado actual de los datos.
    /// </summary>
    NumeroLoteDuplicado
}

/// <summary>
/// Desenlace de un alta o una edicion de lote.
///
/// Las reglas de negocio se devuelven, no se lanzan, igual que en
/// <see cref="ResultadoMovimiento"/>: que un numero de lote este repetido es un
/// desenlace corriente del dia a dia, no una condicion excepcional.
/// </summary>
/// <param name="Exito">Si el lote quedo guardado.</param>
/// <param name="Error">Motivo del rechazo. <see cref="ErrorLote.Ninguno"/> si hubo exito.</param>
/// <param name="Mensaje">Explicacion legible, con los datos concretos del caso.</param>
/// <param name="Lote">
/// El lote tal como quedo. Nulo si hubo rechazo. Viaja de vuelta para que quien
/// creo no tenga que volver a consultarlo solo para saber que id le toco.
/// </param>
public sealed record ResultadoLote(
    bool Exito,
    ErrorLote Error,
    string Mensaje,
    LoteDto? Lote = null)
{
    public static ResultadoLote Fallo(ErrorLote error, string mensaje) =>
        new(false, error, mensaje);

    public static ResultadoLote Ok(LoteDto lote, string mensaje) =>
        new(true, ErrorLote.Ninguno, mensaje, lote);
}
