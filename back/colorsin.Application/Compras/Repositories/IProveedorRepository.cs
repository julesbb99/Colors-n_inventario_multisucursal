using Colorsin.Domain.Compras;

namespace Colorsin.Application.Compras.Repositories;

/// <summary>Acceso de lectura al catalogo de proveedores.</summary>
public interface IProveedorRepository
{
    /// <summary>
    /// Todos los proveedores, ordenados por nombre, con la coleccion
    /// <c>Productos</c> cargada para poder contar cuantos surte cada uno.
    /// </summary>
    Task<IReadOnlyList<Proveedor>> ObtenerTodosAsync(CancellationToken cancellationToken = default);

    /// <summary>El proveedor con ese id, o <c>null</c> si no existe.</summary>
    Task<Proveedor?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Si existe un proveedor con ese id, sin traer la fila.
    ///
    /// Es una consulta aparte porque validar al crear una orden solo necesita
    /// saber si esta o no; traer el proveedor entero y sus productos para eso
    /// es gasto innecesario.
    /// </summary>
    Task<bool> ExisteAsync(int id, CancellationToken cancellationToken = default);
}
