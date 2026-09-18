namespace Colorsin.Api.Endpoints;

/// <summary>
/// Traduccion de los resultados de negocio a respuestas HTTP.
///
/// LOS SERVICIOS NO SABEN DE HTTP, y es correcto que no sepan: devuelven un
/// objeto con <c>Exito</c>, un enum de error y un mensaje. Es aqui donde eso se
/// convierte en un codigo de estado, y conviene que la traduccion este escrita
/// en un solo sitio por modulo en vez de repetida en cada endpoint.
///
/// EL CRITERIO, que es el mismo en los cuatro modulos:
///
///   404  lo que se nombra no existe: un producto, un lote, una orden, un
///        traslado. Volver a intentar con los mismos datos dara igual.
///   409  existe, pero su estado actual no admite la operacion: no hay stock
///        suficiente, la orden ya se recibio, el traslado no esta 'Solicitada'.
///        Es el caso que SI puede cambiar solo, sin que el cliente corrija nada.
///   400  la peticion esta mal formada: cantidad negativa, sin lineas, una
///        conversion de unidades que no se puede hacer.
///
/// Distinguir 409 de 400 importa para quien consume: un 400 se arregla
/// corrigiendo lo que se mando, un 409 puede resolverse solo cuando entre
/// mercancia o alguien cierre el documento.
/// </summary>
internal static class RespuestasHttp
{
    /// <summary>
    /// Respuesta de error con el mensaje que redacto el servicio.
    ///
    /// El mensaje viaja tal cual porque los servicios de este proyecto lo
    /// escriben para que lo lea una persona -incluyen cantidades, saldos y el
    /// motivo concreto- y recortarlo aqui perderia justo lo util. No contiene
    /// datos de otras sedes ni de otros usuarios; la unica excepcion, el 403 de
    /// aislamiento, se maneja aparte en AccesoDenegadoHandler.
    /// </summary>
    public static IResult Fallo(int codigo, string titulo, string mensaje) =>
        Results.Problem(title: titulo, detail: mensaje, statusCode: codigo);
}
