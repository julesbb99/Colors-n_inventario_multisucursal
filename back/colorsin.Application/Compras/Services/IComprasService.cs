using Colorsin.Application.Compras.DTOs;
using Colorsin.Domain.Compras;

namespace Colorsin.Application.Compras.Services;

/// <summary>Operaciones del modulo de compras.</summary>
public interface IComprasService
{
    /// <summary>Proveedores del catalogo, con el conteo de productos que surte cada uno.</summary>
    /// <param name="incluirInactivos">
    /// <c>false</c> por defecto: los retirados no salen, porque quien crea una
    /// orden no debe poder elegirlos. En <c>true</c> los incluye, para la
    /// pantalla de administracion desde la que se reactivan.
    /// </param>
    Task<IReadOnlyList<ProveedorDto>> ObtenerProveedoresAsync(
        bool incluirInactivos,
        CancellationToken cancellationToken = default);

    /// <summary>El proveedor con ese id, o <c>null</c> si no existe.</summary>
    Task<ProveedorDto?> ObtenerProveedorPorIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ordenes de compra, de la mas reciente a la mas antigua. Sin detalle:
    /// para verlo, consultar la orden por id.
    /// </summary>
    Task<IReadOnlyList<OrdenCompraDto>> ObtenerOrdenesAsync(
        int? sucursalId = null,
        int? proveedorId = null,
        EstadoOrdenCompra? estado = null,
        int limite = 100,
        CancellationToken cancellationToken = default);

    /// <summary>La orden con ese id y su detalle completo, o <c>null</c>.</summary>
    Task<OrdenCompraDto?> ObtenerOrdenPorIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea una orden de compra en estado 'Pendiente'.
    ///
    /// No mueve stock: una orden es una intencion de compra, y las existencias
    /// solo cambian cuando la mercancia llega fisicamente. Para eso esta
    /// <see cref="ConfirmarRecepcionAsync"/>.
    ///
    /// No lanza excepciones por reglas de negocio. Revisa
    /// <see cref="ResultadoOrdenCompra.Exito"/>.
    /// </summary>
    /// <param name="peticion">Proveedor, sede y lineas de la orden.</param>
    /// <param name="usuarioId">
    /// Quien crea la orden. Va como PARAMETRO y no dentro de
    /// <paramref name="peticion"/> porque el DTO es lo que se deserializa del
    /// cuerpo de la peticion HTTP: aqui debe llegar
    /// <c>IUsuarioContexto.UsuarioIdRequerido()</c>, que sale del token.
    /// </param>
    Task<ResultadoOrdenCompra> CrearOrdenAsync(
        CrearOrdenCompraDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirma que la mercancia llego y la ingresa al stock.
    ///
    /// En una sola transaccion: pasa la orden a 'Recibida', convierte cada
    /// linea a la unidad base del producto, sube el saldo de la sede, anexa un
    /// movimiento de Ingreso/Compra al libro mayor, registra o engrosa los
    /// lotes recibidos y deja el evento de auditoria. O queda todo, o nada.
    ///
    /// Es la unica operacion de compras que toca inventario.
    ///
    /// No lanza excepciones por reglas de negocio. Revisa
    /// <see cref="ResultadoRecepcion.Exito"/>.
    /// </summary>
    /// <param name="peticion">Orden que se recibe y que llego en esta entrega.</param>
    /// <param name="usuarioId">
    /// Quien recibe. Del token, no del DTO: queda en cada movimiento de
    /// inventario, y esta es la operacion que mueve stock de verdad.
    /// </param>
    Task<ResultadoRecepcion> ConfirmarRecepcionAsync(
        ConfirmarRecepcionDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default);

    // =========================================================================
    // EDICION Y RETIRO DE ORDENES
    //
    // LAS DOS SOLO VALEN EN 'Pendiente'. Una orden deja de ser un borrador en
    // cuanto se confirma o entra mercancia: a partir de ahi hay un compromiso
    // con el proveedor y, si hubo recepcion, stock movido y asientos en el libro
    // mayor que la citan. Cambiarla entonces seria reescribir el papel con el
    // que se recibio.
    //
    // LA AUTORIZACION NO ESTA AQUI: la sede la comprueba el endpoint, como en el
    // resto del modulo.
    // =========================================================================

    /// <summary>
    /// Reemplaza el contenido de una orden en 'Pendiente': proveedor, plazo y
    /// lineas. La sede NO se cambia -mover una orden de bodega es otra orden- ni
    /// el usuario que la creo.
    ///
    /// Es un reemplazo completo, no un parche: las lineas que llegan sustituyen
    /// a las que habia. Es lo que corresponde a un PUT y lo que evita tener que
    /// inventar identidades estables para lineas que aun no existen en ningun
    /// papel.
    /// </summary>
    Task<ResultadoOrdenCompra> ActualizarOrdenAsync(
        int id,
        CrearOrdenCompraDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retira una orden en 'Pendiente' pasandola a 'Cancelada'.
    ///
    /// NO LA BORRA. El estado 'Cancelada' ya existia en el esquema para esto, y
    /// conservar la fila es lo que permite responder despues a "quien pidio esto
    /// y por que no llego". La interfaz la esconde del listado del dia, que es
    /// el efecto que se busca al retirarla.
    /// </summary>
    Task<ResultadoOrdenCompra> CancelarOrdenAsync(
        int id,
        int usuarioId,
        CancellationToken cancellationToken = default);

    // =========================================================================
    // PRECIOS DE REFERENCIA
    // =========================================================================

    /// <summary>
    /// Lo que se sabe del precio de un producto con un proveedor: el de lista y
    /// el de la ultima compra real. Ver <see cref="PrecioReferenciaDto"/>.
    ///
    /// Devuelve el DTO aunque no haya ni lista ni historico -con los dos campos
    /// nulos- porque "no hay precio" es una respuesta legitima que la pantalla
    /// tiene que poder mostrar. Solo da <c>null</c> si el producto o el
    /// proveedor no existen.
    /// </summary>
    Task<PrecioReferenciaDto?> ObtenerPrecioReferenciaAsync(
        int productoId,
        int proveedorId,
        CancellationToken cancellationToken = default);

    // =========================================================================
    // PROVEEDORES: alta, edicion y retiro. Solo Administrador General.
    // =========================================================================

    /// <summary>Da de alta un proveedor. El nombre es unico.</summary>
    Task<ResultadoProveedor> CrearProveedorAsync(
        GuardarProveedorDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>Cambia nombre, contacto o telefono.</summary>
    Task<ResultadoProveedor> ActualizarProveedorAsync(
        int id,
        GuardarProveedorDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Baja logica: deja de ofrecerse en ordenes nuevas. Conserva sus ordenes
    /// historicas y su lista de precios. Ver la migracion 13.
    /// </summary>
    Task<ResultadoProveedor> DesactivarProveedorAsync(
        int id,
        int usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>Deshace el retiro.</summary>
    Task<ResultadoProveedor> ReactivarProveedorAsync(
        int id,
        int usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fija el precio de lista de un producto para un proveedor, en la UNIDAD
    /// BASE del producto. Un precio nulo quita la entrada de la lista.
    /// </summary>
    Task<ResultadoProveedor> GuardarPrecioReferenciaAsync(
        int proveedorId,
        int productoId,
        GuardarPrecioReferenciaDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default);
}
