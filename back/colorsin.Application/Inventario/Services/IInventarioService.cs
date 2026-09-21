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
    /// <param name="incluirInactivas">
    /// <c>false</c> por defecto. En <c>true</c> devuelve tambien las dadas de
    /// baja, que es lo que necesita la pestana desde la que se reactivan.
    /// </param>
    Task<IReadOnlyList<InventarioSucursalDto>> ObtenerExistenciasAsync(
        int? sucursalId = null,
        bool incluirInactivas = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Una existencia por su id, activa o no, o <c>null</c> si no existe.
    ///
    /// Existe sobre todo para el endpoint: necesita saber de QUE sede es antes
    /// de decidir si quien pide puede modificarla.
    /// </summary>
    Task<InventarioSucursalDto?> ObtenerExistenciaPorIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    // -------------------------------------------------------------------------
    // Altas, bajas y edicion de existencias
    //
    // LA AUTORIZACION NO ESTA AQUI. Estos metodos no comprueban sobre que sede
    // puede operar quien llama: eso lo hace el endpoint con
    // IUsuarioContexto.ExigirAccesoASucursal, igual que el resto del modulo, y
    // por el mismo motivo -el servicio no depende del contexto HTTP-. Quien
    // anada otra via de entrada tiene que acordarse.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Empieza a manejar un producto en una sede. Nace con saldo CERO: la
    /// mercancia entra despues por el libro mayor.
    ///
    /// Si la pareja ya existe pero esta dada de baja, la REACTIVA en vez de
    /// fallar: el indice unico (sucursal, producto) impide crear otra fila, asi
    /// que negarse dejaria a la persona sin salida desde la interfaz.
    /// </summary>
    Task<ResultadoExistencia> CrearExistenciaAsync(
        CrearExistenciaDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>Cambia el minimo de reposicion. Ni la cantidad ni el costo.</summary>
    Task<ResultadoExistencia> ActualizarExistenciaAsync(
        int id,
        ActualizarExistenciaDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Baja logica: la existencia deja de listarse y de alertar, pero conserva
    /// saldo, lotes e historia. Se niega si todavia tiene mercancia.
    /// </summary>
    Task<ResultadoExistencia> DesactivarExistenciaAsync(
        int id,
        int usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>Deshace la baja. Vuelve al listado con el saldo que tenia.</summary>
    Task<ResultadoExistencia> ReactivarExistenciaAsync(
        int id,
        int usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lotes disponibles de un producto en una sede, en orden FEFO: el primero
    /// de la lista es el que se debe despachar.
    /// </summary>
    Task<IReadOnlyList<LoteDto>> ObtenerLotesPorVencimientoAsync(
        int sucursalId,
        int productoId,
        CancellationToken cancellationToken = default);

    // =========================================================================
    // LOTES: consulta, alta y correccion
    //
    // NINGUNO DE ESTOS METODOS CAMBIA CANTIDADES. Un lote se crea vacio y su
    // saldo se mueve solo por el camino que ademas actualiza el consolidado de
    // la sede y deja fila en el libro mayor: un movimiento de inventario, una
    // recepcion de compra, una venta o un traslado.
    // =========================================================================

    /// <summary>
    /// Lotes que cumplen los filtros, en orden FEFO. Los filtros nulos no se
    /// aplican.
    /// </summary>
    /// <param name="sucursalId">Sede, o nulo para toda la red.</param>
    /// <param name="productoId">Producto, o nulo para todos.</param>
    /// <param name="soloConSaldo">
    /// Deja fuera los agotados. Falso por defecto, para que un lote recien creado
    /// -que esta en cero- se vea.
    /// </param>
    /// <param name="limite">Tope de filas, de 1 a 1000.</param>
    Task<IReadOnlyList<LoteDto>> ObtenerLotesAsync(
        int? sucursalId = null,
        int? productoId = null,
        bool soloConSaldo = false,
        int limite = 200,
        CancellationToken cancellationToken = default);

    /// <summary>El lote con ese id, o <c>null</c> si no existe.</summary>
    Task<LoteDto?> ObtenerLotePorIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lotes CON SALDO que caducan dentro del umbral.
    ///
    /// EL RANGO llega hasta <c>hoy + diasUmbral</c> y por defecto NO tiene limite
    /// inferior, asi que los que ya caducaron entran. Los lotes que no caducan y
    /// los agotados quedan fuera.
    /// </summary>
    /// <param name="sucursalId">Sede, o nulo para toda la red.</param>
    /// <param name="diasUmbral">
    /// Dias hacia adelante. NULO usa el umbral configurado en
    /// <c>AlertasInventario:DiasUmbralVencimiento</c>, que es el caso normal: el
    /// valor existe justamente para no tener que repetirlo en cada llamada. Un
    /// valor fuera de [1, 365] se acota al limite mas cercano.
    /// </param>
    /// <param name="incluirVencidos">
    /// Si entran tambien los lotes que YA caducaron y todavia tienen saldo.
    ///
    /// VERDADERO POR DEFECTO, o sea que el rango no tiene limite inferior. Un
    /// lote vencido con existencias es MAS urgente que uno que vence en tres
    /// semanas: no se puede despachar y lo que toca es darlo de baja. Dejarlo
    /// fuera de la lista de alertas por tener la fecha "antes de la ventana"
    /// esconderia justamente lo que ya se salio de control, y ademas haria que
    /// esta consulta contradijera al tablero, que si los incluye siempre; ver
    /// <c>MetricasInventarioDto.LotesProximosAVencer</c>.
    ///
    /// En falso, el rango queda en
    /// <c>hoy &lt;= fecha_vencimiento &lt;= hoy + diasUmbral</c>: sirve para la
    /// pregunta acotada de "que se me vence de aqui en adelante", separada de lo
    /// que ya se perdio.
    /// </param>
    /// <param name="limite">Tope de filas, de 1 a 1000.</param>
    Task<IReadOnlyList<LoteDto>> ObtenerLotesProximosAVencerAsync(
        int? sucursalId = null,
        int? diasUmbral = null,
        bool incluirVencidos = true,
        int limite = 200,
        CancellationToken cancellationToken = default);

    // AQUI ESTABA CrearLoteAsync, que abria un lote vacio a mano. Se quito junto
    // con su endpoint, y el motivo es de negocio, no de codigo:
    //
    // EL NUMERO DE LOTE Y SU VENCIMIENTO LOS PONE EL FABRICANTE. Llegan impresos
    // en el envase y no se conocen hasta que el camion descarga. Teclearlos por
    // adelantado solo producia lotes inventados que no coincidian con ninguna
    // caja, o lotes vacios esperando mercancia que quiza llegaba con otro numero.
    //
    // Los lotes nacen donde siempre nacieron de verdad: al recibir una compra
    // -con el numero de la factura- y al recibir un traslado, que recrea en el
    // destino el que salio del origen. Las dos pasan por RegistrarMovimientoAsync
    // y por el servicio de traslados, no por aqui.
    //
    // ActualizarLoteAsync SI sigue: corregir una fecha mal leida del envase es
    // otra cosa que inventarla.

    /// <summary>
    /// Corrige el numero o la caducidad de un lote existente. No toca cantidades
    /// ni mueve el lote de sede; ver <see cref="ActualizarLoteDto"/>.
    ///
    /// Es un reemplazo completo del estado editable: lo que no venga en el cuerpo
    /// se borra, no se conserva.
    /// </summary>
    /// <param name="id">Lote a corregir.</param>
    /// <param name="peticion">Como debe quedar.</param>
    /// <param name="usuarioId">Responsable, para la auditoria. Sale del token.</param>
    Task<ResultadoLote> ActualizarLoteAsync(
        int id,
        ActualizarLoteDto peticion,
        int usuarioId,
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
