using Colorsin.Application.Inventario.DTOs;

namespace Colorsin.Application.Inventario.Services;

/// <summary>Operaciones de stock: catalogo, consulta, movimientos y alertas.</summary>
public interface IInventarioService
{
    /// <summary>
    /// Catalogo de productos, con su unidad base resuelta.
    ///
    /// NO SE FILTRA POR SEDE, y no es un descuido: el catalogo es de la red. Que
    /// una sede no tenga saldo de un producto no significa que no lo maneje, y
    /// esconderselo impediria pedirlo por traslado o comprarlo, que es justo lo
    /// que hace falta cuando no hay. Lo que si es por sede son las existencias.
    /// </summary>
    /// <param name="categoria">
    /// Filtra por categoria exacta. Nulo o vacio trae el catalogo completo.
    /// </param>
    Task<IReadOnlyList<ProductoDto>> ObtenerProductosAsync(
        string? categoria = null,
        CancellationToken cancellationToken = default);

    /// <summary>Existencias de una sede, o de toda la red si no se indica sede.</summary>
    Task<IReadOnlyList<InventarioSucursalDto>> ObtenerExistenciasAsync(
        int? sucursalId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lotes disponibles de un producto en una sede, en orden FEFO: el primero
    /// de la lista es el que se debe despachar.
    /// </summary>
    Task<IReadOnlyList<LoteDto>> ObtenerLotesPorVencimientoAsync(
        int sucursalId,
        int productoId,
        CancellationToken cancellationToken = default);

    /// <summary>Movimientos del libro mayor, del mas reciente al mas antiguo.</summary>
    Task<IReadOnlyList<MovimientoInventarioDto>> ObtenerMovimientosAsync(
        int? sucursalId = null,
        int? productoId = null,
        int limite = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Productos cuyo saldo cayo a o por debajo del minimo
    /// (<c>CantidadBase &lt;= StockMinimo</c>).
    /// </summary>
    Task<IReadOnlyList<StockAlertaDto>> ObtenerAlertasStockBajoAsync(
        int? sucursalId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra una entrada o salida de stock.
    ///
    /// Convierte la cantidad a la unidad base del producto, valida que un
    /// retiro no deje el saldo en negativo, actualiza el saldo (y el lote, si
    /// se indico), anexa la fila al libro mayor y deja el evento de auditoria.
    /// Todo en una sola transaccion: o queda todo, o no queda nada.
    ///
    /// No lanza excepciones por reglas de negocio. Revisa
    /// <see cref="ResultadoMovimiento.Exito"/>.
    /// </summary>
    /// <param name="peticion">Que se mueve, de donde y cuanto.</param>
    /// <param name="usuarioId">
    /// Responsable del movimiento. Queda en el libro mayor y en la auditoria.
    ///
    /// Va como PARAMETRO y no dentro de <paramref name="peticion"/> porque el DTO
    /// es lo que se deserializa del cuerpo de la peticion HTTP, o sea lo que
    /// escribe el cliente. Aqui debe llegar
    /// <c>IUsuarioContexto.UsuarioIdRequerido()</c>, que sale del token firmado
    /// por el servidor. Con el id en el DTO, cualquiera podria imputar un
    /// movimiento a nombre de otro.
    /// </param>
    Task<ResultadoMovimiento> RegistrarMovimientoAsync(
        RegistrarMovimientoDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default);
}
