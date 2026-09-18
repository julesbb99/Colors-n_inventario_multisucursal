using Colorsin.Domain.Inventario;

namespace Colorsin.Application.Inventario.Repositories;

/// <summary>
/// Acceso a los lotes: consulta, alta y correccion de datos.
///
/// POR QUE ES UN REPOSITORIO APARTE DE <see cref="IInventarioRepository"/>, que
/// ya toca la tabla `lotes`. Los dos miran la misma tabla pero con proposito
/// opuesto, y mezclarlos hace facil equivocarse de metodo:
///
///   IInventarioRepository   los lotes como SALDO. Sus metodos bloquean filas
///                           (SELECT ... FOR UPDATE) porque se llaman dentro de
///                           una transaccion que va a mover cantidades.
///   ILoteRepository (este)  los lotes como FICHA. Consultas de lectura con
///                           AsNoTracking y la edicion de los datos digitados
///                           -numero y caducidad-, que no tocan cantidades.
///
/// La consecuencia practica: aqui NO hay ningun metodo que cambie
/// <c>CantidadBase</c>. Eso se hace solo por el camino que mueve a la vez el
/// consolidado de la sede y deja fila en el libro mayor.
///
/// Los metodos que escriben NO guardan: dejan el cambio preparado y quien llama
/// confirma, para que el evento de auditoria entre en la misma transaccion.
/// </summary>
public interface ILoteRepository
{
    // -------------------------------------------------------------------------
    // Consultas
    // -------------------------------------------------------------------------

    /// <summary>
    /// Lotes que cumplen los filtros, en orden FEFO: primero el que vence antes,
    /// y los que no caducan al final.
    ///
    /// Los filtros nulos no se aplican. Con los dos nulos trae los lotes de toda
    /// la red, que es lo que ve un administrador general.
    /// </summary>
    /// <param name="sucursalId">Sede, o nulo para todas.</param>
    /// <param name="productoId">Producto, o nulo para todos.</param>
    /// <param name="soloConSaldo">
    /// Deja fuera los lotes agotados. Falso por defecto: un lote recien creado
    /// esta en cero y tiene que poder verse, o quien acaba de crearlo pensaria
    /// que no se guardo.
    /// </param>
    /// <param name="limite">Tope de filas. Se acota entre 1 y 1000.</param>
    Task<IReadOnlyList<Lote>> ObtenerAsync(
        int? sucursalId = null,
        int? productoId = null,
        bool soloConSaldo = false,
        int limite = 200,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// El lote con ese id, o <c>null</c>. Solo lectura, con producto, unidad base
    /// y sede cargados.
    /// </summary>
    Task<Lote?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// El lote con ese id, RASTREADO por el contexto para poder modificarlo.
    ///
    /// Sin <c>AsNoTracking</c>, a diferencia del anterior: los cambios que se le
    /// hagan a la entidad devuelta se persisten al guardar. Es el unico metodo de
    /// esta interfaz del que eso es cierto.
    /// </summary>
    Task<Lote?> ObtenerParaEditarAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Si ya existe un lote con ese numero para la pareja (producto, sede).
    ///
    /// La base lo garantiza con el indice unico
    /// `ux_lotes_producto_sucursal_numero`; esta consulta existe para poder
    /// responder 409 con un mensaje que se entienda, en vez de dejar que reviente
    /// la restriccion y salga un 500.
    /// </summary>
    /// <param name="excluyendoId">
    /// Lote a ignorar en la comparacion. Es lo que permite guardar una edicion que
    /// no cambia el numero: sin esto, todo lote chocaria consigo mismo.
    /// </param>
    Task<bool> ExisteNumeroAsync(
        int productoId,
        int sucursalId,
        string numeroLote,
        int? excluyendoId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lotes CON SALDO que caducan dentro de una ventana de fechas, en orden
    /// FEFO.
    ///
    /// EL RANGO ENTRA POR PARAMETRO, ya resuelto en fechas, y no como un numero
    /// de dias. Es deliberado: calcular "hoy" dentro del repositorio lo ataria al
    /// reloj del servidor y haria imposible probar la consulta con un caso fijo.
    /// Quien traduce el umbral en dias a un par de fechas es el servicio, que es
    /// tambien quien conoce el valor configurado. El repositorio de tablero hace
    /// lo mismo, y por lo mismo.
    ///
    /// Los lotes que no caducan quedan siempre fuera: no tienen contra que
    /// compararse.
    /// </summary>
    /// <param name="sucursalId">Sede, o nulo para toda la red.</param>
    /// <param name="desde">
    /// Primer dia de la ventana, incluido. Nulo quita el limite inferior, que es
    /// como entran los lotes YA VENCIDOS.
    /// </param>
    /// <param name="hasta">Ultimo dia de la ventana, incluido.</param>
    /// <param name="limite">Tope de filas. Se acota entre 1 y 1000.</param>
    Task<IReadOnlyList<Lote>> ObtenerProximosAVencerAsync(
        int? sucursalId,
        DateOnly? desde,
        DateOnly hasta,
        int limite = 200,
        CancellationToken cancellationToken = default);

    // -------------------------------------------------------------------------
    // Escritura: preparan el cambio, no lo confirman
    // -------------------------------------------------------------------------

    /// <summary>Crea un lote nuevo. La cantidad no se fija aqui; nace vacio.</summary>
    void Agregar(Lote lote);

    /// <summary>Confirma en la base todo lo preparado. Devuelve las filas afectadas.</summary>
    Task<int> GuardarCambiosAsync(CancellationToken cancellationToken = default);
}
