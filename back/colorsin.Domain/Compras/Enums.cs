namespace Colorsin.Domain.Compras;

/// <summary>
/// Ciclo de vida de una orden de compra. Solo al pasar a
/// <see cref="Recibida"/> se afecta el stock real.
/// </summary>
public enum EstadoOrdenCompra
{
    Pendiente,
    Confirmada,
    Recibida,
    Cancelada
}
