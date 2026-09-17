namespace Colorsin.Domain.Compras;

/// <summary>
/// Ciclo de vida de una orden de compra.
///
/// El stock se afecta en cada recepcion, no solo al final: una orden puede
/// pasar por <see cref="ParcialmenteRecibida"/> varias veces, y cada paso
/// ingresa mercancia real.
///
/// OJO: el valor 'Confirmada' que existia antes se retiro al agregar
/// 'ParcialmenteRecibida'. Servia para marcar que el proveedor acuso recibo del
/// pedido, un concepto distinto de la entrega parcial. Si se necesita, hay que
/// devolverlo aqui Y al ENUM de la base, que deben coincidir valor por valor.
/// </summary>
public enum EstadoOrdenCompra
{
    /// <summary>Creada, sin recibir nada todavia.</summary>
    Pendiente,

    /// <summary>Llego parte de la mercancia. Admite mas recepciones.</summary>
    ParcialmenteRecibida,

    /// <summary>Todas las lineas completaron la cantidad pedida. No admite mas recepciones.</summary>
    Recibida,

    /// <summary>Anulada. No admite recepciones.</summary>
    Cancelada
}
