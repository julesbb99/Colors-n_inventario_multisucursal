using Colorsin.Application.Compras.DTOs;
using Colorsin.Domain.Compras;

namespace Colorsin.Application.Compras.Services;

/// <summary>Operaciones del modulo de compras.</summary>
public interface IComprasService
{
    /// <summary>Proveedores del catalogo, con el conteo de productos que surte cada uno.</summary>
    Task<IReadOnlyList<ProveedorDto>> ObtenerProveedoresAsync(
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
}
