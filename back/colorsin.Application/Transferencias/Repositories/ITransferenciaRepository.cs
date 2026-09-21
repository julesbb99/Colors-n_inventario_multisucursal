using Colorsin.Domain.Inventario;
using Colorsin.Domain.Transferencias;

namespace Colorsin.Application.Transferencias.Repositories;

/// <summary>
/// Acceso a los traslados entre sedes y a sus novedades.
///
/// Igual que en el resto del backend, los metodos que escriben solo dejan el
/// cambio preparado; confirmar es cosa de <see cref="GuardarCambiosAsync"/>
/// dentro de la transaccion que abre <see cref="EjecutarEnTransaccionAsync"/>.
/// </summary>
public interface ITransferenciaRepository
{
    /// <summary>
    /// Traslados, del mas reciente al mas antiguo. Los filtros nulos no se
    /// aplican. NO carga movimientos ni novedades.
    /// </summary>
    Task<IReadOnlyList<Transferencia>> ObtenerAsync(
        int? sucursalOrigenId = null,
        int? sucursalDestinoId = null,
        EstadoTransferencia? estado = null,
        int limite = 100,
        CancellationToken cancellationToken = default);

    /// <summary>El traslado con ese id y sus novedades, o <c>null</c>. Solo lectura.</summary>
    Task<Transferencia?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Traslados de un periodo, con sus novedades y sus dos sedes, PARA EL
    /// INFORME DE CUMPLIMIENTO.
    ///
    /// SIN TOPE DE FILAS, a diferencia de <see cref="ObtenerAsync"/>: un informe
    /// recortado a las cien mas recientes daria los porcentajes de una muestra
    /// arbitraria presentandolos como los del periodo. Lo que acota es el
    /// periodo.
    ///
    /// <paramref name="sucursalId"/> trae los traslados en que esa sede
    /// participa POR CUALQUIERA DE LOS DOS LADOS: lo que despacha y lo que
    /// recibe. Filtrar solo por origen dejaria fuera la mitad de su actividad.
    /// </summary>
    Task<IReadOnlyList<Transferencia>> ObtenerParaReporteAsync(
        DateTime? desde = null,
        DateTime? hasta = null,
        int? sucursalId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Una novedad por id, RASTREADA para poder cerrarla. <c>null</c> si no
    /// existe.
    /// </summary>
    Task<NovedadTransferencia?> ObtenerNovedadParaOperarAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cuantas novedades de ese traslado siguen abiertas.
    ///
    /// ES LO QUE DECIDE SI EL TRASLADO PUEDE DARSE POR TERMINADO: cierra solo
    /// cuando no le queda ninguna. Se usa en los dos sentidos:
    ///
    ///   al CERRAR una   se excluye esa, con <paramref name="exceptoNovedadId"/>,
    ///                   porque su cambio todavia no esta confirmado
    ///   al REGISTRAR    sin excluir nada, para saber si el traslado ya tenia
    ///     otra          algo pendiente antes. Sin esta consulta, una novedad
    ///                   de "solo constancia" cerraba un traslado que estaba
    ///                   esperando una reclamacion.
    /// </summary>
    Task<int> ContarNovedadesAbiertasAsync(
        int transferenciaId,
        int? exceptoNovedadId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Como <see cref="ObtenerPorIdAsync"/>, pero con la entidad rastreada y la
    /// fila bloqueada hasta el final de la transaccion.
    ///
    /// Ese bloqueo es lo que impide que dos despachos simultaneos del mismo
    /// traslado vean los dos el estado 'Solicitada' y saquen el stock dos veces.
    /// </summary>
    Task<Transferencia?> ObtenerParaOperarAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Movimientos que genero el despacho de un traslado, en el orden en que
    /// salieron (FEFO): el lote que vencia antes primero.
    ///
    /// Es lo que permite recrear en el destino los MISMOS lotes que salieron del
    /// origen, con su numero y su vencimiento. Sin esta consulta habria que
    /// adivinarlos, y la trazabilidad del fabricante se perderia en cada
    /// traslado.
    /// </summary>
    Task<IReadOnlyList<MovimientoInventario>> ObtenerMovimientosDespachoAsync(
        int transferenciaId,
        CancellationToken cancellationToken = default);

    /// <summary>Todos los movimientos de un traslado: los del despacho y los de la recepcion.</summary>
    Task<IReadOnlyList<MovimientoInventario>> ObtenerMovimientosAsync(
        int transferenciaId,
        CancellationToken cancellationToken = default);

    /// <summary>Crea un traslado.</summary>
    void AgregarTransferencia(Transferencia transferencia);

    /// <summary>Crea una novedad.</summary>
    void AgregarNovedad(NovedadTransferencia novedad);

    /// <summary>Confirma en la base todo lo preparado.</summary>
    Task<int> GuardarCambiosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta <paramref name="operacion"/> dentro de una transaccion: se
    /// confirma si termina bien y se revierte ante cualquier excepcion.
    ///
    /// Recibe la operacion, y no es un par abrir/confirmar, porque la conexion
    /// esta configurada con reintentos (<c>EnableRetryOnFailure</c>): con esa
    /// opcion EF Core rechaza las transacciones abiertas a mano y exige pasar
    /// por su estrategia de ejecucion, que necesita la operacion completa para
    /// poder reintentarla entera.
    ///
    /// Comparte AppDbContext con los repositorios de inventario, asi que lo que
    /// ellos preparen entra en esta misma transaccion. Es lo que hace que el
    /// saldo, los lotes, los movimientos, el estado del traslado y la auditoria
    /// se confirmen juntos o no se confirme nada.
    /// </summary>
    Task<T> EjecutarEnTransaccionAsync<T>(
        Func<CancellationToken, Task<T>> operacion,
        CancellationToken cancellationToken = default);
}
