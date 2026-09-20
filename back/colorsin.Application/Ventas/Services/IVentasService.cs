using Colorsin.Application.Ventas.DTOs;

namespace Colorsin.Application.Ventas.Services;

/// <summary>Operaciones del modulo de ventas.</summary>
public interface IVentasService
{
    /// <summary>Clientes del catalogo, ordenados por razon social.</summary>
    Task<IReadOnlyList<ClienteDto>> ObtenerClientesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>El cliente con ese id, o <c>null</c> si no existe.</summary>
    Task<ClienteDto?> ObtenerClientePorIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// El cliente con ese documento (cedula o NIT), o <c>null</c>. Sirve para
    /// buscarlo en el mostrador, donde se tiene el documento y no el id.
    /// </summary>
    Task<ClienteDto?> ObtenerClientePorDocumentoAsync(
        string documento,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Da de alta un cliente y devuelve el creado, ya con su id.
    ///
    /// ABIERTO A CUALQUIER ROL: en el mostrador aparece un cliente nuevo a
    /// diario, y no poder registrarlo significa no poder facturarle.
    ///
    /// Lanza <see cref="ClienteDuplicadoException"/> si el documento ya existe
    /// -con el id del que ya esta, para poder ofrecerlo- y
    /// <see cref="VentaInvalidaException"/> si falta la razon social, el
    /// documento o el tipo de persona no es 'Natural' ni 'Juridica'.
    /// </summary>
    Task<ClienteDto> CrearClienteAsync(
        CrearClienteDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A cuanto se vende un producto: lo fijado, lo que se cobro la ultima vez y
    /// lo que ha costado en bodega. Ver <see cref="PrecioVentaDto"/>.
    ///
    /// <paramref name="sucursalId"/> acota la "ultima venta" a una sede. Nulo
    /// responde por toda la red.
    ///
    /// Lanza <see cref="ReferenciaVentaNoEncontradaException"/> si el producto
    /// no existe.
    /// </summary>
    Task<PrecioVentaDto> ObtenerPrecioVentaAsync(
        int productoId,
        int? sucursalId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fija el precio de venta de un producto, POR UNIDAD BASE, o lo quita si
    /// llega nulo.
    ///
    /// Es una decision de RED, no de sede -las tres heredan la misma lista-, asi
    /// que el endpoint la reserva al Administrador General.
    ///
    /// Rechaza el cero y los negativos con
    /// <see cref="VentaInvalidaException"/>: para dejar de tener precio esta el
    /// nulo, y un cero haria que la pantalla rellenara las ventas regaladas.
    /// </summary>
    Task<PrecioVentaDto> FijarPrecioVentaAsync(
        int productoId,
        GuardarPrecioVentaDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>Ventas, de la mas reciente a la mas antigua. Sin detalle.</summary>
    Task<IReadOnlyList<VentaDto>> ObtenerVentasAsync(
        int? sucursalId = null,
        int? clienteId = null,
        DateTime? desde = null,
        DateTime? hasta = null,
        int limite = 100,
        CancellationToken cancellationToken = default);

    /// <summary>La venta con ese id y su detalle completo, o <c>null</c>.</summary>
    Task<VentaDto?> ObtenerVentaPorIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra una venta ya concretada y descuenta el stock.
    ///
    /// En una sola transaccion: valida que la sede tenga existencias, crea la
    /// venta con su detalle, descuenta el saldo de la sede, descuenta los lotes
    /// (por FEFO o por el lote que indique cada linea), anexa un movimiento de
    /// Retiro/Venta al libro mayor por cada lote consumido y deja el evento de
    /// auditoria. O queda todo, o no queda nada.
    ///
    /// A diferencia de Inventario y Compras, las reglas de negocio de este
    /// modulo se LANZAN como excepcion en vez de devolverse: hay que envolver
    /// la llamada en try/catch sobre <see cref="VentaException"/>. La de mayor
    /// interes es <see cref="StockInsuficienteException"/>, que lleva el
    /// disponible y el solicitado como propiedades.
    /// </summary>
    /// <param name="peticion">Cliente, sede y lineas de la venta.</param>
    /// <param name="usuarioId">
    /// Quien registra la venta. Va como PARAMETRO y no dentro de
    /// <paramref name="peticion"/> porque el DTO lo escribe el cliente: aqui debe
    /// llegar <c>IUsuarioContexto.UsuarioIdRequerido()</c>, que sale del token.
    /// De este id cuelgan la comision, el cuadre de caja y la responsabilidad
    /// sobre el stock que salio.
    /// </param>
    Task<VentaRegistradaDto> RegistrarVentaAsync(
        CrearVentaDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default);
}
