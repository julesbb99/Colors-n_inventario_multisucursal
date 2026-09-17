using Colorsin.Domain.Ventas;

namespace Colorsin.Application.Ventas.Repositories;

/// <summary>
/// Acceso a las ventas y su detalle.
///
/// Igual que en los otros modulos, los metodos que escriben solo dejan el
/// cambio preparado; confirmar es cosa de <see cref="GuardarCambiosAsync"/>
/// dentro de la transaccion que abre <see cref="EjecutarEnTransaccionAsync"/>.
/// </summary>
public interface IVentaRepository
{
    /// <summary>
    /// Ventas, de la mas reciente a la mas antigua. Los filtros nulos no se
    /// aplican. NO carga el detalle: para eso esta
    /// <see cref="ObtenerPorIdAsync"/>.
    /// </summary>
    Task<IReadOnlyList<Venta>> ObtenerAsync(
        int? sucursalId = null,
        int? clienteId = null,
        DateTime? desde = null,
        DateTime? hasta = null,
        int limite = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// La venta con ese id y todo su detalle (producto y unidad), o <c>null</c>.
    /// Solo lectura.
    /// </summary>
    Task<Venta?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Crea la venta y sus lineas.</summary>
    void AgregarVenta(Venta venta);

    /// <summary>Confirma en la base todo lo preparado.</summary>
    Task<int> GuardarCambiosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta <paramref name="operacion"/> dentro de una transaccion: se
    /// confirma si termina bien y se revierte ante cualquier excepcion,
    /// incluidas las de regla de negocio de este modulo.
    ///
    /// Es un metodo que recibe la operacion, y no un par abrir/confirmar,
    /// porque la conexion esta configurada con reintentos
    /// (<c>EnableRetryOnFailure</c>): con esa opcion EF Core rechaza las
    /// transacciones abiertas a mano y exige pasar por su estrategia de
    /// ejecucion, que necesita recibir la operacion completa para poder
    /// reintentarla entera.
    ///
    /// Comparte AppDbContext con los repositorios de inventario, asi que todo
    /// lo que ellos preparen entra en esta misma transaccion. Es lo que hace
    /// que la venta, el descuento del saldo, el de los lotes, los movimientos y
    /// la auditoria se confirmen juntos o no se confirme nada.
    /// </summary>
    Task<T> EjecutarEnTransaccionAsync<T>(
        Func<CancellationToken, Task<T>> operacion,
        CancellationToken cancellationToken = default);
}
