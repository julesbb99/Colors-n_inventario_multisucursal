using Colorsin.Domain.Compras;

namespace Colorsin.Application.Compras.DTOs;

/// <summary>
/// Por que se rechazo una operacion de compras.
///
/// Enum y no texto libre, por lo mismo que en Inventario: la capa HTTP traduce
/// cada caso a su codigo (404 lo que no existe, 409 el conflicto de estado,
/// 400 lo mal digitado) sin comparar mensajes.
/// </summary>
public enum ErrorCompra
{
    /// <summary>Sin error.</summary>
    Ninguno = 0,

    /// <summary>La orden no trae lineas.</summary>
    OrdenSinLineas,

    /// <summary>Alguna linea viene con cantidad cero o negativa.</summary>
    CantidadInvalida,

    /// <summary>Un precio unitario negativo.</summary>
    PrecioInvalido,

    /// <summary>Un descuento fuera del rango 0..100.</summary>
    DescuentoInvalido,

    /// <summary>Un plazo de pago negativo.</summary>
    PlazoPagoInvalido,

    /// <summary>El proveedor no existe.</summary>
    ProveedorNoEncontrado,

    /// <summary>
    /// No vino el usuario responsable. Se valida aqui, y no se deja para la FK,
    /// porque un id cero o negativo llegaria a MySQL y volveria como error 500
    /// en vez de como un rechazo con motivo.
    /// </summary>
    UsuarioNoIndicado,

    /// <summary>Algun producto de las lineas no existe.</summary>
    ProductoNoEncontrado,

    /// <summary>Un producto sin unidad base: no hay a que convertir al recibir.</summary>
    ProductoSinUnidadBase,

    /// <summary>La unidad de compra de alguna linea no existe.</summary>
    UnidadNoEncontrada,

    /// <summary>
    /// La unidad de compra y la unidad base del producto no son convertibles
    /// entre si: alguna no tiene factor a litros.
    /// </summary>
    ConversionImposible,

    /// <summary>Convertida a unidad base, alguna cantidad se redondea a cero.</summary>
    CantidadBaseCero,

    /// <summary>La orden no existe.</summary>
    OrdenNoEncontrada,

    /// <summary>
    /// La orden no esta en un estado desde el que se pueda recibir: ya se
    /// recibio, o se cancelo.
    /// </summary>
    EstadoNoPermiteRecepcion,

    /// <summary>Una linea de la entrega apunta a un detalle que no es de esta orden.</summary>
    LineaNoPertenece,

    /// <summary>La misma linea aparece dos veces en la entrega.</summary>
    LoteDuplicadoEnPeticion,

    /// <summary>
    /// Una linea de la entrega trae mas de lo que falta por recibir. Se rechaza
    /// en vez de recortarse: recibir de mas suele significar que se digito la
    /// linea equivocada, y recortar en silencio ocultaria el error.
    /// </summary>
    CantidadRecibidaExcedeSolicitada,

    /// <summary>
    /// La entrega no aporta nada: o no trae lineas con saldo pendiente, o todas
    /// las de la orden ya estaban completas.
    /// </summary>
    NadaPorRecibir,

    /// <summary>
    /// Se intento editar o retirar una orden que ya salio de 'Pendiente'.
    ///
    /// Una orden deja de ser un borrador en cuanto se confirma o entra
    /// mercancia: a partir de ahi hay un compromiso con el proveedor y, si hubo
    /// recepcion, stock movido y asientos en el libro mayor. Cambiarla seria
    /// reescribir el papel con el que se recibio.
    /// </summary>
    EstadoNoPermiteEdicion,

    /// <summary>Falta el nombre o el telefono del proveedor.</summary>
    DatosProveedorIncompletos,

    /// <summary>Ya hay otro proveedor con ese nombre.</summary>
    ProveedorDuplicado,

    /// <summary>Se pidio retirar uno ya retirado, o reactivar uno ya activo.</summary>
    ProveedorEstadoSinCambio,

    /// <summary>El producto no esta asociado a ese proveedor en la lista de precios.</summary>
    PrecioNoEncontrado
}

/// <summary>Desenlace de crear una orden de compra.</summary>
/// <param name="Exito">Si la orden quedo creada.</param>
/// <param name="Error">Motivo del rechazo.</param>
/// <param name="Mensaje">Explicacion legible.</param>
/// <param name="OrdenCompraId">Id de la orden creada.</param>
/// <param name="Total">Suma de los subtotales netos.</param>
public sealed record ResultadoOrdenCompra(
    bool Exito,
    ErrorCompra Error,
    string Mensaje,
    int? OrdenCompraId = null,
    decimal? Total = null)
{
    public static ResultadoOrdenCompra Fallo(ErrorCompra error, string mensaje) =>
        new(false, error, mensaje);

    public static ResultadoOrdenCompra Ok(int ordenCompraId, decimal total) =>
        new(true, ErrorCompra.Ninguno, "Orden de compra creada en estado Pendiente.",
            ordenCompraId, total);
}

/// <summary>Lo que entro al stock por una linea de la orden recibida.</summary>
/// <param name="DetalleId">Linea de la orden.</param>
/// <param name="ProductoId">Producto.</param>
/// <param name="ProductoNombre">Nombre del producto.</param>
/// <param name="CantidadRecibidaAhora">Lo que llego en ESTA entrega, en unidad de compra.</param>
/// <param name="CantidadRecibidaTotal">Lo acumulado tras esta entrega, en unidad de compra.</param>
/// <param name="CantidadSolicitada">Lo pedido en la linea, en unidad de compra.</param>
/// <param name="CantidadPendiente">Lo que sigue faltando. Cero si la linea quedo completa.</param>
/// <param name="UnidadId">Unidad de compra.</param>
/// <param name="CantidadBaseIngresada">Lo de esta entrega, convertido a unidad base del producto.</param>
/// <param name="SaldoResultante">Saldo de la sede despues de este ingreso.</param>
/// <param name="MovimientoId">Fila creada en el libro mayor.</param>
/// <param name="LoteId">Lote afectado, si la entrega traia numero de lote.</param>
/// <param name="LoteCreado">
/// <c>true</c> si el lote se creo en esta entrega, <c>false</c> si se sumo a
/// uno que ya existia. Nulo si la linea no traia lote.
/// </param>
public sealed record LineaRecibidaDto(
    int DetalleId,
    int ProductoId,
    string ProductoNombre,
    decimal CantidadRecibidaAhora,
    decimal CantidadRecibidaTotal,
    decimal CantidadSolicitada,
    decimal CantidadPendiente,
    int UnidadId,
    decimal CantidadBaseIngresada,
    decimal SaldoResultante,
    int MovimientoId,
    int? LoteId,
    bool? LoteCreado);

/// <summary>Desenlace de registrar una entrega de una orden.</summary>
/// <param name="Exito">Si la entrega quedo registrada.</param>
/// <param name="Error">Motivo del rechazo.</param>
/// <param name="Mensaje">Explicacion legible.</param>
/// <param name="OrdenCompraId">Orden afectada.</param>
/// <param name="EstadoOrden">
/// Como quedo la orden: 'Recibida' si todas las lineas completaron lo pedido,
/// 'ParcialmenteRecibida' si todavia falta algo.
/// </param>
/// <param name="Completa">
/// <c>true</c> cuando ya no falta nada. Es el mismo dato que
/// <paramref name="EstadoOrden"/>, en forma de bandera para no obligar a
/// comparar contra un texto.
/// </param>
/// <param name="Lineas">Detalle de lo que entro al stock en ESTA entrega.</param>
public sealed record ResultadoRecepcion(
    bool Exito,
    ErrorCompra Error,
    string Mensaje,
    int? OrdenCompraId = null,
    string? EstadoOrden = null,
    bool? Completa = null,
    IReadOnlyList<LineaRecibidaDto>? Lineas = null)
{
    public static ResultadoRecepcion Fallo(ErrorCompra error, string mensaje) =>
        new(false, error, mensaje);

    public static ResultadoRecepcion Ok(
        int ordenCompraId,
        EstadoOrdenCompra estado,
        IReadOnlyList<LineaRecibidaDto> lineas)
    {
        var completa = estado == EstadoOrdenCompra.Recibida;
        var mensaje = completa
            ? $"Orden {ordenCompraId} recibida por completo: " +
              $"{lineas.Count} linea(s) ingresadas al stock en esta entrega."
            : $"Entrega parcial registrada en la orden {ordenCompraId}: " +
              $"{lineas.Count} linea(s) ingresadas al stock. Quedan lineas pendientes.";

        return new ResultadoRecepcion(
            true, ErrorCompra.Ninguno, mensaje,
            ordenCompraId, estado.ToString(), completa, lineas);
    }
}
