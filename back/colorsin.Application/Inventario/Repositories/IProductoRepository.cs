using Colorsin.Domain.Inventario;

namespace Colorsin.Application.Inventario.Repositories;

/// <summary>Acceso de lectura al catalogo de productos.</summary>
public interface IProductoRepository
{
    /// <summary>Todos los productos, ordenados por nombre, con su unidad base cargada.</summary>
    Task<IReadOnlyList<Producto>> ObtenerTodosAsync(CancellationToken cancellationToken = default);

    /// <summary>El producto con ese id, o <c>null</c> si no existe. Incluye la unidad base.</summary>
    Task<Producto?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Productos de una categoria, ordenados por nombre.</summary>
    Task<IReadOnlyList<Producto>> ObtenerPorCategoriaAsync(
        string categoria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// El producto con ese id CON SEGUIMIENTO, para poder modificarlo y guardar.
    ///
    /// Va aparte de <see cref="ObtenerPorIdAsync"/>, que usa <c>AsNoTracking</c>:
    /// sobre una entidad sin seguimiento, cambiar una propiedad y llamar a
    /// <c>SaveChanges</c> no guarda nada y no avisa de nada. Hoy lo usa solo el
    /// precio de venta.
    /// </summary>
    Task<Producto?> ObtenerParaActualizarAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>Confirma en la base los cambios preparados. Devuelve las filas afectadas.</summary>
    Task<int> GuardarCambiosAsync(CancellationToken cancellationToken = default);
}
