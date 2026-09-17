using System.Globalization;

namespace Colorsin.Application.Ventas;

/// <summary>
/// Formato de numeros para los mensajes de excepcion.
///
/// Con punto decimal siempre: sin esto el mismo error saldria con coma en un
/// servidor con configuracion regional colombiana y con punto en otro, y los
/// mensajes quedarian con dos formatos segun donde se ejecute la API.
/// </summary>
internal static class FormatoVenta
{
    internal static string Num(decimal valor) =>
        valor.ToString("0.####", CultureInfo.InvariantCulture);
}

/// <summary>
/// Base de las reglas de negocio de ventas que interrumpen el flujo.
///
/// OJO, esto se aparta del resto del backend: Inventario y Compras devuelven un
/// objeto de resultado (<c>ResultadoMovimiento</c>, <c>ResultadoRecepcion</c>)
/// porque un retiro que excede el stock es un desenlace corriente del dia a
/// dia, no una condicion excepcional. Ventas lanza excepciones por peticion
/// expresa.
///
/// La consecuencia practica: quien llame a <c>VentasService</c> necesita
/// try/catch, mientras que con los otros dos modulos basta con revisar una
/// bandera. Si conviene unificar, lo natural seria mover Ventas al patron de
/// resultado y no al contrario, porque esa es la forma que ya tienen los otros
/// dos modulos.
/// </summary>
public abstract class VentaException : Exception
{
    protected VentaException(string mensaje) : base(mensaje) { }
}

/// <summary>
/// La sede no tiene cantidad suficiente de un producto para cubrir la venta.
///
/// Lleva los numeros concretos como propiedades, no solo dentro del mensaje,
/// para que la capa HTTP pueda armar su respuesta sin tener que interpretar
/// texto.
/// </summary>
public sealed class StockInsuficienteException : VentaException
{
    public StockInsuficienteException(
        int sucursalId,
        int productoId,
        string productoNombre,
        decimal disponible,
        decimal solicitado,
        string unidad)
        : base($"Stock insuficiente de '{productoNombre}' en la sede {sucursalId}: " +
               $"hay {FormatoVenta.Num(disponible)} {unidad} y la venta pide " +
               $"{FormatoVenta.Num(solicitado)} {unidad}.")
    {
        SucursalId = sucursalId;
        ProductoId = productoId;
        ProductoNombre = productoNombre;
        Disponible = disponible;
        Solicitado = solicitado;
        Unidad = unidad;
    }

    public int SucursalId { get; }
    public int ProductoId { get; }
    public string ProductoNombre { get; }

    /// <summary>Saldo de la sede en unidad base, al momento de validar.</summary>
    public decimal Disponible { get; }

    /// <summary>Lo que pide la venta en unidad base, sumando todas sus lineas.</summary>
    public decimal Solicitado { get; }

    /// <summary>Simbolo de la unidad base del producto.</summary>
    public string Unidad { get; }
}

/// <summary>
/// El lote indicado en una linea no tiene cantidad suficiente.
///
/// Es distinto de <see cref="StockInsuficienteException"/>: la sede si tiene el
/// producto, pero no en ese lote concreto. La salida es despachar de otro lote
/// o dejar que FEFO elija.
/// </summary>
public sealed class StockLoteInsuficienteException : VentaException
{
    public StockLoteInsuficienteException(
        int loteId,
        string numeroLote,
        decimal disponible,
        decimal solicitado)
        : base($"El lote '{numeroLote}' tiene {FormatoVenta.Num(disponible)} " +
               $"y la venta pide {FormatoVenta.Num(solicitado)}.")
    {
        LoteId = loteId;
        NumeroLote = numeroLote;
        Disponible = disponible;
        Solicitado = solicitado;
    }

    public int LoteId { get; }
    public string NumeroLote { get; }
    public decimal Disponible { get; }
    public decimal Solicitado { get; }
}

/// <summary>Una venta mal formada: sin lineas, con cantidades o descuentos invalidos.</summary>
public sealed class VentaInvalidaException : VentaException
{
    public VentaInvalidaException(string mensaje) : base(mensaje) { }
}

/// <summary>Algo que la venta referencia no existe: cliente, producto, unidad o lote.</summary>
public sealed class ReferenciaVentaNoEncontradaException : VentaException
{
    public ReferenciaVentaNoEncontradaException(string mensaje) : base(mensaje) { }
}

/// <summary>
/// No hay forma de convertir la cantidad a la unidad base del producto: el
/// producto no declara unidad base, o las unidades no son convertibles entre si.
/// </summary>
public sealed class ConversionVentaImposibleException : VentaException
{
    public ConversionVentaImposibleException(string mensaje) : base(mensaje) { }
}
