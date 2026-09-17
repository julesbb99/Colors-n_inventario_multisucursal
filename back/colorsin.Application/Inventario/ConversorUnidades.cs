namespace Colorsin.Application.Inventario;

/// <summary>Como termino una conversion a unidad base.</summary>
public enum EstadoConversion
{
    /// <summary>Convertida correctamente.</summary>
    Ok,

    /// <summary>
    /// Alguna de las dos unidades no tiene factor a litros, asi que no hay
    /// conversion posible entre ellas.
    /// </summary>
    SinFactor,

    /// <summary>
    /// La cantidad convertida se redondea a cero con los decimales que guarda
    /// la base.
    /// </summary>
    SeRedondeaACero
}

/// <summary>Resultado de convertir una cantidad a la unidad base de un producto.</summary>
public readonly record struct Conversion(EstadoConversion Estado, decimal CantidadBase)
{
    public bool Exito => Estado == EstadoConversion.Ok;
}

/// <summary>
/// Paso de una cantidad a la unidad base de un producto.
///
/// Vive aparte de los servicios porque Inventario y Compras hacen exactamente
/// la misma cuenta: un ingreso manual de 10 galones y la recepcion de una orden
/// de 10 galones tienen que dar el mismo numero. Con la formula escrita dos
/// veces, basta con que alguien corrija una para que dejen de coincidir y el
/// stock se descuadre sin que nada falle.
///
/// Es aritmetica pura, sin base de datos: los factores entran por parametro.
/// </summary>
public static class ConversorUnidades
{
    /// <summary>
    /// Decimales de `cantidad_base` en la base: DECIMAL(14,4).
    ///
    /// Se redondea en C# ANTES de guardar, a proposito. Si se dejara redondear
    /// a MySQL, el saldo calculado en memoria y el almacenado se separarian en
    /// el cuarto decimal, y ese descuadre se acumularia movimiento tras
    /// movimiento hasta que el libro mayor dejara de reconstruir el saldo.
    /// </summary>
    public const int DecimalesCantidadBase = 4;

    /// <summary>
    /// Convierte <paramref name="cantidad"/> desde la unidad
    /// <paramref name="unidadId"/> a la unidad <paramref name="unidadBaseId"/>.
    ///
    /// La formula NO es "multiplicar por el factor a litros". Eso solo vale si
    /// la unidad base del producto es el litro. El factor de cada unidad dice
    /// cuantos litros vale una unidad suya, de modo que para ir de una a otra
    /// hay que pasar por litros en ambos sentidos:
    ///
    ///     cantidadBase = cantidad x factor(unidad) / factor(unidadBase)
    ///
    /// Con unidad base Litro (factor 1) se reduce al caso simple, pero si
    /// manana un producto se lleva en galones la formula sigue valiendo.
    /// </summary>
    /// <param name="cantidad">Cantidad a convertir. Se asume ya validada como mayor que cero.</param>
    /// <param name="unidadId">Unidad en que viene la cantidad.</param>
    /// <param name="factorUnidad">Litros por unidad de <paramref name="unidadId"/>. Nulo si no es de volumen.</param>
    /// <param name="unidadBaseId">Unidad base del producto.</param>
    /// <param name="factorUnidadBase">Litros por unidad base. Nulo si no es de volumen.</param>
    public static Conversion ABaseDelProducto(
        decimal cantidad,
        int unidadId,
        decimal? factorUnidad,
        int unidadBaseId,
        decimal? factorUnidadBase)
    {
        decimal cantidadBase;

        if (unidadId == unidadBaseId)
        {
            // Misma unidad: no hay nada que convertir. Este atajo ademas permite
            // mover productos en unidades sin factor a litros, como el
            // kilogramo, mientras se registren en su propia unidad.
            cantidadBase = cantidad;
        }
        else if (factorUnidad is not > 0m || factorUnidadBase is not > 0m)
        {
            // Un factor nulo significa que la unidad no es de volumen (el
            // kilogramo). Entre una de peso y una de volumen no hay conversion
            // posible sin conocer la densidad del producto, que el sistema no
            // guarda: mejor rechazarlo que inventar un numero.
            return new Conversion(EstadoConversion.SinFactor, 0m);
        }
        else
        {
            cantidadBase = cantidad * factorUnidad.Value / factorUnidadBase.Value;
        }

        // MySQL redondea DECIMAL al medio hacia arriba; se replica aqui para que
        // el valor en memoria sea exactamente el que quedara almacenado.
        cantidadBase = Math.Round(
            cantidadBase, DecimalesCantidadBase, MidpointRounding.AwayFromZero);

        // Los CHECK `chk_movinv_cantidad_base` y `chk_ocd_cantidad` exigen > 0.
        // Una cantidad tan pequena que se redondea a cero se rechaza aqui, con
        // un mensaje util, en vez de dejar que MySQL responda con el error 3819.
        return cantidadBase <= 0m
            ? new Conversion(EstadoConversion.SeRedondeaACero, 0m)
            : new Conversion(EstadoConversion.Ok, cantidadBase);
    }
}
