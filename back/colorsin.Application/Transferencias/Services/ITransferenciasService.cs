using Colorsin.Application.Transferencias.DTOs;
using Colorsin.Domain.Transferencias;

namespace Colorsin.Application.Transferencias.Services;

/// <summary>Operaciones del modulo de traslados entre sedes.</summary>
public interface ITransferenciasService
{
    /// <summary>
    /// Transportadoras del catalogo, ordenadas por nombre. Las retiradas solo
    /// salen si se piden: ver <c>ITransportadoraRepository.ObtenerTodasAsync</c>.
    /// </summary>
    Task<IReadOnlyList<TransportadoraDto>> ObtenerTransportadorasAsync(
        bool incluirRetiradas = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Da de alta una transportadora y devuelve la creada, ya con su id.
    ///
    /// Rechaza el nombre repetido -ignorando mayusculas y espacios-, el tipo de
    /// servicio que no sea 'urgente' ni 'estandar', y un plazo menor que 1 dia.
    /// </summary>
    Task<ResultadoTransportadora> CrearTransportadoraAsync(
        GuardarTransportadoraDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cambia los datos de una transportadora. Mismas validaciones que el alta.
    ///
    /// NO TOCA LOS TRASLADOS YA DESPACHADOS: cada uno guarda su guia y su fecha
    /// estimada propias, no una referencia a este plazo. Cambiar los dias afecta
    /// a los despachos que vengan, no a los que ya salieron.
    /// </summary>
    Task<ResultadoTransportadora> ActualizarTransportadoraAsync(
        int id,
        GuardarTransportadoraDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retira o reactiva una transportadora.
    ///
    /// NO LA BORRA: los traslados que llevo siguen citandola, con su guia y su
    /// fecha estimada, que es lo que hace falta el dia que se reclama un
    /// faltante. Retirada deja de ofrecerse al despachar.
    ///
    /// Es la misma operacion en los dos sentidos porque hace lo mismo -cambiar
    /// una bandera- y separarla en dos metodos identicos es como uno gana una
    /// comprobacion que el otro no tiene.
    /// </summary>
    /// <param name="activa"><c>false</c> retira, <c>true</c> reactiva.</param>
    Task<ResultadoTransportadora> CambiarEstadoTransportadoraAsync(
        int id,
        bool activa,
        int usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>Traslados, del mas reciente al mas antiguo. Sin movimientos ni novedades.</summary>
    Task<IReadOnlyList<TransferenciaDto>> ObtenerTransferenciasAsync(
        int? sucursalOrigenId = null,
        int? sucursalDestinoId = null,
        EstadoTransferencia? estado = null,
        int limite = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// El traslado con ese id, con su detalle de movimientos lote por lote y
    /// sus novedades. <c>null</c> si no existe.
    /// </summary>
    Task<TransferenciaDto?> ObtenerTransferenciaPorIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Solicita un traslado. Nace 'Solicitada' y NO mueve stock ni lo reserva:
    /// las existencias se validan y se descuentan al despachar.
    /// </summary>
    /// <param name="peticion">Producto, sedes y cantidad.</param>
    /// <param name="usuarioId">
    /// Quien SOLICITA. Va como PARAMETRO y no dentro de
    /// <paramref name="peticion"/>: sale del token, no del cuerpo del JSON.
    ///
    /// Los tres responsables del ciclo -pide, despacha, recibe- se toman cada uno
    /// del token de SU propia peticion. Eso es lo que hace que de verdad sean
    /// distinguibles y no tres copias del mismo id que mando un cliente.
    /// </param>
    Task<ResultadoTransferencia> CrearAsync(
        CrearTransferenciaDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Despacha el traslado: la mercancia sale de la sede origen.
    ///
    /// En una sola transaccion valida disponibilidad, descuenta el saldo del
    /// origen, reparte el descuento entre sus lotes por FEFO, anexa un
    /// movimiento de Retiro/Transferencia por cada lote consumido, asigna
    /// transportadora y guia, y pasa el traslado a 'EnTransito'. O queda todo,
    /// o no queda nada.
    /// </summary>
    /// <param name="peticion">Traslado, transportadora y guia.</param>
    /// <param name="usuarioId">Quien despacha, del token. Queda en cada movimiento de Retiro.</param>
    Task<ResultadoTransferencia> DespacharAsync(
        DespacharTransferenciaDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirma la llegada a la sede destino.
    ///
    /// En una sola transaccion sube el saldo del destino, recrea alli los lotes
    /// que salieron del origen con su mismo numero y vencimiento, anexa un
    /// movimiento de Ingreso/Transferencia por cada uno y cierra el traslado
    /// como 'Completada' o 'RecibidaParcial' segun lo que haya llegado.
    /// </summary>
    /// <param name="peticion">Traslado y lo que llego.</param>
    /// <param name="usuarioId">Quien recibe, del token. Queda en cada movimiento de Ingreso.</param>
    Task<ResultadoTransferencia> RecibirAsync(
        RecibirTransferenciaDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rechaza un traslado solicitado: la sede origen no lo atiende. Solo desde
    /// 'Solicitada', porque despues del despacho la mercancia ya salio.
    /// </summary>
    Task<ResultadoTransferencia> RechazarAsync(
        int transferenciaId,
        int usuarioId,
        string? motivo = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Anula un traslado solicitado, a peticion de quien lo pidio. Solo desde
    /// 'Solicitada', por la misma razon.
    /// </summary>
    Task<ResultadoTransferencia> CancelarAsync(
        int transferenciaId,
        int usuarioId,
        string? motivo = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deja constancia de un hallazgo sobre un traslado. NO MUEVE STOCK: se
    /// puede registrar en cualquier momento, incluso despues de cerrado.
    ///
    /// SI PUEDE CAMBIAR EL ESTADO del traslado, segun el tratamiento:
    ///
    ///   Reenvio / Reclamacion  la novedad queda ABIERTA y el traslado sigue
    ///                          pendiente: hay algo que esperar.
    ///   Ninguno / Asumido      la novedad nace cerrada, y un traslado en
    ///                          'RecibidaParcial' pasa a 'Cerrada'.
    /// </summary>
    /// <param name="peticion">Traslado, tipo de novedad, tratamiento y descripcion.</param>
    /// <param name="usuarioId">
    /// Quien reporta, del token. Una novedad puede acabar en un reclamo a la
    /// transportadora, y entonces quien la firmo es justamente lo que se alega.
    /// </param>
    Task<ResultadoNovedad> RegistrarNovedadAsync(
        RegistrarNovedadDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cierra una novedad que quedaba esperando desenlace, dejando escrito el
    /// porque.
    ///
    /// NO BORRA NADA: la novedad se conserva entera -tipo, cantidad, quien la
    /// reporto y cuando- y se le anade el desenlace. Es lo que permite revisar
    /// despues por que aquel faltante no se le cobro a nadie.
    ///
    /// Si con esta se acaban las pendientes del traslado y estaba en
    /// 'RecibidaParcial', el traslado pasa a 'Cerrada'.
    /// </summary>
    /// <param name="novedadId">La novedad a cerrar. Debe estar abierta.</param>
    /// <param name="motivo">El porque. Obligatorio.</param>
    /// <param name="usuarioId">Quien cierra, del token.</param>
    Task<ResultadoNovedad> CerrarNovedadAsync(
        int novedadId,
        string? motivo,
        int usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Informe de cumplimiento logistico: por sede y por ruta.
    ///
    /// TODO EN CONTEOS, nunca en volumenes: cada traslado lleva su producto en
    /// su unidad, y sumar litros con galones daria un numero sin significado.
    /// Lo comparable entre traslados distintos es cuantos llegaron completos y
    /// cuantos a tiempo.
    /// </summary>
    /// <param name="desde">Inicio del periodo, por fecha de solicitud. Nulo no acota.</param>
    /// <param name="hasta">Fin del periodo. Nulo no acota.</param>
    /// <param name="sucursalId">
    /// Acota a los traslados en que esa sede participa por cualquiera de los dos
    /// lados. Nulo -solo el Administrador General- da la red entera.
    /// </param>
    Task<ReporteCumplimientoDto> ObtenerReporteCumplimientoAsync(
        DateTime? desde = null,
        DateTime? hasta = null,
        int? sucursalId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// El mismo informe, pero TRASLADO POR TRASLADO.
    ///
    /// EL AGREGADO CONTESTA "COMO VAMOS"; ESTE CONTESTA "CUAL FALLO". Un 66 % de
    /// cumplimiento de plazo no dice que traslado llego tarde, con que
    /// transportadora ni con que guia, y esos tres datos son los que hacen falta
    /// para reclamar. Aqui cada fila lleva su producto, sus lotes, sus cuatro
    /// fechas, los dias de transito reales, la desviacion contra lo previsto y
    /// sus novedades.
    ///
    /// LLEVA TOPE DE FILAS, a diferencia del agregado: aquel devuelve un punado
    /// de grupos por muchos traslados que haya, y este una fila por traslado. Se
    /// acota entre 1 y 500, y la respuesta dice si quedaron filas fuera.
    /// </summary>
    /// <param name="desde">Inicio del periodo, por fecha de solicitud. Nulo no acota.</param>
    /// <param name="hasta">Fin del periodo. Nulo no acota.</param>
    /// <param name="sucursalId">
    /// Acota a los traslados en que esa sede participa por cualquiera de los dos
    /// lados. Nulo -solo el Administrador General- da la red entera.
    /// </param>
    /// <param name="limite">Tope de filas, de 1 a 500.</param>
    Task<CumplimientoDetalleDto> ObtenerDetalleCumplimientoAsync(
        DateTime? desde = null,
        DateTime? hasta = null,
        int? sucursalId = null,
        int limite = 200,
        CancellationToken cancellationToken = default);
}
