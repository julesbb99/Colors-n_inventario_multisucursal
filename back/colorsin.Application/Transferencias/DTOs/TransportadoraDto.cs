namespace Colorsin.Application.Transferencias.DTOs;

/// <summary>Empresa de transporte usada para los despachos entre sedes.</summary>
/// <param name="Id">Clave primaria.</param>
/// <param name="Nombre">Razon social.</param>
/// <param name="TipoServicio">
/// 'urgente' o 'estandar', en minuscula, tal como estan definidos los valores
/// del ENUM en la base.
/// </param>
/// <param name="DiasEntrega">
/// Dias que tarda en entregar. Es lo que permite calcular la fecha estimada de
/// llegada al elegirla en el despacho, en vez de teclearla a mano.
///
/// Va por transportadora y no por tipo de servicio: 'urgente' es una etiqueta
/// comercial, no un plazo.
/// </param>
public sealed record TransportadoraDto(
    int Id,
    string Nombre,
    string TipoServicio,
    byte DiasEntrega);
