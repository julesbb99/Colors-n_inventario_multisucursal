using Colorsin.Domain.Comun;
using Colorsin.Domain.Inventario;

namespace Colorsin.Domain.Transferencias;

/// <summary>
/// Traslado de un producto entre dos sedes de la red. Origen y destino
/// apuntan a la misma tabla, y la base impide que sean iguales.
/// </summary>
public class Transferencia
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public int SucursalOrigenId { get; set; }
    public int SucursalDestinoId { get; set; }

    /// <summary>
    /// Quien SOLICITO el traslado. Obligatorio.
    ///
    /// Ojo con no confundirlo con los otros dos responsables del ciclo: quien
    /// despacha y quien recibe quedan en `movimientos_inventario.usuario_id` de
    /// sus respectivos movimientos, y los tres en la bitacora de auditoria. Este
    /// es el que pidio el producto, y hasta ahora solo sobrevivia en esa
    /// bitacora, donde no se podia consultar desde el traslado.
    /// </summary>
    public int UsuarioId { get; set; }

    /// <summary>
    /// Transportadora que lleva el traslado. NULA mientras esta 'Solicitada':
    /// al pedir el producto todavia no se sabe quien lo va a mover; se asigna
    /// al despachar.
    /// </summary>
    public int? TransportadoraId { get; set; }

    /// <summary>
    /// Numero de guia del transportador. Se asigna junto con la transportadora,
    /// al despachar, y es con lo que se reclama si algo llega mal.
    /// </summary>
    public string? Guia { get; set; }

    /// <summary>Cantidad pedida, expresada en <see cref="UnidadId"/>.</summary>
    public decimal? CantidadSolicitada { get; set; }

    /// <summary>
    /// Lo que DE VERDAD salio del origen. Nulo hasta el despacho.
    ///
    /// Puede ser menor que <see cref="CantidadSolicitada"/>: la sede origen
    /// ajusta a lo que tiene en el estante. Nunca mayor -lo impide un CHECK-
    /// porque mandar de mas seria stock que el destino no pidio.
    ///
    /// SON DOS FALTANTES DISTINTOS Y NO HAY QUE MEZCLARLOS:
    ///   solicitada - despachada  lo que el origen no pudo mandar. Sigue en su
    ///                            estante; no se perdio nada.
    ///   despachada - recibida    lo que se perdio en el camino. Eso si es baja
    ///                            neta de la red y es lo que se reclama.
    /// </summary>
    public decimal? CantidadDespachada { get; set; }

    /// <summary>
    /// Nulo hasta que la sede destino confirma. Comparada con
    /// <see cref="CantidadDespachada"/> distingue recepcion completa de parcial.
    ///
    /// SE COMPARA CONTRA LO DESPACHADO, NO CONTRA LO SOLICITADO: si el origen
    /// mando 3 de los 5 que le pidieron y llegaron los 3, el traslado llego
    /// completo. Compararlo contra los 5 lo marcaria como perdida en transito
    /// de algo que nunca viajo.
    /// </summary>
    public decimal? CantidadRecibida { get; set; }

    /// <summary>
    /// Unidad del traslado: se puede pedir en galones aunque el stock se
    /// lleve en litros.
    /// </summary>
    public int UnidadId { get; set; }

    public EstadoTransferencia? Estado { get; set; }
    public Urgencia? Urgencia { get; set; }
    public DateTime? FechaSolicitud { get; set; }

    /// <summary>Cuando salio del origen. Nula mientras esta 'Solicitada'.</summary>
    public DateTime? FechaDespacho { get; set; }

    public DateTime? FechaEstimadaLlegada { get; set; }

    /// <summary>
    /// Cuando la conto el destino. Nula hasta que se recibe.
    ///
    /// Contra <see cref="FechaEstimadaLlegada"/> dice si la transportadora
    /// cumplio; menos <see cref="FechaDespacho"/>, el transito real. Las dos
    /// cifras son las que alimentan el informe de cumplimiento logistico.
    /// </summary>
    public DateTime? FechaRecepcion { get; set; }

    // --- Navegacion ---
    public Producto Producto { get; set; } = null!;
    public Sucursal SucursalOrigen { get; set; } = null!;
    public Sucursal SucursalDestino { get; set; } = null!;
    public Transportadora? Transportadora { get; set; }

    /// <summary>Quien solicito el traslado.</summary>
    public Usuario Usuario { get; set; } = null!;
    public UnidadMedida Unidad { get; set; } = null!;
    public ICollection<NovedadTransferencia> Novedades { get; set; } = new List<NovedadTransferencia>();
}
