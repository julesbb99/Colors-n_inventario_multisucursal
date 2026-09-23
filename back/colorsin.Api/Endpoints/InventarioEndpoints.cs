using Colorsin.Api.Auth;
using Colorsin.Application.Comun.Auth;
using Colorsin.Application.Inventario.DTOs;
using Colorsin.Application.Inventario.Services;

namespace Colorsin.Api.Endpoints;

/// <summary>
/// Endpoints de inventario: existencias, lotes, libro mayor y movimientos.
///
/// AISLAMIENTO POR SEDE. Las consultas pasan el <c>sucursalId</c> por
/// <see cref="IUsuarioContexto.ResolverFiltroSucursal"/> y usan lo que ese
/// metodo devuelve. El registro de un movimiento, que si nombra una sede
/// concreta, pasa por <see cref="IUsuarioContexto.ExigirAccesoASucursal"/>.
///
/// A diferencia del tablero, donde la regla vive dentro del servicio, aqui va en
/// el endpoint: <c>IInventarioService</c> no depende del contexto del usuario y
/// atarlo lo dejaria inservible fuera de una peticion HTTP. La contrapartida es
/// que un endpoint nuevo tiene que acordarse, y por eso todos los de este
/// archivo empiezan igual.
/// </summary>
public static class InventarioEndpoints
{
    /// <summary>Registra los grupos <c>/api/inventario</c> y <c>/api/productos</c>.</summary>
    public static IEndpointRouteBuilder MapInventarioEndpoints(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/inventario")
            .WithTags("Inventario")
            .RequireAuthorization();

        // ---------------------------------------------------------------------
        // Catalogo de productos
        //
        // Cuelga de /api/productos y no de /api/inventario porque no es una
        // consulta de stock: es el catalogo con el que se arma cualquier linea
        // de compra, venta o traslado. El stock de un producto en una sede si
        // vive en /api/inventario/existencias.
        // ---------------------------------------------------------------------
        rutas.MapGet("/api/productos", async (
                IInventarioService inventario,
                string? categoria,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await inventario.ObtenerProductosAsync(categoria, cancellationToken)))
            .WithTags("Productos")
            .WithName("ProductosListar")
            .WithSummary("Catalogo de productos con su unidad base")
            .WithDescription(
                "De toda la red, sin filtrar por sede: que una sede no tenga saldo de un producto " +
                "no significa que no lo maneje, y esconderselo le impediria pedirlo o comprarlo, " +
                "que es justo lo que hace falta cuando no hay. " +
                "`categoria` filtra por coincidencia exacta; omitida trae el catalogo completo. " +
                "OJO: el esquema no tiene marca de activo o inactivo, asi que salen todos.")
            .RequireAuthorization();

        // ---------------------------------------------------------------------
        // Consultas
        // ---------------------------------------------------------------------
        // ---------------------------------------------------------------------
        // EXISTENCIAS: LA UNICA CONSULTA DEL SISTEMA SIN AISLAMIENTO POR SEDE.
        //
        // No pasa por ResolverFiltroSucursal, y es deliberado: un gerente o un
        // operador ven las existencias de CUALQUIER sede. Lo pidio el negocio, y
        // tiene sentido operativo -antes de pedir un traslado hay que poder mirar
        // quien tiene saldo- pero conviene tener claro que se esta aceptando:
        //
        //   Se expone entre sedes el saldo, el minimo y el COSTO PROMEDIO, que es
        //   dato comercial. Si algun dia el costo no debe cruzar de sede, lo que
        //   hay que hacer es vaciarlo en el DTO cuando la fila no sea de la sede
        //   de quien pregunta, NO volver a filtrar la consulta entera.
        //
        // ESTO NO AFECTA A NADIE MAS. El resto de consultas del sistema -lotes,
        // movimientos, ventas, compras, traslados, tablero- siguen pasando por
        // ResolverFiltroSucursal y siguen devolviendo 403 ante una sede ajena.
        // Y ESCRIBIR sigue acotado a la sede propia: ver no es tocar.
        // ---------------------------------------------------------------------
        grupo.MapGet("/existencias", async (
                IInventarioService inventario,
                int? sucursalId,
                bool? incluirInactivas,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await inventario.ObtenerExistenciasAsync(
                sucursalId, incluirInactivas ?? false, cancellationToken)))
            .WithName("InventarioExistencias")
            .WithSummary("Saldos por sede y producto. Visible desde cualquier sede.")
            .WithDescription(
                "Sin `sucursalId` devuelve la red entera, para cualquier rol: esta consulta NO " +
                "aisla por sede, a diferencia del resto del sistema, porque ver donde hay saldo " +
                "es lo que permite pedir un traslado con criterio. " +
                "Poder verlas no da derecho a tocarlas: crear, editar y deshabilitar siguen " +
                "exigiendo que la sede sea la propia. " +
                "`incluirInactivas=true` anade las dadas de baja, que por defecto no salen.");

        grupo.MapGet("/alertas", async (
                IInventarioService inventario,
                IUsuarioContexto contexto,
                int? sucursalId,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await inventario.ObtenerAlertasStockBajoAsync(
                contexto.ResolverFiltroSucursal(sucursalId), cancellationToken)))
            .WithName("InventarioAlertas")
            .WithSummary("Productos por debajo del minimo")
            .WithDescription("Criterio: cantidad_base <= stock_minimo. Los mas criticos primero.");

        grupo.MapGet("/movimientos", async (
                IInventarioService inventario,
                IUsuarioContexto contexto,
                int? sucursalId,
                int? productoId,
                int? limite,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await inventario.ObtenerMovimientosAsync(
                contexto.ResolverFiltroSucursal(sucursalId),
                productoId,
                limite ?? 100,
                cancellationToken)))
            .WithName("InventarioMovimientos")
            .WithSummary("Libro mayor, del mas reciente al mas antiguo")
            .WithDescription("`limite` se acota entre 1 y 1000.");

        // ---------------------------------------------------------------------
        // Lotes
        //
        // OJO, LA RUTA /lotes CAMBIO DE SIGNIFICADO. Antes era la cola FEFO de un
        // producto en una sede, con los dos parametros obligatorios; ahora es el
        // listado general, con los dos opcionales. La consulta anterior no se
        // perdio: vive en /lotes/fefo, con el mismo comportamiento.
        //
        // El motivo es que "listar los lotes de mi sede" es la consulta que se
        // hace todos los dias, y exigir un producto para verla la volvia
        // inservible como listado.
        // ---------------------------------------------------------------------
        grupo.MapGet("/lotes", async (
                IInventarioService inventario,
                IUsuarioContexto contexto,
                int? sucursalId,
                int? productoId,
                bool? soloConSaldo,
                int? limite,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await inventario.ObtenerLotesAsync(
                contexto.ResolverFiltroSucursal(sucursalId),
                productoId,
                soloConSaldo ?? false,
                limite ?? 200,
                cancellationToken)))
            .WithName("InventarioLotes")
            .WithSummary("Lotes de una sede o de la red, en orden FEFO")
            .WithDescription(
                "Un gerente u operador ve solo su sede, omita o no el parametro. " +
                "`soloConSaldo=true` deja fuera los agotados; por defecto salen todos, para " +
                "que un lote recien creado -que esta en cero- se vea. `limite` se acota entre " +
                "1 y 1000.");

        grupo.MapGet("/lotes/fefo", async (
                IInventarioService inventario,
                IUsuarioContexto contexto,
                int sucursalId,
                int productoId,
                CancellationToken cancellationToken) =>
            {
                // Aqui la sede es obligatoria -un lote esta en una bodega
                // concreta- asi que se exige acceso en vez de acotar el filtro.
                contexto.ExigirAccesoASucursal(sucursalId);

                return TypedResults.Ok(await inventario.ObtenerLotesPorVencimientoAsync(
                    sucursalId, productoId, cancellationToken));
            })
            .WithName("InventarioLotesFefo")
            .WithSummary("Cola de despacho de un producto en una sede")
            .WithDescription(
                "El primero de la lista es el que se debe despachar: vence antes. Los que no " +
                "caducan van al final. Solo trae lotes con saldo mayor que cero. Es la misma " +
                "consulta que usan por dentro las ventas y los traslados para escoger lote.");

        grupo.MapGet("/lotes/proximos-a-vencer", async (
                IInventarioService inventario,
                IUsuarioContexto contexto,
                int? sucursalId,
                int? dias,
                bool? incluirVencidos,
                int? limite,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await inventario.ObtenerLotesProximosAVencerAsync(
                contexto.ResolverFiltroSucursal(sucursalId),
                dias,
                incluirVencidos ?? true,
                limite ?? 200,
                cancellationToken)))
            .WithName("InventarioLotesProximosAVencer")
            .WithSummary("Alertas de caducidad")
            .WithDescription(
                "Lotes CON SALDO que vencen el dia `hoy + dias` o antes. `dias` omitido usa el " +
                "umbral configurado en `AlertasInventario:DiasUmbralVencimiento`; fuera de " +
                "[1, 365] se acota. " +
                "Los lotes YA VENCIDOS con existencias entran por defecto y salen de primeros, " +
                "con `diasParaVencer` negativo y `vencido: true`: son el caso mas urgente, " +
                "porque ya no se pueden despachar. " +
                "`incluirVencidos=false` los deja fuera y acota el rango a hoy en adelante, " +
                "para la pregunta separada de que esta por vencerse. " +
                "Mismo criterio que el tablero.");

        grupo.MapGet("/lotes/{id:int}", async (
                int id,
                IInventarioService inventario,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                var lote = await inventario.ObtenerLotePorIdAsync(id, cancellationToken);

                if (lote is null)
                {
                    return RespuestasHttp.Fallo(
                        StatusCodes.Status404NotFound,
                        "Lote no encontrado",
                        $"No existe el lote {id}.");
                }

                // La comprobacion de sede va DESPUES de leer, porque hasta no
                // leerlo no se sabe en que bodega esta. Tiene una consecuencia
                // que conviene tener presente: un 403 aqui confirma que ese id
                // existe, aunque sea de otra sede. Es el mismo comportamiento del
                // resto de modulos y se acepta porque los ids son correlativos y
                // no revelan nada que no se adivine contando.
                contexto.ExigirAccesoASucursal(lote.SucursalId);

                return Results.Ok(lote);
            })
            .WithName("InventarioLoteDetalle")
            .WithSummary("Detalle de un lote")
            .Produces<LoteDto>();

        // ---------------------------------------------------------------------
        // Escritura
        // ---------------------------------------------------------------------
        // AQUI VIVIA POST /movimientos, el ajuste manual de stock. Se quito para
        // TODOS los roles, supervision incluida, y conviene dejar escrito por que.
        //
        // Era la unica forma de cambiar el stock SIN un documento detras. Todo lo
        // demas tiene uno: lo que entra viene de una orden de compra recibida, lo
        // que sale de una venta, y lo que cambia de sede de un traslado. El
        // movimiento a mano era la puerta por la que un saldo podia cuadrarse sin
        // que nada explicara de donde salio la diferencia, que es tambien la
        // puerta por la que se tapa un faltante.
        //
        // NO ES SOLO QUITAR EL BOTON. La pantalla ya no lo ofrece, pero la ruta
        // seguia abierta a cualquiera con un token de supervision y un cliente
        // HTTP. Una regla que solo vive en la interfaz no es una regla.
        //
        // QUE SE PIERDE, para que se pueda revertir a sabiendas: ya no hay forma
        // de registrar una merma, una devolucion ni un ajuste por conteo fisico.
        // Si aparece esa necesidad, lo suyo no es reabrir esta ruta tal cual sino
        // darle su propio flujo con motivo obligatorio y su documento, como
        // tienen las otras tres vias.
        //
        // InventarioService.RegistrarMovimientoAsync SIGUE EXISTIENDO, sin nadie
        // que lo llame. Se deja a proposito -es la pieza sobre la que se montaria
        // ese flujo- pero conviene saber que ya no es alcanzable desde fuera: la
        // unica puerta era esta.
        //
        // GET /movimientos se queda: leer el libro mayor no lo escribe.

        // ---------------------------------------------------------------------
        // Lotes: alta y correccion
        //
        // SUPERVISION LAS DOS, y aqui si estaba en la lista pedida. El motivo de
        // fondo es el mismo que el del movimiento manual: un lote es la ficha con
        // la que se rastrea la mercancia hasta el fabricante, y su fecha de
        // caducidad es la que decide que se despacha primero y que se da de baja.
        // Poder correr esa fecha a mano es poder alargarle la vida a un producto
        // vencido en el papel.
        //
        // Ninguna de las dos cambia cantidades: para eso esta el movimiento.
        // ---------------------------------------------------------------------
        // AQUI VIVIA POST /lotes, que abria un lote vacio a mano. Se quito, y
        // conviene dejar escrito por que, porque el hueco se nota.
        //
        // UN LOTE NO ES UN DATO NUESTRO. El numero y el vencimiento los pone el
        // fabricante y llegan impresos en el envase; no se sabe cuales son hasta
        // que el camion descarga. Teclearlos por adelantado solo podia producir
        // dos cosas: lotes inventados que no coinciden con ninguna caja, o lotes
        // vacios esperando una mercancia que quiza llegue con otro numero.
        //
        // POR DONDE NACEN AHORA, que es por donde nacian de verdad igualmente:
        //
        //   - la recepcion de una compra, con el numero que trae la factura;
        //   - la recepcion de un traslado, que recrea en el destino el lote que
        //     salio del origen, con su mismo numero y vencimiento.
        //
        // Las dos los crean desde el servicio, no por este endpoint, asi que
        // quitarlo no deja nada sin cubrir. Editar un lote -PUT, aqui abajo- si
        // sigue: corregir una fecha mal leida es otra cosa que inventarla.

        grupo.MapPut("/lotes/{id:int}", async (
                int id,
                ActualizarLoteDto peticion,
                IInventarioService inventario,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                // Hay que saber en que sede esta ANTES de dejar editarlo, y eso
                // exige leerlo. Una lectura de mas frente a permitir que un
                // gerente corrija la caducidad de un lote de otra sede.
                var actual = await inventario.ObtenerLotePorIdAsync(id, cancellationToken);

                if (actual is null)
                {
                    return RespuestasHttp.Fallo(
                        StatusCodes.Status404NotFound,
                        "Lote no encontrado",
                        $"No existe el lote {id}.");
                }

                contexto.ExigirAccesoASucursal(actual.SucursalId);

                var resultado = await inventario.ActualizarLoteAsync(
                    id, peticion, contexto.UsuarioIdRequerido(), cancellationToken);

                return resultado.Exito
                    ? Results.Ok(resultado)
                    : RespuestasHttp.Fallo(
                        CodigoDe(resultado.Error), "Lote rechazado", resultado.Mensaje);
            })
            .WithName("InventarioActualizarLote")
            .WithSummary("Corrige el numero o la caducidad de un lote. Solo supervision.")
            .WithDescription(
                "Solo se corrige lo que se digito. La cantidad no se toca por aqui -eso es un " +
                "movimiento de ajuste, que ademas queda en el libro mayor- y el producto y la " +
                "sede tampoco, porque mover un lote de bodega es un traslado. " +
                "Es un PUT: el cuerpo describe como debe quedar el lote, asi que un " +
                "`fechaVencimiento` nulo BORRA la fecha en vez de dejarla como estaba. " +
                "El valor anterior y el nuevo quedan en auditoria_eventos.")
            .Produces<ResultadoLote>()
            .RequireAuthorization(PoliticasAutorizacion.Supervision);

        // ---------------------------------------------------------------------
        // Altas, bajas y edicion de existencias
        //
        // SUPERVISION LAS CUATRO, y ademas sobre la sede propia. Son las dos
        // comprobaciones del sistema y ninguna sustituye a la otra: la politica
        // dice QUE clase de operacion puedes hacer, ExigirAccesoASucursal dice
        // SOBRE QUE sede. Un gerente de Manizales pasa la politica y aun asi no
        // deshabilita un producto del Eje Cafetero.
        //
        // Queda fuera el Operador. Habilitar o retirar un producto de una sede es
        // una decision de surtido, de quien responde por el resultado de la sede,
        // no de quien ejecuta el dia a dia. Mismo criterio que el movimiento
        // manual y que el alta de lotes.
        //
        // NO HAY DELETE DE VERDAD. `DELETE /existencias/{id}` hace baja logica:
        // ver la migracion 12 y ErrorExistencia.TieneSaldo. Se mantiene el verbo
        // DELETE porque es lo que expresa la intencion de quien llama -retirar el
        // producto de esa sede- y el efecto observable es ese; lo que no ocurre
        // es la perdida de la historia.
        // ---------------------------------------------------------------------
        grupo.MapPost("/existencias", async (
                CrearExistenciaDto peticion,
                IInventarioService inventario,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                contexto.ExigirAccesoASucursal(peticion.SucursalId);

                var resultado = await inventario.CrearExistenciaAsync(
                    peticion, contexto.UsuarioIdRequerido(), cancellationToken);

                return resultado.Exito
                    ? Results.Created(
                        $"/api/inventario/existencias/{resultado.Existencia!.Id}", resultado)
                    : RespuestasHttp.Fallo(
                        CodigoDeExistencia(resultado.Error), "Existencia rechazada", resultado.Mensaje);
            })
            .WithName("InventarioCrearExistencia")
            .WithSummary("Habilita un producto en una sede. Solo supervision, y solo en tu sede.")
            .WithDescription(
                "NO recibe cantidad, por el mismo motivo que el alta de lotes: el saldo solo se " +
                "mueve por el libro mayor, donde cada asiento dice de donde salio la mercancia. " +
                "La existencia nace en CERO y se llena con una recepcion de compra o con un " +
                "traslado, que son las dos unicas vias que la suben. " +
                "Si la pareja (sede, producto) ya existe pero esta deshabilitada, la REACTIVA con " +
                "el saldo que tenia en vez de fallar: el indice unico no deja crear otra fila, " +
                "asi que negarse dejaria a la persona sin salida.")
            .Produces<ResultadoExistencia>(StatusCodes.Status201Created)
            .RequireAuthorization(PoliticasAutorizacion.Supervision);

        grupo.MapPut("/existencias/{id:int}", async (
                int id,
                ActualizarExistenciaDto peticion,
                IInventarioService inventario,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                var actual = await inventario.ObtenerExistenciaPorIdAsync(id, cancellationToken);
                if (actual is null)
                {
                    return RespuestasHttp.Fallo(
                        StatusCodes.Status404NotFound,
                        "Existencia no encontrada",
                        $"No existe la existencia {id}.");
                }

                contexto.ExigirAccesoASucursal(actual.SucursalId);

                var resultado = await inventario.ActualizarExistenciaAsync(
                    id, peticion, contexto.UsuarioIdRequerido(), cancellationToken);

                return resultado.Exito
                    ? Results.Ok(resultado)
                    : RespuestasHttp.Fallo(
                        CodigoDeExistencia(resultado.Error), "Existencia rechazada", resultado.Mensaje);
            })
            .WithName("InventarioActualizarExistencia")
            .WithSummary("Cambia el minimo de reposicion. Solo supervision, y solo en tu sede.")
            .WithDescription(
                "Solo el minimo. La cantidad la mueve el libro mayor y el costo promedio lo " +
                "recalcula cada entrada de mercancia; ninguno de los dos se digita. " +
                "El valor anterior y el nuevo quedan en auditoria_eventos.")
            .Produces<ResultadoExistencia>()
            .RequireAuthorization(PoliticasAutorizacion.Supervision);

        grupo.MapDelete("/existencias/{id:int}", async (
                int id,
                IInventarioService inventario,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                var actual = await inventario.ObtenerExistenciaPorIdAsync(id, cancellationToken);
                if (actual is null)
                {
                    return RespuestasHttp.Fallo(
                        StatusCodes.Status404NotFound,
                        "Existencia no encontrada",
                        $"No existe la existencia {id}.");
                }

                contexto.ExigirAccesoASucursal(actual.SucursalId);

                var resultado = await inventario.DesactivarExistenciaAsync(
                    id, contexto.UsuarioIdRequerido(), cancellationToken);

                return resultado.Exito
                    ? Results.Ok(resultado)
                    : RespuestasHttp.Fallo(
                        CodigoDeExistencia(resultado.Error), "Existencia rechazada", resultado.Mensaje);
            })
            .WithName("InventarioDesactivarExistencia")
            .WithSummary("BAJA LOGICA: deshabilita el producto en esa sede. No borra nada.")
            .WithDescription(
                "La fila se queda en la base con su saldo, sus lotes y todo el libro mayor que la " +
                "referencia; lo unico que cambia es que deja de listarse y de alertar. " +
                "SE NIEGA CON 409 SI TODAVIA TIENE SALDO: esconder una fila con mercancia dentro " +
                "haria que la suma de las existencias dejara de cuadrar con la bodega sin que " +
                "ningun asiento lo explique. Saca primero el saldo con un traslado, una venta o un " +
                "ajuste. " +
                "Se deshace con POST /api/inventario/existencias/{id}/reactivar.")
            .Produces<ResultadoExistencia>()
            .RequireAuthorization(PoliticasAutorizacion.Supervision);

        grupo.MapPost("/existencias/{id:int}/reactivar", async (
                int id,
                IInventarioService inventario,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                var actual = await inventario.ObtenerExistenciaPorIdAsync(id, cancellationToken);
                if (actual is null)
                {
                    return RespuestasHttp.Fallo(
                        StatusCodes.Status404NotFound,
                        "Existencia no encontrada",
                        $"No existe la existencia {id}.");
                }

                contexto.ExigirAccesoASucursal(actual.SucursalId);

                var resultado = await inventario.ReactivarExistenciaAsync(
                    id, contexto.UsuarioIdRequerido(), cancellationToken);

                return resultado.Exito
                    ? Results.Ok(resultado)
                    : RespuestasHttp.Fallo(
                        CodigoDeExistencia(resultado.Error), "Existencia rechazada", resultado.Mensaje);
            })
            .WithName("InventarioReactivarExistencia")
            .WithSummary("Deshace la baja logica. Solo supervision, y solo en tu sede.")
            .WithDescription(
                "Vuelve a listar el producto en esa sede, con el saldo que tuviera. " +
                "Es lo que hace que la baja no sea un camino sin retorno desde la interfaz.")
            .Produces<ResultadoExistencia>()
            .RequireAuthorization(PoliticasAutorizacion.Supervision);

        return rutas;
    }

    private static int CodigoDeExistencia(ErrorExistencia error) => error switch
    {
        ErrorExistencia.ProductoNoEncontrado
            or ErrorExistencia.SucursalNoEncontrada
            or ErrorExistencia.ExistenciaNoEncontrada
            => StatusCodes.Status404NotFound,

        // 409 los dos: la peticion esta bien formada y quien la manda tiene
        // permiso; lo que no admite la operacion es el estado actual de los
        // datos. Un 400 diria que el cuerpo esta mal, que no es el caso.
        ErrorExistencia.ExistenciaDuplicada
            or ErrorExistencia.TieneSaldo
            or ErrorExistencia.EstadoSinCambio
            => StatusCodes.Status409Conflict,

        _ => StatusCodes.Status400BadRequest
    };

    private static int CodigoDe(ErrorLote error) => error switch
    {
        ErrorLote.ProductoNoEncontrado
            or ErrorLote.SucursalNoEncontrada
            or ErrorLote.LoteNoEncontrado
            => StatusCodes.Status404NotFound,

        // Existe, pero el estado actual de los datos no admite la operacion.
        ErrorLote.NumeroLoteDuplicado => StatusCodes.Status409Conflict,

        _ => StatusCodes.Status400BadRequest
    };

    private static int CodigoDe(ErrorMovimiento error) => error switch
    {
        ErrorMovimiento.ProductoNoEncontrado
            or ErrorMovimiento.UnidadNoEncontrada
            or ErrorMovimiento.LoteNoEncontrado
            or ErrorMovimiento.SaldoNoEncontrado
            => StatusCodes.Status404NotFound,

        ErrorMovimiento.StockInsuficiente
            or ErrorMovimiento.StockLoteInsuficiente
            => StatusCodes.Status409Conflict,

        _ => StatusCodes.Status400BadRequest
    };
}
