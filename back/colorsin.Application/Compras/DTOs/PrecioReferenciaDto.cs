namespace Colorsin.Application.Compras.DTOs;

/// <summary>
/// Lo que se sabe del precio de un producto con un proveedor, para ayudar a
/// digitar una orden nueva.
///
/// SON DOS COSAS DISTINTAS Y SE DEVUELVEN POR SEPARADO A PROPOSITO:
///
///   PrecioReferencia   lo pactado, de `producto_proveedor`. Es una lista, la
///                      mantiene la administracion y no cambia sola.
///   UltimaCompra       lo que de verdad se cobro la ultima vez, sacado del
///                      historico de ordenes. Puede no coincidir con la lista
///                      -por eso se muestran los dos- y justo la diferencia
///                      entre ambos es la informacion util.
///
/// Fundirlos en un solo numero obligaria a elegir cual gana, y quien digita la
/// orden perderia la unica senal de que el proveedor se salio de lo pactado.
/// </summary>
/// <param name="ProductoId">Producto consultado.</param>
/// <param name="ProductoNombre">Nombre del producto.</param>
/// <param name="ProveedorId">Proveedor consultado.</param>
/// <param name="ProveedorNombre">Nombre del proveedor.</param>
/// <param name="UnidadBaseSimbolo">
/// Unidad base del producto. ES LA UNIDAD EN LA QUE VIENE
/// <paramref name="PrecioReferencia"/>: la tabla `producto_proveedor` no tiene
/// columna de unidad, asi que su precio se interpreta por unidad base. Quien lo
/// muestre en galones o canecas tiene que convertirlo.
/// </param>
/// <param name="PrecioReferencia">
/// Precio de lista por unidad base. Nulo si ese proveedor no tiene el producto
/// en su lista, que NO es lo mismo que cero.
/// </param>
/// <param name="UltimaCompra">La ultima vez que se le compro. Nulo si nunca.</param>
/// <param name="CostoPromedio">
/// Lo que ha costado el producto de media, por unidad base, segun el inventario.
///
/// ES EL UNICO PRECIO QUE SIEMPRE ESTA. Los otros dos dependen de que exista un
/// acuerdo con ESE proveedor o una compra previa a ESE proveedor, y con un
/// catalogo donde cada producto cuelga de un solo proveedor eso deja la mayoria
/// de combinaciones en blanco. Este no: sale de lo que ya entro a bodega, venga
/// de quien venga.
///
/// Nulo solo si el producto no tiene existencias con costo en ninguna sede, que
/// es el caso de un producto recien dado de alta.
/// </param>
/// <param name="OtrosProveedores">
/// Lo que el MISMO producto tiene pactado con otros proveedores.
///
/// Sirve para comparar antes de pedir, y sobre todo para que la pantalla tenga
/// algo que ofrecer cuando este proveedor no lo lista. Va aparte y no mezclado
/// con <paramref name="PrecioReferencia"/> a proposito: no es lo pactado con
/// quien se le esta comprando, y presentarlo como si lo fuera seria mentir.
/// </param>
public sealed record PrecioReferenciaDto(
    int ProductoId,
    string ProductoNombre,
    int ProveedorId,
    string ProveedorNombre,
    string? UnidadBaseSimbolo,
    decimal? PrecioReferencia,
    UltimaCompraDto? UltimaCompra,
    decimal? CostoPromedio,
    IReadOnlyList<PrecioDeOtroProveedorDto> OtrosProveedores);

/// <summary>El precio de lista del mismo producto con otro proveedor.</summary>
/// <param name="ProveedorId">Quien lo surte.</param>
/// <param name="ProveedorNombre">Su nombre.</param>
/// <param name="Activo">
/// Si ese proveedor sigue en el catalogo. Un retirado se muestra igual -su
/// precio sigue siendo una referencia valida de lo que costaba- pero marcado,
/// para que nadie cuente con volver a pedirle.
/// </param>
/// <param name="PrecioReferencia">Precio pactado, por unidad base.</param>
public sealed record PrecioDeOtroProveedorDto(
    int ProveedorId,
    string ProveedorNombre,
    bool Activo,
    decimal PrecioReferencia);

/// <summary>
/// La ultima linea de orden de ese producto a ese proveedor.
///
/// El precio viaja CON SU UNIDAD, sin normalizar. Es deliberado: normalizarlo a
/// unidad base aqui mostraria "22.500 por litro" cuando en el papel decia
/// "85.172 por galon", y quien compara con la factura del proveedor no
/// reconoceria la cifra. La conversion se hace despues, y se ve.
/// </summary>
/// <param name="OrdenCompraId">Orden en la que se compro.</param>
/// <param name="Fecha">Cuando se hizo esa orden.</param>
/// <param name="Cantidad">Cuanto se pidio, en <paramref name="UnidadSimbolo"/>.</param>
/// <param name="UnidadId">Unidad en la que se pidio y se cotizo.</param>
/// <param name="UnidadSimbolo">Simbolo de esa unidad.</param>
/// <param name="PrecioUnitario">Precio por esa unidad. Nulo si la linea no lo traia.</param>
/// <param name="Descuento">Descuento aplicado, en porcentaje.</param>
/// <param name="Estado">Estado de aquella orden: un precio de una orden cancelada vale menos.</param>
public sealed record UltimaCompraDto(
    int OrdenCompraId,
    DateTime Fecha,
    decimal? Cantidad,
    int UnidadId,
    string? UnidadSimbolo,
    decimal? PrecioUnitario,
    decimal Descuento,
    string Estado);

/// <summary>Alta o cambio de un proveedor. Solo Administrador General.</summary>
/// <param name="Nombre">Razon social. Obligatoria y unica.</param>
/// <param name="Contacto">Persona de contacto. Opcional.</param>
/// <param name="Telefono">Telefono. Obligatorio.</param>
public sealed record GuardarProveedorDto(
    string Nombre,
    string? Contacto,
    string Telefono);

/// <summary>
/// Fija el precio de lista de un producto para un proveedor.
///
/// Un precio nulo BORRA la entrada de la lista: es la forma de decir "este
/// proveedor ya no surte este producto" sin tocar las ordenes historicas.
/// </summary>
/// <param name="PrecioReferencia">Precio por unidad base, o nulo para quitarlo.</param>
public sealed record GuardarPrecioReferenciaDto(decimal? PrecioReferencia);

/// <summary>Desenlace de una operacion sobre un proveedor o su lista de precios.</summary>
public sealed record ResultadoProveedor(
    bool Exito,
    ErrorCompra Error,
    string Mensaje,
    ProveedorDto? Proveedor = null)
{
    public static ResultadoProveedor Ok(ProveedorDto proveedor, string mensaje) =>
        new(true, ErrorCompra.Ninguno, mensaje, proveedor);

    public static ResultadoProveedor Fallo(ErrorCompra error, string mensaje) =>
        new(false, error, mensaje);
}
