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

    /// <summary>
    /// Las ventas MES A MES y el acumulado de toda la historia.
    ///
    /// POR QUE EXISTE: el resumen solo decia "del dia" y "del mes", y el mes en
    /// curso siempre arranca en cero, asi que el dia 1 el tablero parecia el de
    /// una empresa que nunca ha vendido. Aqui se ve de donde viene ese mes y
    /// cuanto lleva vendido el negocio entero.
    ///
    /// EL ACUMULADO NO SALE DE SUMAR LA SERIE: esa viene recortada a la ventana
    /// de meses, y el acumulado es de toda la historia. Son dos consultas.
    /// </summary>
    /// <param name="sucursalId">Sede a consultar. Nulo trae toda la red.</param>
    /// <param name="meses">
    /// Cuantos meses hacia atras trae la serie, contando el actual. Se acota
    /// entre 1 y 120. No afecta al acumulado.
    /// </param>
    Task<HistoricoVentasDto> ObtenerHistoricoVentasAsync(
        int? sucursalId = null,
        int meses = 12,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotacion del catalogo: que se mueve rapido y que lleva meses quieto.
    ///
    /// SE CLASIFICA POR DIAS DE COBERTURA -cuanto aguanta el stock al ritmo del
    /// periodo- y no por cantidad vendida. "Se vendieron 400 litros" no dice si
    /// eso es mucho sin saber cuanto hay en bodega; los dias de cobertura si, y
    /// ademas son comparables entre productos de unidades distintas.
    ///
    /// Salen TODOS los productos del catalogo, tambien los que no se vendieron:
    /// ese es justamente el caso que hay que mirar.
    /// </summary>
    /// <param name="desde">Primer dia del periodo, incluido.</param>
    /// <param name="hasta">Ultimo dia, tambien incluido.</param>
    /// <param name="sucursalId">Sede a consultar. Nulo trae toda la red.</param>
    Task<RotacionProductosDto> ObtenerRotacionProductosAsync(
        DateOnly desde,
        DateOnly hasta,
        int? sucursalId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Comparativa de rendimiento entre las sedes de la red.
    ///
    /// NO APLICA EL AISLAMIENTO POR SEDE, y es la unica consulta de este
    /// servicio de la que hay que decirlo: una comparativa en la que cada
    /// gerente solo ve su propia fila no es una comparativa. Administracion y
    /// gerencia ven aqui las cifras de todas las sedes.
    ///
    /// LO QUE SI COMPRUEBA: el rol. Lanza
    /// <see cref="AccesoDenegadoException"/> si quien pregunta es un operador,
    /// ademas de la politica que lleva el endpoint. Y lo que expone son
    /// AGREGADOS: ninguna venta concreta, ningun cliente, ningun precio.
    /// </summary>
    /// <param name="desde">Primer dia del periodo, incluido.</param>
    /// <param name="hasta">Ultimo dia, tambien incluido.</param>
    Task<ComparativaSucursalesDto> ObtenerComparativaSucursalesAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancellationToken = default);
}
