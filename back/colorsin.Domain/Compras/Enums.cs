namespace Colorsin.Domain.Compras;

/// <summary>
/// Ciclo de vida de una orden de compra.
///
/// El stock se afecta en cada recepcion, no solo al final: una orden puede
/// pasar por <see cref="ParcialmenteRecibida"/> varias veces, y cada paso
/// ingresa mercancia real.
///
/// El orden de los valores sigue al del ENUM en MySQL. Los estados se guardan
/// como texto, asi que el orden no cambia lo almacenado, pero si cambia el
/// numero ordinal de cada uno: un cliente que mande el estado como numero en
/// JSON vera otro significado si esta lista se reordena.
/// </summary>
public enum EstadoOrdenCompra
{
    /// <summary>Creada, sin recibir nada todavia.</summary>
    Pendiente,

    /// <summary>
    /// El proveedor acuso recibo del pedido. Es un concepto distinto de la
    /// entrega parcial: aqui todavia no ha llegado mercancia, solo esta
    /// confirmado que el pedido se va a surtir.
    /// </summary>
    Confirmada,

    /// <summary>Llego parte de la mercancia. Admite mas recepciones.</summary>
    ParcialmenteRecibida,

    /// <summary>Todas las lineas completaron la cantidad pedida. No admite mas recepciones.</summary>
    Recibida,

    /// <summary>Anulada. No admite recepciones.</summary>
    Cancelada
}
