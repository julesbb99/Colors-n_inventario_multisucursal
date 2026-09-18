using Colorsin.Application.Dashboard.DTOs;

namespace Colorsin.Application.Dashboard.Services;

/// <summary>
/// Lecturas agregadas para la pantalla de inicio. Cruza los cuatro modulos
/// operativos (inventario, compras, ventas y transferencias) sin ser dueno de
/// ninguno.
///
/// SOLO LECTURA, y no como recomendacion sino como contrato:
///
///   - Ningun metodo escribe, ni siquiera auditoria. Consultar un tablero no es
///     un hecho que haya que rastrear, y un <c>INSERT</c> por cada refresco
///     llenaria `auditoria_eventos` de ruido que le quita valor a lo que si
///     importa rastrear.
///   - Todas las consultas van con <c>AsNoTracking</c> y proyectando a estos
///     records. El dashboard trae miles de filas para devolver diez numeros; si
///     EF las rastreara, cada refresco dejaria ese volumen enganchado al
///     DbContext de la peticion, y una llamada posterior a <c>SaveChanges</c>
///     tendria que revisarlas una por una.
///   - No se toca el modelo. Ni entidades, ni <c>AppDbContext</c>, ni
///     migraciones: si algo no se puede calcular con el esquema actual, se
///     reporta la limitacion en vez de agregarle columnas al dominio para
///     acomodar una pantalla.
///
/// SOBRE LAS CIFRAS. Todo se recalcula en cada llamada contra las tablas
/// operativas; no hay tabla de resumen ni cache. Es lo correcto con el volumen
/// actual y mantiene el dato siempre fresco, pero significa que dos llamadas
/// seguidas pueden diferir y que el costo crece con la historia. El dia que
/// pese, el sitio para atacarlo es esta capa, no la interfaz.
///
/// SOBRE LAS UNIDADES. Todo lo que se exprese en litros pasa por el factor de
/// conversion de la unidad base de cada producto. Sumar <c>cantidad_base</c> a
/// secas mezclaria unidades distintas; ver
/// <see cref="ResumenGeneralDto.SaldoInventarioLitros"/>.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Los totales de la pantalla de inicio: ventas del dia y del mes, stock en
    /// litros, traslados en transito y alertas de reposicion.
    /// </summary>
    /// <param name="sucursalId">Sede a consultar. Nulo trae toda la red.</param>
    /// <param name="fechaCorte">
    /// Dia contra el que se calculan "del dia" y "del mes". Nulo usa la fecha de
    /// hoy del servidor.
    ///
    /// Es parametro y no una lectura de reloj escondida para que el resultado
    /// sea reproducible y para poder consultar un cierre pasado sin tener que
    /// cambiarle la hora a la maquina.
    /// </param>
    /// <param name="diasUmbralVencimiento">
    /// Ventana para el contador de lotes por vencer, de 1 a 365. Nulo usa el
    /// umbral configurado en <c>AlertasInventario:DiasUmbralVencimiento</c>.
    /// </param>
    Task<ResumenGeneralDto> ObtenerResumenGeneralAsync(
        int? sucursalId = null,
        DateOnly? fechaCorte = null,
        int? diasUmbralVencimiento = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ventas de un periodo: consolidado, serie diaria, productos mas vendidos
    /// y mejores clientes.
    /// </summary>
    /// <param name="desde">Primer dia del rango, incluido.</param>
    /// <param name="hasta">
    /// Ultimo dia del rango, tambien incluido. Si es anterior a
    /// <paramref name="desde"/>, el rango esta vacio y el resultado vuelve en
    /// ceros con las listas vacias: un rango al reves es un error de quien
    /// llama, no un fallo del sistema, y no justifica una excepcion.
    /// </param>
    /// <param name="sucursalId">Sede a consultar. Nulo trae toda la red.</param>
    /// <param name="top">
    /// Cuantos elementos devuelve cada ranking, de 1 a 50. Fuera de ese rango se
    /// ajusta al limite mas cercano: un tablero no necesita mas, y sin tope
    /// duro un <c>top</c> descuidado se trae el catalogo entero.
    /// </param>
    Task<MetricasVentasDto> ObtenerMetricasVentasAsync(
        DateOnly desde,
        DateOnly hasta,
        int? sucursalId = null,
        int top = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inventario visto desde arriba: reparto por sede, lotes por vencer en
    /// orden FEFO y ultimos movimientos del libro mayor.
    /// </summary>
    /// <param name="sucursalId">Sede a consultar. Nulo trae toda la red.</param>
    /// <param name="diasHorizonteVencimiento">
    /// Cuantos dias hacia adelante mirar para los vencimientos, de 1 a 365. Nulo
    /// usa el umbral configurado en
    /// <c>AlertasInventario:DiasUmbralVencimiento</c>, que es el mismo con el que
    /// se cuenta <c>ResumenGeneralDto.AlertasVencimiento</c>: si no coincidieran,
    /// la pantalla de inicio diria "7 lotes por vencer" y el detalle mostraria
    /// otra cantidad.
    ///
    /// Los lotes YA VENCIDOS con saldo entran siempre, sea cual sea el
    /// horizonte: son los mas urgentes y esconderlos por quedar fuera de la
    /// ventana seria justo al reves de lo que se busca.
    /// </param>
    /// <param name="topLotes">Cuantos lotes devuelve, de 1 a 50.</param>
    /// <param name="topMovimientos">Cuantos movimientos recientes devuelve, de 1 a 50.</param>
    Task<MetricasInventarioDto> ObtenerMetricasInventarioAsync(
        int? sucursalId = null,
        int? diasHorizonteVencimiento = null,
        int topLotes = 10,
        int topMovimientos = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Estado de la logistica: cuantos traslados hay en cada etapa y las
    /// novedades reportadas mas recientes.
    /// </summary>
    /// <param name="sucursalId">
    /// Sede a consultar. Nulo trae toda la red. Cuenta los traslados en que la
    /// sede es origen O destino.
    /// </param>
    /// <param name="topNovedades">Cuantas novedades devuelve, de 1 a 50.</param>
    Task<MetricasTransferenciasDto> ObtenerMetricasTransferenciasAsync(
        int? sucursalId = null,
        int topNovedades = 10,
        CancellationToken cancellationToken = default);
}
