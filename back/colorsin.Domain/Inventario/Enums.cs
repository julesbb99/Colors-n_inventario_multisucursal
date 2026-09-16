namespace Colorsin.Domain.Inventario;

/// <summary>
/// Direccion del movimiento. La cantidad se guarda siempre positiva;
/// el signo lo aporta este tipo.
/// </summary>
public enum TipoMovimiento
{
    Ingreso,
    Retiro
}

/// <summary>Razon que origino el movimiento de stock.</summary>
public enum MotivoMovimiento
{
    Compra,
    Venta,
    Ajuste,
    Transferencia,
    Merma,
    Devolucion
}
