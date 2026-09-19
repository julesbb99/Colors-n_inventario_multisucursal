using Colorsin.Domain.Compras;

namespace Colorsin.Application.Compras.Repositories;

/// <summary>
/// Acceso a las ordenes de compra y su detalle.
///
/// Igual que en Inventario, los metodos que escriben solo dejan el cambio
/// preparado; confirmar es cosa de <see cref="GuardarCambiosAsync"/> dentro de
/// la transaccion que abre el servicio.
/// </summary>
public interface IOrdenCompraRepository
{
    /// <summary>
    /// Ordenes, de la mas reciente a la mas antigua. Los filtros nulos no se
    /// aplican. NO carga el detalle: para eso esta
    /// <see cref="ObtenerPorIdAsync"/>.
    /// </summary>
    Task<IReadOnlyList<OrdenCompra>> ObtenerAsync(
        int? sucursalId = null,
        int? proveedorId = null,
        EstadoOrdenCompra? estado = null,
        int limite = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// La orden con ese id y todo su detalle (producto, unidad base y unidad de
    /// compra), o <c>null</c>. Solo lectura.
    /// </summary>
    Task<OrdenCompra?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Como <see cref="ObtenerPorIdAsync"/>, pero con la entidad rastreada y la
    /// fila del encabezado bloqueada hasta el final de la transaccion.
    ///
    /// Ese bloqueo es lo que impide que dos confirmaciones simultaneas de la
    /// misma orden vean las dos el estado 'Pendiente' y suban el stock dos
    /// veces.
    /// </summary>
    Task<OrdenCompra?> ObtenerParaRecibirAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Crea la orden y sus lineas.</summary>
    void AgregarOrden(OrdenCompra orden);

    /// <summary>Fija el estado de una orden.</summary>
    void ActualizarEstado(OrdenCompra orden, EstadoOrdenCompra estado);

    /// <summary>
    /// Fija cuanto se lleva recibido de una linea, acumulado. En la unidad de
    /// compra de la linea, la misma de <c>Cantidad</c>.
    /// </summary>
    void ActualizarCantidadRecibida(OrdenCompraDetalle detalle, decimal cantidadRecibida);

    /// <summary>
    /// La orden con sus lineas, CON SEGUIMIENTO, para editarla o retirarla.
    ///
    /// Distinta de <see cref="ObtenerParaRecibirAsync"/>: aquella bloquea la
    /// fila porque la recepcion mueve stock y compite con otras recepciones.
    /// Editar un borrador no mueve nada, asi que no hace falta el bloqueo.
    /// </summary>
    Task<OrdenCompra?> ObtenerParaEditarAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Quita lineas de una orden. Solo al reemplazar las de un borrador.</summary>
    void QuitarDetalles(IEnumerable<OrdenCompraDetalle> detalles);

    /// <summary>Anade una linea a una orden existente.</summary>
    void AgregarDetalle(OrdenCompraDetalle detalle);

    /// <summary>
    /// La ULTIMA linea con precio de ese producto a ese proveedor, mirando el
    /// historico de ordenes de toda la red.
    ///
    /// DE TODA LA RED y no solo de la sede de quien pregunta: lo que cobra un
    /// proveedor no depende de a que bodega entrega, y acotarlo por sede
    /// esconderia el dato justo a la sede que aun no le ha comprado, que es la
    /// que mas lo necesita.
    ///
    /// Se descartan las lineas SIN precio -no informan de nada- y las de ordenes
    /// canceladas, porque un precio que nunca llego a ejecutarse no dice lo que
    /// se paga.
    /// </summary>
    Task<OrdenCompraDetalle?> ObtenerUltimaLineaConPrecioAsync(
        int productoId,
        int proveedorId,
        CancellationToken cancellationToken = default);

    /// <summary>Confirma en la base todo lo preparado.</summary>
    Task<int> GuardarCambiosAsync(CancellationToken cancellationToken = default);
}
