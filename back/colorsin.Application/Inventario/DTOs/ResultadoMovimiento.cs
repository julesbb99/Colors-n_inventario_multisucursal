namespace Colorsin.Application.Inventario.DTOs;

/// <summary>
/// Por que se rechazo un movimiento.
///
/// Es un enum y no un string suelto para que la capa HTTP pueda traducir cada
/// caso a su codigo (404 para lo que no existe, 409 para el stock insuficiente,
/// 400 para lo mal digitado) sin comparar mensajes de texto.
/// </summary>
public enum ErrorMovimiento
{
    /// <summary>Sin error.</summary>
    Ninguno = 0,

    /// <summary>La cantidad venia en cero o negativa.</summary>
    CantidadInvalida,

    /// <summary>El producto no existe.</summary>
    ProductoNoEncontrado,

    /// <summary>El producto no tiene unidad base definida, asi que no hay a que convertir.</summary>
    ProductoSinUnidadBase,

    /// <summary>La unidad en que se digito la cantidad no existe.</summary>
    UnidadNoEncontrada,

    /// <summary>
    /// Las unidades no son convertibles entre si: alguna no tiene factor a
    /// litros. Pasa al mezclar una unidad de peso con una de volumen.
    /// </summary>
    ConversionImposible,

    /// <summary>La conversion dio un valor que se redondea a cero en la base.</summary>
    CantidadBaseCero,

    /// <summary>No hay fila de saldo para esa pareja (sede, producto).</summary>
    SaldoNoEncontrado,

    /// <summary>El retiro supera lo disponible en la sede.</summary>
    StockInsuficiente,

    /// <summary>El lote indicado no existe.</summary>
    LoteNoEncontrado,

    /// <summary>El lote existe pero es de otra sede o de otro producto.</summary>
    LoteNoCorresponde,

    /// <summary>El retiro supera lo disponible en ese lote.</summary>
    StockLoteInsuficiente
}

/// <summary>
/// Desenlace de un intento de registrar un movimiento.
///
/// Las reglas de negocio se devuelven, no se lanzan: que un retiro supere el
/// stock es un desenlace normal del dia a dia, no una condicion excepcional, y
/// usar excepciones para eso obliga a envolver cada llamada en try/catch.
/// Las excepciones quedan para lo que si es excepcional: que la base no
/// responda.
/// </summary>
/// <param name="Exito">Si el movimiento quedo registrado.</param>
/// <param name="Error">Motivo del rechazo. <see cref="ErrorMovimiento.Ninguno"/> si hubo exito.</param>
/// <param name="Mensaje">Explicacion legible, con los numeros concretos del caso.</param>
/// <param name="MovimientoId">Id de la fila creada en el libro mayor.</param>
/// <param name="CantidadBaseAplicada">Cantidad ya convertida a unidad base.</param>
/// <param name="SaldoResultante">Saldo de la sede despues del movimiento.</param>
public sealed record ResultadoMovimiento(
    bool Exito,
    ErrorMovimiento Error,
    string Mensaje,
    int? MovimientoId = null,
    decimal? CantidadBaseAplicada = null,
    decimal? SaldoResultante = null)
{
    public static ResultadoMovimiento Fallo(ErrorMovimiento error, string mensaje) =>
        new(false, error, mensaje);

    public static ResultadoMovimiento Ok(
        int movimientoId,
        decimal cantidadBase,
        decimal saldoResultante) =>
        new(true, ErrorMovimiento.Ninguno, "Movimiento registrado.",
            movimientoId, cantidadBase, saldoResultante);
}
