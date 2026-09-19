namespace Colorsin.Application.Inventario.DTOs;

/// <summary>
/// Alta de una existencia: la pareja (sede, producto) empieza a manejarse ahi.
///
/// NO LLEVA CANTIDAD, Y ES DELIBERADO. El saldo solo se mueve por el libro
/// mayor -un ingreso manual, una recepcion de compra, un traslado- porque cada
/// una de esas vias deja un asiento que explica de donde salio la mercancia.
/// Permitir sembrar aqui un saldo inicial abriria la unica puerta para cambiar
/// el inventario sin rastro, que es justo lo que el libro mayor existe para
/// impedir. Nace en cero y se llena por donde corresponde.
/// </summary>
/// <param name="SucursalId">Sede que empieza a manejar el producto.</param>
/// <param name="ProductoId">Producto del catalogo.</param>
/// <param name="StockMinimo">
/// Umbral de reposicion, en la unidad base del producto. Cero es valido y
/// significa "no avisar": la alerta salta con <c>cantidad &lt;= minimo</c>, asi
/// que con minimo cero solo avisa cuando llega a cero.
/// </param>
public sealed record CrearExistenciaDto(
    int SucursalId,
    int ProductoId,
    decimal StockMinimo);

/// <summary>
/// Edicion de una existencia. Solo el minimo: la cantidad la mueve el libro
/// mayor y el costo promedio lo recalcula cada entrada.
/// </summary>
public sealed record ActualizarExistenciaDto(decimal StockMinimo);

/// <summary>Que salio mal al crear, editar o dar de baja una existencia.</summary>
public enum ErrorExistencia
{
    /// <summary>Sin error.</summary>
    Ninguno = 0,

    /// <summary>El producto no existe en el catalogo.</summary>
    ProductoNoEncontrado,

    /// <summary>La sede no existe.</summary>
    SucursalNoEncontrada,

    /// <summary>La existencia que se quiere editar o dar de baja no existe.</summary>
    ExistenciaNoEncontrada,

    /// <summary>
    /// Esa sede ya maneja ese producto y la fila esta ACTIVA. Es un 409: la
    /// peticion esta bien formada, lo que no admite la operacion es el estado
    /// actual de los datos.
    /// </summary>
    ExistenciaDuplicada,

    /// <summary>El minimo llego negativo.</summary>
    StockMinimoInvalido,

    /// <summary>
    /// Se intento dar de baja una existencia que todavia tiene saldo.
    ///
    /// Se niega a proposito: la suma de las existencias activas es lo que la
    /// empresa cree tener, y esconder una fila con mercancia dentro haria que
    /// esa suma dejara de cuadrar con lo que hay en la bodega, sin que ningun
    /// asiento lo explique. Primero se saca el saldo -traslado, venta o ajuste-
    /// y entonces la baja es limpia.
    /// </summary>
    TieneSaldo,

    /// <summary>Se pidio dar de baja algo ya dado de baja, o reactivar algo ya activo.</summary>
    EstadoSinCambio
}

/// <summary>
/// Desenlace de una operacion sobre una existencia.
///
/// Las reglas de negocio se devuelven, no se lanzan, igual que en
/// <see cref="ResultadoLote"/>: que una sede ya maneje un producto es un
/// desenlace corriente, no una condicion excepcional. Las de AUTORIZACION si
/// lanzan, porque no son parte del flujo normal y no deben poder confundirse con
/// un fallo de datos.
/// </summary>
public sealed record ResultadoExistencia(
    bool Exito,
    ErrorExistencia Error,
    string Mensaje,
    InventarioSucursalDto? Existencia = null)
{
    public static ResultadoExistencia Ok(InventarioSucursalDto existencia, string mensaje) =>
        new(true, ErrorExistencia.Ninguno, mensaje, existencia);

    public static ResultadoExistencia Fallo(ErrorExistencia error, string mensaje) =>
        new(false, error, mensaje);
}
