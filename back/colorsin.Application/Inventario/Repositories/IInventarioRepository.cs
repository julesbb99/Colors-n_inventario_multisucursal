using Colorsin.Domain.Inventario;

namespace Colorsin.Application.Inventario.Repositories;

/// <summary>
/// Acceso al stock: saldos, lotes y libro mayor.
///
/// Los metodos que escriben NO guardan: solo dejan el cambio preparado. Quien
/// decide cuando confirmar es el servicio, con <see cref="GuardarCambiosAsync"/>
/// dentro de una transaccion. Asi el saldo, el movimiento y el evento de
/// auditoria se confirman juntos o no se confirma ninguno.
/// </summary>
public interface IInventarioRepository
{
    // -------------------------------------------------------------------------
    // Consultas
    // -------------------------------------------------------------------------

    /// <summary>
    /// Existencias de una sede, o de toda la red si <paramref name="sucursalId"/>
    /// es nulo. Incluye producto, sede y unidad base.
    /// </summary>
    Task<IReadOnlyList<InventarioSucursal>> ObtenerExistenciasAsync(
        int? sucursalId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Saldo de una pareja (sede, producto), o <c>null</c> si no hay fila.</summary>
    Task<InventarioSucursal?> ObtenerSaldoAsync(
        int sucursalId,
        int productoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Igual que <see cref="ObtenerSaldoAsync"/>, pero bloqueando la fila
    /// (SELECT ... FOR UPDATE) hasta el final de la transaccion.
    ///
    /// Es lo que evita el error clasico de todo inventario: dos retiros
    /// simultaneos leen el mismo saldo de 100, cada uno resta 60 y el stock
    /// termina en 40 en vez de rechazarse el segundo. Con el bloqueo, el
    /// segundo espera y lee 40.
    ///
    /// Exige una transaccion abierta; sin ella MySQL confirma cada sentencia
    /// al instante y el bloqueo se libera de inmediato.
    /// </summary>
    Task<InventarioSucursal?> ObtenerSaldoParaActualizarAsync(
        int sucursalId,
        int productoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lotes disponibles de un producto en una sede, en orden FEFO: primero el
    /// que vence antes.
    ///
    /// Los lotes sin fecha de vencimiento van al final, no al principio. MySQL
    /// ordena los NULL primero por defecto, y eso pondria lo que no caduca
    /// delante de lo que si, justo al reves de lo que pide FEFO. Entre lotes
    /// con la misma fecha desempata la de ingreso (FIFO).
    ///
    /// Solo devuelve lotes con cantidad mayor que cero: los agotados no sirven
    /// para despachar.
    /// </summary>
    Task<IReadOnlyList<Lote>> ObtenerLotesPorVencimientoAsync(
        int sucursalId,
        int productoId,
        CancellationToken cancellationToken = default);

    /// <summary>El lote con ese id, o <c>null</c>. Bloquea la fila, como el saldo.</summary>
    Task<Lote?> ObtenerLoteParaActualizarAsync(
        int loteId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca un lote por su numero dentro de una sede y un producto, bloqueando
    /// la fila. Devuelve <c>null</c> si esa sede no tiene todavia ese lote.
    ///
    /// Lo usa la recepcion de compras para decidir entre sumar a un lote que ya
    /// existe o crear uno nuevo.
    ///
    /// La unicidad de (producto_id, sucursal_id, numero_lote) la garantiza el
    /// indice `ux_lotes_producto_sucursal_numero`, asi que como mucho hay una
    /// fila. Ver la nota de la implementacion sobre por que el FOR UPDATE no
    /// bastaba por si solo.
    /// </summary>
    Task<Lote?> ObtenerLotePorNumeroParaActualizarAsync(
        int sucursalId,
        int productoId,
        string numeroLote,
        CancellationToken cancellationToken = default);

    /// <summary>Crea un lote nuevo.</summary>
    void AgregarLote(Lote lote);

    /// <summary>
    /// Saldos en alerta: los que cumplen <c>cantidad_base &lt;= stock_minimo</c>.
    /// Filtra por sede si se indica.
    /// </summary>
    Task<IReadOnlyList<InventarioSucursal>> ObtenerAlertasStockBajoAsync(
        int? sucursalId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Movimientos del libro mayor, del mas reciente al mas antiguo.
    /// Los filtros nulos no se aplican.
    /// </summary>
    Task<IReadOnlyList<MovimientoInventario>> ObtenerMovimientosAsync(
        int? sucursalId = null,
        int? productoId = null,
        int limite = 100,
        CancellationToken cancellationToken = default);

    // -------------------------------------------------------------------------
    // Escritura: preparan el cambio, no lo confirman
    // -------------------------------------------------------------------------

    /// <summary>Fija el nuevo saldo de una fila de existencias.</summary>
    void ActualizarCantidadBase(InventarioSucursal saldo, decimal nuevaCantidadBase);

    /// <summary>
    /// Crea la fila de saldo de una pareja (sede, producto) que aun no la tenia.
    /// Solo la usa el primer Ingreso de un producto en una sede.
    /// </summary>
    void AgregarSaldo(InventarioSucursal saldo);

    /// <summary>Fija la nueva cantidad de un lote.</summary>
    void ActualizarCantidadLote(Lote lote, decimal nuevaCantidadBase);

    /// <summary>Anexa una fila al libro mayor.</summary>
    void AgregarMovimiento(MovimientoInventario movimiento);

    /// <summary>Confirma en la base todo lo preparado. Devuelve las filas afectadas.</summary>
    Task<int> GuardarCambiosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta <paramref name="operacion"/> dentro de una transaccion: se
    /// confirma si termina bien y se revierte ante cualquier excepcion.
    ///
    /// Es un metodo que recibe la operacion, y no un par abrir/confirmar, por
    /// dos razones concretas:
    ///
    /// 1. La conexion esta configurada con reintentos
    ///    (<c>EnableRetryOnFailure</c>). Con esa opcion, EF Core RECHAZA las
    ///    transacciones abiertas a mano y exige pasar por su estrategia de
    ///    ejecucion, que necesita recibir la operacion completa para poder
    ///    reintentarla entera. Un par abrir/confirmar no le sirve.
    /// 2. Con la operacion adentro es imposible olvidar el rollback en un
    ///    camino de salida.
    ///
    /// Si la operacion se reintenta, se vuelve a ejecutar desde el principio,
    /// releyendo saldos y lotes. Por eso no debe modificar estado en memoria
    /// que sobreviva entre intentos.
    /// </summary>
    Task<T> EjecutarEnTransaccionAsync<T>(
        Func<CancellationToken, Task<T>> operacion,
        CancellationToken cancellationToken = default);
}
