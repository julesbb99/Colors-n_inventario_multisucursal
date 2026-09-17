namespace Colorsin.Application.Transferencias.DTOs;

/// <summary>Empresa de transporte usada para los despachos entre sedes.</summary>
/// <param name="Id">Clave primaria.</param>
/// <param name="Nombre">Razon social.</param>
/// <param name="TipoServicio">
/// 'urgente' o 'estandar', en minuscula, tal como estan definidos los valores
/// del ENUM en la base.
/// </param>
public sealed record TransportadoraDto(
    int Id,
    string Nombre,
    string TipoServicio);
