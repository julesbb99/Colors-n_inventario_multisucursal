namespace Colorsin.Application.Inventario;

/// <summary>
/// Cuantos dias antes de la caducidad se empieza a avisar.
///
/// POR QUE ES CONFIGURACION Y NO UNA CONSTANTE. El umbral no es una verdad del
/// dominio sino una decision del negocio, y depende de cosas que cambian: lo que
/// tarda un traslado entre sedes, cada cuanto se revisa la bodega, que tan
/// rapido rota el producto. Treinta dias es un punto de partida razonable, no una
/// ley; recompilar para moverlo a cuarenta seria absurdo.
///
/// ES UN VALOR POR DEFECTO, NO UN LIMITE. Quien consulta puede pedir otro
/// horizonte en la misma peticion: este es el que se usa cuando no pide ninguno,
/// que es el caso de la pantalla de inicio y de las alertas automaticas.
///
/// NO DEPENDE DE IConfiguration a proposito. Esta clase vive en Application, que
/// no referencia ningun paquete de configuracion; la lectura de appsettings se
/// hace en el arranque de la API y aqui solo llega el numero ya resuelto. Asi el
/// servicio se puede construir en una prueba sin montar un arbol de
/// configuracion.
/// </summary>
public sealed class OpcionesAlertasInventario
{
    /// <summary>Nombre de la seccion en appsettings.json.</summary>
    public const string Seccion = "AlertasInventario";

    /// <summary>
    /// El que se usa si la seccion no esta en la configuracion. Coincide con el
    /// valor sembrado en appsettings.json; esta duplicado a proposito, para que
    /// la API arranque igual aunque alguien borre la seccion.
    /// </summary>
    public const int DiasUmbralPorDefecto = 30;

    /// <summary>Menos de un dia no es un horizonte, es el pasado.</summary>
    public const int DiasMinimo = 1;

    /// <summary>
    /// Tope de un ano. Mas alla el aviso deja de ser un aviso: si todo lo que
    /// vence en los proximos dos anos sale en la lista de urgencias, la lista no
    /// dice nada. Ademas acota el costo de la consulta.
    /// </summary>
    public const int DiasMaximo = 365;

    /// <summary>Dias hacia adelante que se consideran "proximo a vencer".</summary>
    public int DiasUmbralVencimiento { get; }

    /// <param name="diasUmbralVencimiento">Entre 1 y 365.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Si el valor esta fuera de rango. LANZA en vez de acotar en silencio
    /// porque este valor viene de un archivo de configuracion que alguien
    /// escribio a mano: un cero o un 3650 es un error de digitacion, y acotarlo
    /// sin decir nada lo deja funcionando con un umbral que nadie pidio. El mismo
    /// criterio que usa JwtSettings con la clave de firma.
    ///
    /// Los parametros que llegan por HTTP si se acotan, porque ahi el numero lo
    /// manda un cliente cualquiera y tumbar la peticion no aporta nada.
    /// </exception>
    public OpcionesAlertasInventario(int diasUmbralVencimiento)
    {
        if (diasUmbralVencimiento is < DiasMinimo or > DiasMaximo)
        {
            throw new ArgumentOutOfRangeException(
                nameof(diasUmbralVencimiento),
                diasUmbralVencimiento,
                $"'{Seccion}:{nameof(DiasUmbralVencimiento)}' debe estar entre " +
                $"{DiasMinimo} y {DiasMaximo} dias.");
        }

        DiasUmbralVencimiento = diasUmbralVencimiento;
    }

    /// <summary>Los valores de fabrica, para pruebas y para cuando falta la seccion.</summary>
    public static OpcionesAlertasInventario PorDefecto { get; } = new(DiasUmbralPorDefecto);

    /// <summary>
    /// Deja un horizonte pedido por HTTP dentro del rango valido; si no se pidio
    /// ninguno, devuelve el configurado.
    ///
    /// Centralizado aqui y no repetido en cada servicio para que el tablero y el
    /// modulo de inventario no puedan acotar distinto y terminar contando lotes
    /// diferentes bajo el mismo nombre.
    /// </summary>
    public int ResolverHorizonte(int? diasSolicitados) =>
        diasSolicitados is int dias
            ? Math.Clamp(dias, DiasMinimo, DiasMaximo)
            : DiasUmbralVencimiento;
}
