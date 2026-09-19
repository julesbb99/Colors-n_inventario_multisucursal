using Colorsin.Domain.Compras;

namespace Colorsin.Application.Compras.Repositories;

/// <summary>Acceso al catalogo de proveedores.</summary>
public interface IProveedorRepository
{
    /// <summary>
    /// Todos los proveedores, ordenados por nombre, con la coleccion
    /// <c>Productos</c> cargada para poder contar cuantos surte cada uno.
    /// </summary>
    /// <param name="incluirInactivos">
    /// <c>false</c> por defecto: los retirados no salen. En <c>true</c> los
    /// incluye, que es lo que necesita la pantalla desde la que se reactivan.
    /// </param>
    Task<IReadOnlyList<Proveedor>> ObtenerTodosAsync(
        bool incluirInactivos = false,
        CancellationToken cancellationToken = default);

    /// <summary>El proveedor con ese id, o <c>null</c> si no existe.</summary>
    Task<Proveedor?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Igual, pero CON SEGUIMIENTO: es el que se modifica y se guarda. Sin
    /// seguimiento los cambios se perderian en silencio al guardar.
    /// </summary>
    Task<Proveedor?> ObtenerParaEditarAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Si ya hay un proveedor con ese nombre, ignorando mayusculas y sin contar
    /// al de <paramref name="excluyendoId"/> (para poder renombrarse a si mismo).
    /// </summary>
    Task<bool> ExisteNombreAsync(
        string nombre,
        int? excluyendoId,
        CancellationToken cancellationToken = default);

    /// <summary>Da de alta un proveedor.</summary>
    void Agregar(Proveedor proveedor);

    /// <summary>
    /// El precio de lista de un producto para un proveedor, o <c>null</c> si esa
    /// pareja no esta en `producto_proveedor`.
    ///
    /// Devuelve la ENTIDAD y con seguimiento porque el mismo metodo sirve para
    /// leerla y para cambiarle el precio.
    /// </summary>
    Task<ProductoProveedor?> ObtenerPrecioAsync(
        int productoId,
        int proveedorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Todos los proveedores que tienen ese producto en su lista, con el
    /// proveedor cargado para poder nombrarlo. Incluye los retirados: su precio
    /// sigue siendo una referencia de lo que costaba.
    /// </summary>
    Task<IReadOnlyList<ProductoProveedor>> ObtenerPreciosDeProductoAsync(
        int productoId,
        CancellationToken cancellationToken = default);

    /// <summary>Anade una pareja producto-proveedor a la lista de precios.</summary>
    void AgregarPrecio(ProductoProveedor precio);

    /// <summary>Quita una pareja de la lista de precios. No toca ninguna orden.</summary>
    void QuitarPrecio(ProductoProveedor precio);

    /// <summary>Confirma los cambios pendientes.</summary>
    Task<int> GuardarCambiosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Si existe un proveedor con ese id, sin traer la fila.
    ///
    /// Es una consulta aparte porque validar al crear una orden solo necesita
    /// saber si esta o no; traer el proveedor entero y sus productos para eso
    /// es gasto innecesario.
    /// </summary>
    Task<bool> ExisteAsync(int id, CancellationToken cancellationToken = default);
}
