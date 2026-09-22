using Colorsin.Api.Auth;
using Colorsin.Application.Comun.Auth;
using Colorsin.Application.Transferencias.DTOs;
using Colorsin.Application.Transferencias.Services;
using Colorsin.Domain.Transferencias;

namespace Colorsin.Api.Endpoints;

/// <summary>
/// Endpoints de traslados entre sedes: solicitud, despacho, recepcion, cierre y
/// novedades.
///
/// QUE SEDE MANDA EN CADA OPERACION. Un traslado tiene dos, y no siempre vale la
/// misma:
///
///   solicitar   el DESTINO, que es quien pide el producto
///   despachar   el ORIGEN, que es de donde sale el stock
///   recibir     el DESTINO, que es donde entra
///   rechazar    el ORIGEN, que es quien decide no atenderlo
///   cancelar    cualquiera de las dos: es anular algo que aun no se ha movido
///   novedad     cualquiera de las dos: el dano se ve al cargar o al descargar
///
/// Esa es la razon de que cada endpoint compruebe un lado distinto en vez de
/// haber una sola regla para todos. Aplicar la misma a todo dejaria a la sede
/// destino despachando stock que no tiene.
///
/// Todas las operaciones salvo la solicitud se dirigen por id, asi que hay que
/// leer el traslado antes para saber sus dos sedes. Ese es el motivo de la
/// consulta previa.
/// </summary>
public static class TransferenciasEndpoints
{
    /// <summary>Registra el grupo <c>/api/transferencias</c>.</summary>
    public static IEndpointRouteBuilder MapTransferenciasEndpoints(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/transferencias")
            .WithTags("Transferencias")
            .RequireAuthorization();

        // ---------------------------------------------------------------------
        // Transportadoras
        // ---------------------------------------------------------------------
        grupo.MapGet("/transportadoras", async (
                ITransferenciasService transferencias,
                bool? incluirRetiradas,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await transferencias.ObtenerTransportadorasAsync(
                incluirRetiradas ?? false, cancellationToken)))
            .WithName("TransferenciasTransportadoras")
            .WithSummary("Catalogo de transportadoras")
            .WithDescription(
                "De la red, no de una sede. Se elige al despachar, y sus `diasEntrega` son los " +
                "que calculan la fecha estimada de llegada. " +
                "Por omision NO trae las retiradas, que es lo que quiere el selector del " +
                "despacho; `incluirRetiradas=true` las anade, para la pantalla que las administra.");

        grupo.MapPost("/transportadoras", async (
                GuardarTransportadoraDto peticion,
                ITransferenciasService transferencias,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                var resultado = await transferencias.CrearTransportadoraAsync(
                    peticion, contexto.UsuarioIdRequerido(), cancellationToken);

                return resultado.Exito
                    ? Results.Created(
                        $"/api/transferencias/transportadoras/{resultado.Transportadora!.Id}",
                        resultado.Transportadora)
                    : RespuestasHttp.Fallo(
                        CodigoDe(resultado.Error), "Transportadora rechazada", resultado.Mensaje);
            })
            .WithName("TransferenciasCrearTransportadora")
            .WithSummary("Da de alta una transportadora. Administracion y gerencia.")
            .WithDescription(
                "Nombre, tipo de servicio ('urgente' o 'estandar') y dias de entrega. " +
                "Responde 409 si el nombre ya existe, ignorando mayusculas y espacios. " +
                "SIN COMPROBACION DE SEDE, a diferencia del resto del modulo: el catalogo es de " +
                "la red y no pertenece a ninguna bodega.")
            .Produces<TransportadoraDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status409Conflict)
            // Administrador General y Gerente de Sucursal, no solo el primero.
            //
            // Es una diferencia deliberada con el catalogo de PROVEEDORES, que
            // si esta reservado al Administrador General: alli cuelgan los
            // precios de compra, y retocar uno mueve el criterio con el que
            // compran las tres sedes. Una transportadora no lleva precios; es un
            // contacto de logistica, y quien negocia el flete de su sede es
            // quien sabe con quien se trabaja.
            .RequireAuthorization(PoliticasAutorizacion.Supervision);

        grupo.MapPut("/transportadoras/{id:int}", async (
                int id,
                GuardarTransportadoraDto peticion,
                ITransferenciasService transferencias,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                var resultado = await transferencias.ActualizarTransportadoraAsync(
                    id, peticion, contexto.UsuarioIdRequerido(), cancellationToken);

                return resultado.Exito
                    ? Results.Ok(resultado.Transportadora)
                    : RespuestasHttp.Fallo(
                        CodigoDe(resultado.Error), "Transportadora rechazada", resultado.Mensaje);
            })
            .WithName("TransferenciasActualizarTransportadora")
            .WithSummary("Cambia los datos de una transportadora. Administracion y gerencia.")
            .WithDescription(
                "NO TOCA LOS TRASLADOS YA DESPACHADOS: cada uno guarda su guia y su fecha " +
                "estimada propias, no una referencia a este plazo. Cambiar los dias afecta a los " +
                "despachos que vengan, no a los que ya salieron.")
            .Produces<TransportadoraDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(PoliticasAutorizacion.Supervision);

        grupo.MapDelete("/transportadoras/{id:int}", async (
                int id,
                ITransferenciasService transferencias,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                var resultado = await transferencias.CambiarEstadoTransportadoraAsync(
                    id, activa: false, contexto.UsuarioIdRequerido(), cancellationToken);

                return resultado.Exito
                    ? Results.Ok(resultado.Transportadora)
                    : RespuestasHttp.Fallo(
                        CodigoDe(resultado.Error), "Retiro no aplicado", resultado.Mensaje);
            })
            .WithName("TransferenciasRetirarTransportadora")
            .WithSummary("Retira una transportadora del catalogo. Administracion y gerencia.")
            .WithDescription(
                "ES UNA BAJA LOGICA, NO UN BORRADO, y el verbo DELETE aqui es la unica parte que " +
                "suena a lo contrario: la fila se conserva con `activo=false`. Los traslados que " +
                "llevo la siguen citando, con su guia y su fecha estimada, que es lo que hace " +
                "falta el dia que se reclama un faltante. " +
                "Deja de ofrecerse al despachar, y un despacho que la pida igualmente se rechaza. " +
                "Se deshace con el endpoint de reactivar.")
            .Produces<TransportadoraDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(PoliticasAutorizacion.Supervision);

        grupo.MapPost("/transportadoras/{id:int}/reactivar", async (
                int id,
                ITransferenciasService transferencias,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                var resultado = await transferencias.CambiarEstadoTransportadoraAsync(
                    id, activa: true, contexto.UsuarioIdRequerido(), cancellationToken);

                return resultado.Exito
                    ? Results.Ok(resultado.Transportadora)
                    : RespuestasHttp.Fallo(
                        CodigoDe(resultado.Error), "Reactivacion no aplicada", resultado.Mensaje);
            })
            .WithName("TransferenciasReactivarTransportadora")
            .WithSummary("Devuelve al catalogo una transportadora retirada.")
            .WithDescription(
                "Existe porque se vuelve a contratar a una empresa que se habia dejado de usar, y " +
                "reactivarla conserva su historia en vez de obligar a crear un duplicado con el " +
                "mismo nombre -que ademas chocaria con la comprobacion de nombre unico-.")
            .Produces<TransportadoraDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(PoliticasAutorizacion.Supervision);

        // ---------------------------------------------------------------------
        // Consultas
        // ---------------------------------------------------------------------
        grupo.MapGet("/", async (
                ITransferenciasService transferencias,
                IUsuarioContexto contexto,
                int? sucursalOrigenId,
                int? sucursalDestinoId,
                EstadoTransferencia? estado,
                int? limite,
                CancellationToken cancellationToken) =>
            {
                // Un traslado tiene dos sedes, asi que no basta con acotar un
                // filtro: hay que comprobar cada uno de los dos que venga. Sin
                // ninguno, un usuario de sede solo puede ver los suyos, y se le
                // ponen las DOS consultas -lo que envia y lo que recibe- en vez
                // de una, porque las dos son suyas.
                var origen = sucursalOrigenId;
                var destino = sucursalDestinoId;

                if (origen is int o) { contexto.ExigirAccesoASucursal(o); }
                if (destino is int d) { contexto.ExigirAccesoASucursal(d); }

                if (origen is null && destino is null && !contexto.EsAdminGeneral)
                {
                    var propia = contexto.ResolverFiltroSucursal(null);

                    var enviadas = await transferencias.ObtenerTransferenciasAsync(
                        propia, null, estado, limite ?? 100, cancellationToken);

                    var recibidas = await transferencias.ObtenerTransferenciasAsync(
                        null, propia, estado, limite ?? 100, cancellationToken);

                    // Un traslado de la sede consigo misma no existe -lo impide
                    // un CHECK- pero el Distinct cuesta nada y evita depender de
                    // eso.
                    return Results.Ok(enviadas.Concat(recibidas)
                        .DistinctBy(t => t.Id)
                        .OrderByDescending(t => t.FechaSolicitud)
                        .ThenByDescending(t => t.Id)
                        .ToList());
                }

                return Results.Ok(await transferencias.ObtenerTransferenciasAsync(
                    origen, destino, estado, limite ?? 100, cancellationToken));
            })
            .WithName("TransferenciasListar")
            .WithSummary("Traslados, del mas reciente al mas antiguo")
            .WithDescription(
                "Sin movimientos ni novedades. Estados: Solicitada, EnTransito, Completada, " +
                "RecibidaParcial, Rechazada, Cancelada. Un usuario de sede que no filtre ve los " +
                "traslados que su sede envia Y los que recibe.");

        grupo.MapGet("/{id:int}", async (
                int id,
                ITransferenciasService transferencias,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                var traslado = await transferencias.ObtenerTransferenciaPorIdAsync(
                    id, cancellationToken);

                if (traslado is null)
                {
                    return Results.NotFound();
                }

                contexto.ExigirAccesoAAlgunaDe(
                    traslado.SucursalOrigenId, traslado.SucursalDestinoId);

                return Results.Ok(traslado);
            })
            .WithName("TransferenciaPorId")
            .WithSummary("Un traslado con sus movimientos y novedades")
            .Produces<TransferenciaDto>()
            .Produces(StatusCodes.Status404NotFound);

        // ---------------------------------------------------------------------
        // Ciclo de vida
        // ---------------------------------------------------------------------
        grupo.MapPost("/", async (
                CrearTransferenciaDto peticion,
                ITransferenciasService transferencias,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                // El DESTINO: quien pide es quien necesita el producto. Un
                // gerente no puede hacer que otra sede se pida mercancia.
                contexto.ExigirAccesoASucursal(peticion.SucursalDestinoId);

                var resultado = await transferencias.CrearAsync(
                    peticion, contexto.UsuarioIdRequerido(), cancellationToken);

                return Responder(resultado, "Traslado rechazado");
            })
            .WithName("TransferenciasCrear")
            .WithSummary("Solicita un traslado")
            .WithDescription(
                "Nace 'Solicitada' y NO mueve stock ni lo reserva: las existencias se validan y " +
                "se descuentan al despachar, asi que pueden haberse agotado para entonces. " +
                "Quien solicita sale del token.")
            .Produces<ResultadoTransferencia>();

        grupo.MapPost("/{id:int}/despacho", async (
                int id,
                DespacharTransferenciaDto peticion,
                ITransferenciasService transferencias,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                var traslado = await transferencias.ObtenerTransferenciaPorIdAsync(
                    id, cancellationToken);

                if (traslado is null) { return Results.NotFound(); }

                // El ORIGEN: el stock sale de su bodega.
                contexto.ExigirAccesoASucursal(traslado.SucursalOrigenId);

                var resultado = await transferencias.DespacharAsync(
                    peticion with { TransferenciaId = id },
                    contexto.UsuarioIdRequerido(),
                    cancellationToken);

                return Responder(resultado, "Despacho rechazado");
            })
            .WithName("TransferenciasDespachar")
            .WithSummary("Despacha el traslado: la mercancia sale del origen")
            .WithDescription(
                "Valida disponibilidad, descuenta el saldo del origen repartiendolo entre sus " +
                "lotes por FEFO, anexa un movimiento de Retiro por lote, asigna transportadora y " +
                "guia, y pasa el traslado a 'EnTransito'. Todo o nada. " +
                "`cantidadDespachada` AJUSTA LO QUE DE VERDAD SALE cuando el origen no tiene todo " +
                "lo pedido: piden 5, hay 3, se mandan 3. Omitida despacha lo solicitado; mayor " +
                "que lo solicitado se rechaza, porque el ajuste solo va hacia abajo. La " +
                "diferencia NO es una perdida: esa mercancia nunca salio. " +
                "`fechaEstimadaLlegada` es OBLIGATORIA y no puede ser anterior a hoy: sin ella no " +
                "hay a partir de cuando decir que el traslado va tarde, y ademas es la que " +
                "habilita la recepcion. La pantalla la calcula sumando `diasEntrega` de la " +
                "transportadora a la fecha de despacho.")
            .Produces<ResultadoTransferencia>();

        grupo.MapPost("/{id:int}/recepcion", async (
                int id,
                RecibirTransferenciaDto peticion,
                ITransferenciasService transferencias,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                var traslado = await transferencias.ObtenerTransferenciaPorIdAsync(
                    id, cancellationToken);

                if (traslado is null) { return Results.NotFound(); }

                // El DESTINO: es donde entra el stock.
                contexto.ExigirAccesoASucursal(traslado.SucursalDestinoId);

                var resultado = await transferencias.RecibirAsync(
                    peticion with { TransferenciaId = id },
                    contexto.UsuarioIdRequerido(),
                    cancellationToken);

                return Responder(resultado, "Recepcion rechazada");
            })
            .WithName("TransferenciasRecibir")
            .WithSummary("Confirma la llegada al destino")
            .WithDescription(
                "Sube el saldo del destino y recrea alli los lotes que salieron del origen, con " +
                "su mismo numero y vencimiento, para no perder la trazabilidad del fabricante. " +
                "Sin `cantidadRecibida` se da por recibido todo lo despachado; con menos, el " +
                "traslado queda 'RecibidaParcial' y la diferencia se da por perdida en transito. " +
                "SE COMPARA CONTRA LO DESPACHADO, no contra lo solicitado: si el origen ajusto el " +
                "envio a 3 de los 5 pedidos y llegaron los 3, el traslado llego COMPLETO. " +
                "NO SE PUEDE RECIBIR ANTES DE `fechaEstimadaLlegada`: responde 409 hasta ese dia, " +
                "para todos los roles.")
            .Produces<ResultadoTransferencia>();

        grupo.MapPost("/{id:int}/rechazo", async (
                int id,
                CierreTransferenciaDto? peticion,
                ITransferenciasService transferencias,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                var traslado = await transferencias.ObtenerTransferenciaPorIdAsync(
                    id, cancellationToken);

                if (traslado is null) { return Results.NotFound(); }

                // El ORIGEN: rechazar es decir "no lo atiendo".
                contexto.ExigirAccesoASucursal(traslado.SucursalOrigenId);

                var resultado = await transferencias.RechazarAsync(
                    id, contexto.UsuarioIdRequerido(), peticion?.Motivo, cancellationToken);

                return Responder(resultado, "Rechazo no aplicado");
            })
            .WithName("TransferenciasRechazar")
            .WithSummary("La sede origen no atiende el traslado. Cualquier rol, en el origen.")
            .WithDescription(
                "Solo desde 'Solicitada'. Despues del despacho la mercancia ya salio y anularlo " +
                "dejaria stock sin dueno. " +
                "ABIERTO AL OPERADOR DEL ORIGEN: aceptar y rechazar son la misma decision vista " +
                "desde los dos lados, y aceptar -despachar- siempre estuvo abierta. No mueve stock.")
            .Produces<ResultadoTransferencia>();
            // SIN politica de supervision, al contrario que antes.
            //
            // La razon por la que estaba: se leyo como la "aprobacion de
            // traslados" de la especificacion, una decision sobre a quien se
            // atiende. El problema practico es que la decision contraria
            // -aceptar, o sea despachar- nunca exigio supervision, asi que el
            // operador del origen podia entregar la mercancia pero no decir que
            // no podia. Quien mira el estante y ve que no hay es el, y sin esto
            // la peticion se quedaba abierta hasta que pasara un gerente.
            //
            // Lo que SIGUE protegiendo: el eje de sede, comprobado arriba, y el
            // estado, comprobado en el servicio. Un rechazo no toca stock.

        grupo.MapPost("/{id:int}/cancelacion", async (
                int id,
                CierreTransferenciaDto? peticion,
                ITransferenciasService transferencias,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                var traslado = await transferencias.ObtenerTransferenciaPorIdAsync(
                    id, cancellationToken);

                if (traslado is null) { return Results.NotFound(); }

                // Cualquiera de las dos: no se ha movido nada todavia.
                contexto.ExigirAccesoAAlgunaDe(
                    traslado.SucursalOrigenId, traslado.SucursalDestinoId);

                var usuarioId = contexto.UsuarioIdRequerido();

                // CANCELAR ES RETIRAR UNA PETICION PROPIA, no cerrar el
                // documento de otro. Por eso la comprobacion es por PERSONA y no
                // solo por sede: en una misma bodega, quien pidio el traslado es
                // quien sabe si dejo de hacer falta, y no tiene por que poder
                // retirar lo que pidio un companero.
                //
                // Supervision sigue pudiendo con cualquiera, porque responde por
                // la sede y alguien tiene que poder cerrar la peticion de quien
                // ya no esta.
                if (traslado.UsuarioId != usuarioId && !RolesColorsin.EsSupervision(contexto.Rol))
                {
                    throw new AccesoDenegadoException(
                        $"El usuario {usuarioId} (rol '{contexto.Rol}') intento cancelar el " +
                        $"traslado {id}, que solicito el usuario {traslado.UsuarioId}.");
                }

                var resultado = await transferencias.CancelarAsync(
                    id, usuarioId, peticion?.Motivo, cancellationToken);

                return Responder(resultado, "Cancelacion no aplicada");
            })
            .WithName("TransferenciasCancelar")
            .WithSummary("Anula un traslado antes de despacharlo. Quien lo pidio, o supervision.")
            .WithDescription(
                "Solo desde 'Solicitada', por la misma razon que el rechazo. " +
                "ABIERTO A QUIEN LO SOLICITO, sea cual sea su rol: es su peticion y todavia no se " +
                "ha movido nada. Otro usuario de la misma sede recibe 403 salvo que sea " +
                "supervision, que si puede cerrar la peticion de cualquiera.")
            .Produces<ResultadoTransferencia>();

        // ---------------------------------------------------------------------
        // Novedades
        // ---------------------------------------------------------------------
        grupo.MapPost("/{id:int}/novedades", async (
                int id,
                RegistrarNovedadDto peticion,
                ITransferenciasService transferencias,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                var traslado = await transferencias.ObtenerTransferenciaPorIdAsync(
                    id, cancellationToken);

                if (traslado is null) { return Results.NotFound(); }

                contexto.ExigirAccesoAAlgunaDe(
                    traslado.SucursalOrigenId, traslado.SucursalDestinoId);

                // AQUI ESTABA LA RESTRICCION que reservaba Faltante y Averia a
                // supervision. Se quito, y conviene dejar escrito por que.
                //
                // Una novedad NO MUEVE STOCK: es el testimonio de lo que se vio
                // al abrir las cajas. Quien las abre es el operador, asi que
                // exigirle rango para declarar un faltante solo conseguia que el
                // faltante no se anotara -o que lo anotara, horas despues, quien
                // no estuvo en la descarga-.
                //
                // En un traslado que llega corto ademas era contradictorio: el
                // operador del destino SI podia registrar la recepcion parcial,
                // que es la que de verdad decide cuanto entra al saldo, y no
                // podia dejar constancia de lo que falto.
                //
                // Lo que sigue protegiendo: el eje de sede, comprobado arriba.
                // Nadie deja novedades en traslados ajenos.
                var resultado = await transferencias.RegistrarNovedadAsync(
                    peticion with { TransferenciaId = id },
                    contexto.UsuarioIdRequerido(),
                    cancellationToken);

                return resultado.Exito
                    ? Results.Ok(resultado)
                    : RespuestasHttp.Fallo(
                        CodigoDe(resultado.Error), "Novedad rechazada", resultado.Mensaje);
            })
            .WithName("TransferenciasRegistrarNovedad")
            .WithSummary("Deja constancia de un hallazgo sobre el traslado")
            .WithDescription(
                "Tipos: Faltante, Averia, Sobrante, Retraso. NO MUEVE STOCK: lo que ajusta el " +
                "saldo es la cantidad que declare el destino al recibir. " +
                "EL TRATAMIENTO decide el estado del traslado: 'Reenvio' y 'Reclamacion' dejan la " +
                "novedad ABIERTA y el traslado sigue por recibir hasta que se cierre; 'Ninguno' y " +
                "'Asumido' no esperan nada, y un traslado en 'RecibidaParcial' pasa a 'Cerrada'. " +
                "Se puede registrar en cualquier estado, incluso despues de cerrado: los danos se " +
                "descubren al abrir las cajas. " +
                "ABIERTA A CUALQUIER ROL de las dos sedes del traslado: es el testimonio de quien " +
                "descargo.")
            .Produces<ResultadoNovedad>();

        grupo.MapPost("/{id:int}/novedades/{novedadId:int}/cierre", async (
                int id,
                int novedadId,
                CerrarNovedadDto? peticion,
                ITransferenciasService transferencias,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                var traslado = await transferencias.ObtenerTransferenciaPorIdAsync(
                    id, cancellationToken);

                if (traslado is null) { return Results.NotFound(); }

                contexto.ExigirAccesoAAlgunaDe(
                    traslado.SucursalOrigenId, traslado.SucursalDestinoId);

                // La novedad tiene que ser DE ESTE traslado. El servicio la
                // busca por su id, asi que sin esta comprobacion se podria
                // cerrar la novedad de un traslado ajeno pasando el id de uno
                // propio en la ruta, y el control de sede de arriba no serviria
                // de nada.
                if (traslado.Novedades.All(n => n.Id != novedadId))
                {
                    return RespuestasHttp.Fallo(
                        StatusCodes.Status404NotFound, "Novedad no encontrada",
                        $"La novedad {novedadId} no pertenece al traslado {id}.");
                }

                var resultado = await transferencias.CerrarNovedadAsync(
                    novedadId, peticion?.Motivo, contexto.UsuarioIdRequerido(), cancellationToken);

                return resultado.Exito
                    ? Results.Ok(resultado)
                    : RespuestasHttp.Fallo(
                        CodigoDe(resultado.Error), "Cierre no aplicado", resultado.Mensaje);
            })
            .WithName("TransferenciasCerrarNovedad")
            .WithSummary("Cierra una novedad pendiente, dejando escrito el porque")
            .WithDescription(
                "Es el final de un reenvio o de una reclamacion: llego lo que faltaba, la " +
                "transportadora respondio, o se da por perdido. " +
                "NO BORRA NADA: la novedad se conserva entera -tipo, cantidad, quien la reporto y " +
                "cuando- y se le anade el desenlace con su motivo, su fecha y quien la cerro. " +
                "El MOTIVO es obligatorio; sin el responde 400, porque sin el cerrar seria " +
                "indistinguible de borrar. " +
                "Si era la ultima pendiente y el traslado estaba en 'RecibidaParcial', pasa a " +
                "'Cerrada'. Si le quedan otras abiertas, sigue pendiente.")
            .Produces<ResultadoNovedad>()
            .Produces(StatusCodes.Status404NotFound);

        // ---------------------------------------------------------------------
        // Informes
        // ---------------------------------------------------------------------
        grupo.MapGet("/reportes/cumplimiento", async (
                ITransferenciasService transferencias,
                IUsuarioContexto contexto,
                DateTime? desde,
                DateTime? hasta,
                int? sucursalId,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await transferencias.ObtenerReporteCumplimientoAsync(
                desde,
                hasta,
                // La MISMA regla de aislamiento que el resto del modulo: un
                // gerente de Armenia no saca el informe de Cali. Sin sede, el
                // Administrador General ve la red y los demas su propia sede.
                contexto.ResolverFiltroSucursal(sucursalId),
                cancellationToken)))
            .WithName("TransferenciasReporteCumplimiento")
            .WithSummary("Cumplimiento logistico por sucursal y por ruta")
            .WithDescription(
                "TODO EN CONTEOS, nunca en volumenes: cada traslado lleva su producto en su " +
                "unidad, y sumar litros con galones daria un numero sin significado. " +
                "TRES CUMPLIMIENTOS DISTINTOS que no se funden en uno: 'atencion' es cuantas " +
                "peticiones despacho el origen, 'cantidad' cuantos de los recibidos llegaron " +
                "enteros, y 'plazo' cuantos llegaron dentro de la fecha estimada. Una " +
                "transportadora puede cumplir el plazo y perder producto en cada viaje. " +
                "Se agrupa por la sede de ORIGEN, que es la que responde por el traslado. " +
                "`desde` y `hasta` filtran por fecha de solicitud; sin ellas, todo el historico. " +
                "ABIERTO A CUALQUIER ROL, acotado a su sede.")
            .Produces<ReporteCumplimientoDto>();

        grupo.MapGet("/reportes/cumplimiento/detalle", async (
                ITransferenciasService transferencias,
                IUsuarioContexto contexto,
                DateTime? desde,
                DateTime? hasta,
                int? sucursalId,
                int? limite,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await transferencias.ObtenerDetalleCumplimientoAsync(
                desde,
                hasta,
                // La MISMA regla de aislamiento que el agregado y que el resto
                // del modulo: un gerente de Armenia no saca el detalle de Cali.
                contexto.ResolverFiltroSucursal(sucursalId),
                limite ?? 200,
                cancellationToken)))
            .WithName("TransferenciasCumplimientoDetalle")
            .WithSummary("Cumplimiento traslado por traslado")
            .WithDescription(
                "EL AGREGADO CONTESTA 'COMO VAMOS'; ESTE CONTESTA 'CUAL FALLO'. Un 66 % de " +
                "cumplimiento de plazo no dice que traslado llego tarde, con que transportadora " +
                "ni con que guia. Cada fila trae su producto, sus LOTES en orden FEFO, las cuatro " +
                "fechas del ciclo, los dias de transito reales, la desviacion en dias contra lo " +
                "previsto -positiva es tarde- y sus novedades. " +
                "`plazo` tiene SEIS valores y no dos: 'NoAplica' si se rechazo o se cancelo, " +
                "'SinDespachar', 'EnTransito', 'ATiempo', 'Tarde', y 'SinPlazo' para los " +
                "despachados antes de que la fecha estimada fuera obligatoria, que no se pueden " +
                "juzgar. " +
                "`llegoCompleto` se mide contra lo DESPACHADO, no contra lo pedido: lo que el " +
                "origen no mando va en `ajustadoEnOrigen`, que tiene otro responsable. " +
                "LLEVA TOPE, de 1 a 500, y `hayMas` dice si quedaron filas fuera; para ver mas " +
                "alla se acota el periodo. " +
                "ABIERTO A CUALQUIER ROL, acotado a su sede.")
            .Produces<CumplimientoDetalleDto>();

        return rutas;
    }

    // Aqui vivia EsCritica(), que reservaba Faltante y Averia a supervision. Su
    // propio comentario ya avisaba de lo que pasaria en la bodega: "el operador
    // que descarga es quien ve la lata rota, y con esta regla no puede
    // registrarla". Eso es justo lo que estorbo, asi que se quito. El detalle
    // esta en el endpoint de novedades.

    private static IResult Responder(ResultadoTransferencia resultado, string titulo) =>
        resultado.Exito
            ? Results.Ok(resultado)
            : RespuestasHttp.Fallo(CodigoDe(resultado.Error), titulo, resultado.Mensaje);

    private static int CodigoDe(ErrorTransportadora error) => error switch
    {
        ErrorTransportadora.NoEncontrada => StatusCodes.Status404NotFound,

        // 409: la peticion esta bien formada y quien la manda tiene permiso; lo
        // que no la admite es el estado actual de los datos.
        ErrorTransportadora.NombreDuplicado
            or ErrorTransportadora.EstadoSinCambio
            => StatusCodes.Status409Conflict,

        _ => StatusCodes.Status400BadRequest
    };

    private static int CodigoDe(ErrorTransferencia error) => error switch
    {
        ErrorTransferencia.TransferenciaNoEncontrada
            or ErrorTransferencia.ProductoNoEncontrado
            or ErrorTransferencia.UnidadNoEncontrada
            or ErrorTransferencia.TransportadoraNoEncontrada
            or ErrorTransferencia.SaldoNoEncontrado
            or ErrorTransferencia.NovedadNoEncontrada
            => StatusCodes.Status404NotFound,

        // 409: la peticion esta bien formada y quien la manda tiene permiso; lo
        // que la impide es el estado actual de los datos. La recepcion
        // anticipada entra aqui y no en 400 por lo mismo: lo que hay mal no es
        // lo que se mando, es el dia en que se manda.
        ErrorTransferencia.EstadoNoPermiteOperacion
            or ErrorTransferencia.StockInsuficiente
            or ErrorTransferencia.TransportadoraRetirada
            or ErrorTransferencia.RecepcionAnticipada
            or ErrorTransferencia.NovedadYaCerrada
            => StatusCodes.Status409Conflict,

        _ => StatusCodes.Status400BadRequest
    };
}

/// <summary>
/// Cuerpo opcional del rechazo y la cancelacion.
///
/// Existe porque esas dos operaciones no tienen DTO propio -el servicio las
/// expone con parametros sueltos- y un endpoint POST necesita algo que
/// deserializar para admitir el motivo. Es anulable entero: se puede cancelar
/// sin dar explicaciones.
/// </summary>
/// <param name="Motivo">Por que. Queda en la bitacora de auditoria.</param>
public sealed record CierreTransferenciaDto(string? Motivo = null);
