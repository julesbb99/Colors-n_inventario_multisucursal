using Colorsin.Application.Transferencias.DTOs;
using Colorsin.Domain.Inventario;
using Colorsin.Domain.Transferencias;

namespace Colorsin.Application.Transferencias.Mapping;

/// <summary>Conversion de entidades del modulo Transferencias a sus DTOs.</summary>
public static class MapeosTransferencias
{
    public static TransportadoraDto ToDto(this Transportadora transportadora) => new(
        transportadora.Id,
        transportadora.Nombre,
        // El convertidor del AppDbContext traduce entre el enum y los valores
        // en minuscula de la base; aqui se expone el texto tal como se guarda.
        transportadora.TipoServicio == TipoServicio.Urgente ? "urgente" : "estandar",
        transportadora.DiasEntrega,
        transportadora.Activo);

    public static NovedadTransferenciaDto ToDto(this NovedadTransferencia novedad) => new(
        novedad.Id,
        novedad.TransferenciaId,
        novedad.UsuarioId,
        novedad.Usuario?.Nombre ?? string.Empty,
        novedad.Tipo?.ToString(),
        novedad.CantidadAfectada,
        novedad.Tratamiento.ToString(),
        novedad.Estado.ToString(),
        novedad.MotivoCierre,
        novedad.FechaCierre,
        novedad.UsuarioCierre?.Nombre,
        novedad.Observaciones,
        novedad.Fecha);

    /// <summary>
    /// Mapea una fila del libro mayor como linea de detalle del traslado.
    ///
    /// El lote se lee de la navegacion: en el despacho es el del origen y en la
    /// recepcion el recreado en el destino, con el mismo numero y vencimiento.
    /// </summary>
    public static DetalleTransferenciaDto ToDetalleDto(this MovimientoInventario movimiento) => new(
        movimiento.Id,
        movimiento.Tipo?.ToString(),
        movimiento.SucursalId,
        movimiento.Sucursal?.Nombre ?? string.Empty,
        movimiento.LoteId,
        movimiento.Lote?.NumeroLote,
        movimiento.Lote?.FechaVencimiento,
        movimiento.CantidadBase,
        movimiento.Fecha);

    /// <summary>
    /// Mapea el traslado. Los movimientos entran por parametro en vez de leerse
    /// de la entidad porque NO son una navegacion de
    /// <see cref="Transferencia"/>: viven en el libro mayor y se consultan
    /// aparte. Pasarlos explicitamente deja claro cuando la consulta los cargo
    /// y cuando no.
    ///
    /// LAS NOVEDADES SI SON NAVEGACION, y por eso, cuando no vienen por
    /// parametro, se leen de la entidad. El listado las carga -la tabla pinta
    /// el tipo de cada una, no un contador- y el detalle tambien; pasarlas
    /// explicitamente sigue valiendo para el caso en que ya se mapearon.
    /// </summary>
    public static TransferenciaDto ToDto(
        this Transferencia transferencia,
        IReadOnlyList<DetalleTransferenciaDto>? movimientos = null,
        IReadOnlyList<NovedadTransferenciaDto>? novedades = null)
    {
        var lista = novedades ?? transferencia.Novedades
            // Mas reciente primero: lo ultimo que paso con el traslado es lo
            // que se quiere ver de un vistazo en la tabla.
            .OrderByDescending(n => n.Fecha)
            .ThenByDescending(n => n.Id)
            .Select(n => n.ToDto())
            .ToList();

        return new TransferenciaDto(
            transferencia.Id,
            transferencia.ProductoId,
            transferencia.Producto?.Nombre ?? string.Empty,
            transferencia.SucursalOrigenId,
            transferencia.SucursalOrigen?.Nombre ?? string.Empty,
            transferencia.SucursalDestinoId,
            transferencia.SucursalDestino?.Nombre ?? string.Empty,
            transferencia.UsuarioId,
            transferencia.Usuario?.Nombre ?? string.Empty,
            transferencia.TransportadoraId,
            transferencia.Transportadora?.Nombre,
            transferencia.Guia,
            transferencia.CantidadSolicitada,
            transferencia.CantidadDespachada,
            transferencia.CantidadRecibida,
            transferencia.UnidadId,
            transferencia.Unidad?.Simbolo ?? string.Empty,
            transferencia.Estado?.ToString(),
            transferencia.Urgencia?.ToString(),
            transferencia.FechaSolicitud,
            transferencia.FechaDespacho,
            transferencia.FechaEstimadaLlegada,
            transferencia.FechaRecepcion,
            lista.Count(n => n.Estado == nameof(EstadoNovedad.Abierta)),
            movimientos ?? [],
            lista);
    }
}
