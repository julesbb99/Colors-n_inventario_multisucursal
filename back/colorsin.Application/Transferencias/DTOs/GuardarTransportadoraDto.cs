namespace Colorsin.Application.Transferencias.DTOs;

/// <summary>
/// Alta o edicion de una transportadora.
///
/// SIRVE PARA LAS DOS porque los campos son exactamente los mismos: el id va en
/// la ruta, no en el cuerpo. Dos DTO identicos es como uno termina ganando un
/// campo que el otro no tiene y nadie se entera hasta que falla.
/// </summary>
/// <param name="Nombre">
/// Razon social. Unico en el catalogo, ignorando mayusculas y espacios de los
/// extremos: "Redetrans" y "redetrans " son la misma empresa.
/// </param>
/// <param name="TipoServicio">
/// 'urgente' o 'estandar', en minuscula, como los valores del ENUM en la base.
/// No distingue mayusculas al entrar.
///
/// ES UNA ETIQUETA, NO UN PLAZO: sirve para agrupar en el selector, pero la
/// fecha de llegada se calcula con <paramref name="DiasEntrega"/>.
/// </param>
/// <param name="DiasEntrega">
/// Dias que tarda en entregar entre sedes. Al menos 1.
///
/// Es el dato que de verdad se usa: al elegirla en un despacho, la pantalla suma
/// estos dias a la fecha y rellena la llegada estimada. Dos empresas 'urgente'
/// pueden tardar 1 y 2 dias, y por eso el plazo va aqui y no en el tipo.
/// </param>
public sealed record GuardarTransportadoraDto(
    string Nombre,
    string TipoServicio,
    byte DiasEntrega);
