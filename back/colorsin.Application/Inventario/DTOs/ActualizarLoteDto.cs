namespace Colorsin.Application.Inventario.DTOs;

/// <summary>
/// Correccion de los datos de un lote que ya existe.
///
/// SOLO SE PUEDE CORREGIR LO QUE SE DIGITO: el numero impreso en el envase y la
/// fecha de caducidad. Ni la cantidad, ni el producto, ni la sede.
///
///   LA CANTIDAD no esta aqui, y tampoco estaba al crear el lote:
///   cambiarla por este camino desajustaria el lote respecto del consolidado de
///   la sede y ademas no dejaria fila en el libro mayor. Para corregir un saldo
///   real -una merma, un conteo fisico que no cuadra- existe el movimiento de
///   ajuste, que mueve las dos tablas y queda registrado con su responsable.
///
///   EL PRODUCTO Y LA SEDE tampoco: un lote es una cantidad fisica en una
///   bodega, y "moverlo" de sede es un traslado, no una edicion. Si se pudieran
///   cambiar, el saldo saldria de una sede y entraria en otra sin que ningun
///   documento lo explique.
///
/// SOBRE CAMBIAR EL NUMERO DE LOTE. Se permite, porque un digito mal tecleado en
/// una recepcion es de lo mas comun, pero tiene una consecuencia que conviene
/// saber: los movimientos del libro mayor apuntan al lote por su id y muestran el
/// numero resolviendolo al consultar, asi que al renombrarlo TAMBIEN cambia como
/// se lee el historico. El rastro de quien lo cambio y desde que valor queda en
/// `auditoria_eventos`, que es donde no se puede reescribir.
/// </summary>
/// <param name="NumeroLote">
/// El nuevo numero. Sigue teniendo que ser unico dentro de la pareja
/// (producto, sede).
/// </param>
/// <param name="FechaVencimiento">
/// La nueva caducidad. OBLIGATORIA: se puede corregir, pero no borrar.
///
/// Antes, nula dejaba el lote como "no caduca". Ya no: todo lo que vende
/// Colorsin caduca, la columna `lotes.fecha_vencimiento` es NOT NULL y una
/// peticion sin fecha se rechaza con
/// <see cref="ErrorLote.VencimientoRequerido"/>. Si se admitiera, bastaria con
/// recibir el lote con fecha y borrarsela aqui para dejarlo fuera de las alertas.
///
/// SIGUE SIENDO ANULABLE EN EL TIPO para poder contestar ese rechazo con su
/// motivo: con un `DateOnly` a secas, la peticion sin fecha reventaria al
/// deserializar y volveria como un 400 generico.
///
/// OJO, ES UN PUT: el cuerpo describe como debe quedar el lote, no que campos
/// mover. Para cambiar solo uno hay que mandar el otro con su valor actual.
/// </param>
public sealed record ActualizarLoteDto(
    string NumeroLote,
    DateOnly? FechaVencimiento = null);
