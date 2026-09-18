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
        grupo.MapGet("/existencias", async (
                IInventarioService inventario,
                IUsuarioContexto contexto,
                int? sucursalId,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await inventario.ObtenerExistenciasAsync(
                contexto.ResolverFiltroSucursal(sucursalId), cancellationToken)))
            .WithName("InventarioExistencias")
            .WithSummary("Saldos por sede y producto")
            .WithDescription(
                "Un gerente u operador ve solo su sede, omita o no el parametro. El administrador " +
                "ve la red entera si no lo manda.");

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
        grupo.MapPost("/movimientos", async (
                RegistrarMovimientoDto peticion,
                IInventarioService inventario,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                contexto.ExigirAccesoASucursal(peticion.SucursalId);

                var resultado = await inventario.RegistrarMovimientoAsync(
                    peticion, contexto.UsuarioIdRequerido(), cancellationToken);

                return resultado.Exito
                    ? Results.Ok(resultado)
                    : RespuestasHttp.Fallo(
                        CodigoDe(resultado.Error), "Movimiento rechazado", resultado.Mensaje);
            })
            .WithName("InventarioRegistrarMovimiento")
            .WithSummary("Registra una entrada o salida de stock. Solo supervision.")
            .WithDescription(
                "La cantidad va en la unidad que tenga a mano el operario; el servicio convierte " +
                "a la unidad base del producto. El responsable NO se manda en el cuerpo: sale del " +
                "token. Un retiro que deje el saldo en negativo se rechaza con 409. " +
                "Restringido a Administrador General y Gerente de Sucursal.")
            .Produces<ResultadoMovimiento>()
            // Supervision, y esta no venia en la lista que se pidio: es una
            // inferencia, explicada aqui para que se pueda discutir.
            //
            // Lo que entra por compras, sale por ventas o se mueve por traslados
            // tiene su propio flujo y su propio documento detras. Un movimiento
            // registrado a mano es lo que queda: un ajuste, una merma, una
            // devolucion. Es decir, la unica forma de cambiar el stock SIN un
            // documento que lo respalde, y por eso mismo la via por la que se
            // tapa un faltante.
            //
            // Si en la operacion real el operador es quien reporta la merma
            // -que es razonable, es quien ve el envase roto- esto hay que
            // abrirlo; pero conviene que sea una decision tomada, no un descuido.
            .RequireAuthorization(PoliticasAutorizacion.Supervision);

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
        grupo.MapPost("/lotes", async (
                CrearLoteDto peticion,
                IInventarioService inventario,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                contexto.ExigirAccesoASucursal(peticion.SucursalId);

                var resultado = await inventario.CrearLoteAsync(
                    peticion, contexto.UsuarioIdRequerido(), cancellationToken);

                return resultado.Exito
                    ? Results.Created($"/api/inventario/lotes/{resultado.Lote!.Id}", resultado)
                    : RespuestasHttp.Fallo(
                        CodigoDe(resultado.Error), "Lote rechazado", resultado.Mensaje);
            })
            .WithName("InventarioCrearLote")
            .WithSummary("Abre un lote nuevo, vacio. Solo supervision.")
            .WithDescription(
                "NO recibe cantidad, a proposito: el saldo de una sede vive a la vez en el " +
                "consolidado y en el desglose por lote, y crear un lote con cantidad lo " +
                "sumaria solo al desglose, sin fila en el libro mayor que diga de donde salio. " +
                "El lote nace en cero y la mercancia entra con POST /api/inventario/movimientos " +
                "indicando `loteId`, o con una recepcion de compra. " +
                "El numero de lote es unico dentro de la pareja (producto, sede): si vuelve a " +
                "llegar el mismo, se le suma cantidad en vez de abrir otro.")
            .Produces<ResultadoLote>(StatusCodes.Status201Created)
            .RequireAuthorization(PoliticasAutorizacion.Supervision);

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

        return rutas;
    }

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
