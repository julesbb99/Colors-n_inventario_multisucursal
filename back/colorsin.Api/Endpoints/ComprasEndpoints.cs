using Colorsin.Api.Auth;
using Colorsin.Application.Compras.DTOs;
using Colorsin.Application.Compras.Services;
using Colorsin.Application.Comun.Auth;
using Colorsin.Domain.Compras;

namespace Colorsin.Api.Endpoints;

/// <summary>
/// Endpoints de compras: proveedores, ordenes y recepcion de mercancia.
///
/// LAS DOS FORMAS DE COMPROBAR LA SEDE. Crear una orden trae la sede en el
/// cuerpo, asi que se comprueba directamente. Recibir una entrega solo trae el
/// id de la orden, y la sede esta EN la orden: hay que leerla antes para poder
/// comprobarla. Ese es el motivo de la consulta previa en la recepcion; sin
/// ella, cualquiera podria ingresar mercancia a la bodega de otra sede con solo
/// saber el numero de orden.
///
/// La carrera entre esa lectura y la operacion es inofensiva: la sede de una
/// orden no cambia nunca, y el servicio vuelve a bloquear la fila por su cuenta.
/// </summary>
public static class ComprasEndpoints
{
    /// <summary>Registra el grupo <c>/api/compras</c>.</summary>
    public static IEndpointRouteBuilder MapComprasEndpoints(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/compras")
            .WithTags("Compras")
            .RequireAuthorization();

        // ---------------------------------------------------------------------
        // Proveedores
        // ---------------------------------------------------------------------
        grupo.MapGet("/proveedores", async (
                IComprasService compras,
                bool? incluirInactivos,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await compras.ObtenerProveedoresAsync(
                incluirInactivos ?? false, cancellationToken)))
            .WithName("ComprasProveedores")
            .WithSummary("Catalogo de proveedores")
            .WithDescription(
                "No se filtra por sede: el catalogo de proveedores es de toda la red, no de una " +
                "bodega. Lo que si es de una sede es la orden que se le hace. " +
                "Por defecto solo los activos; `incluirInactivos=true` anade los retirados.");

        grupo.MapGet("/proveedores/{id:int}", async (
                int id,
                IComprasService compras,
                CancellationToken cancellationToken) =>
            await compras.ObtenerProveedorPorIdAsync(id, cancellationToken) is { } proveedor
                ? Results.Ok(proveedor)
                : Results.NotFound())
            .WithName("ComprasProveedorPorId")
            .WithSummary("Un proveedor")
            .Produces<ProveedorDto>()
            .Produces(StatusCodes.Status404NotFound);

        // ---------------------------------------------------------------------
        // Proveedores: alta, edicion y retiro. SOLO ADMINISTRADOR GENERAL.
        //
        // Otra politica, no supervision con un rol menos. El motivo esta en
        // PoliticasAutorizacion.SoloAdminGeneral y vale la pena repetirlo: el
        // catalogo de proveedores y su lista de precios son COMPARTIDOS por las
        // tres sedes, asi que aqui no hay ninguna sede sobre la que comprobar
        // nada -por eso no se llama a ExigirAccesoASucursal- y un cambio lo
        // hereda toda la red. Un gerente responde por su sede; esto se le sale.
        // ---------------------------------------------------------------------
        grupo.MapPost("/proveedores", async (
                GuardarProveedorDto peticion,
                IComprasService compras,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                var resultado = await compras.CrearProveedorAsync(
                    peticion, contexto.UsuarioIdRequerido(), cancellationToken);

                return resultado.Exito
                    ? Results.Created(
                        $"/api/compras/proveedores/{resultado.Proveedor!.Id}", resultado)
                    : RespuestasHttp.Fallo(
                        CodigoDe(resultado.Error), "Proveedor rechazado", resultado.Mensaje);
            })
            .WithName("ComprasCrearProveedor")
            .WithSummary("Da de alta un proveedor. Solo Administrador General.")
            .WithDescription(
                "El nombre es unico, sin distinguir mayusculas. Si ya existe pero esta retirado, " +
                "la respuesta invita a reactivarlo en vez de crear otro: asi conserva su lista " +
                "de precios y su historial de ordenes.")
            .Produces<ResultadoProveedor>(StatusCodes.Status201Created)
            .RequireAuthorization(PoliticasAutorizacion.SoloAdminGeneral);

        grupo.MapPut("/proveedores/{id:int}", async (
                int id,
                GuardarProveedorDto peticion,
                IComprasService compras,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                var resultado = await compras.ActualizarProveedorAsync(
                    id, peticion, contexto.UsuarioIdRequerido(), cancellationToken);

                return resultado.Exito
                    ? Results.Ok(resultado)
                    : RespuestasHttp.Fallo(
                        CodigoDe(resultado.Error), "Proveedor rechazado", resultado.Mensaje);
            })
            .WithName("ComprasActualizarProveedor")
            .WithSummary("Cambia nombre, contacto o telefono. Solo Administrador General.")
            .Produces<ResultadoProveedor>()
            .RequireAuthorization(PoliticasAutorizacion.SoloAdminGeneral);

        grupo.MapDelete("/proveedores/{id:int}", async (
                int id,
                IComprasService compras,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                var resultado = await compras.DesactivarProveedorAsync(
                    id, contexto.UsuarioIdRequerido(), cancellationToken);

                return resultado.Exito
                    ? Results.Ok(resultado)
                    : RespuestasHttp.Fallo(
                        CodigoDe(resultado.Error), "Proveedor rechazado", resultado.Mensaje);
            })
            .WithName("ComprasRetirarProveedor")
            .WithSummary("BAJA LOGICA: retira al proveedor del catalogo. No borra nada.")
            .WithDescription(
                "Deja de ofrecerse al crear ordenes, pero conserva sus ordenes historicas y su " +
                "lista de precios. NO existe borrado real, y no es una decision de estilo: " +
                "`ordenes_compra` lo referencia con RESTRICT -MySQL rechazaria el borrado en " +
                "cuanto tenga una orden- y `producto_proveedor` con CASCADE, que se llevaria su " +
                "lista de precios por delante. Se deshace con .../reactivar.")
            .Produces<ResultadoProveedor>()
            .RequireAuthorization(PoliticasAutorizacion.SoloAdminGeneral);

        grupo.MapPost("/proveedores/{id:int}/reactivar", async (
                int id,
                IComprasService compras,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                var resultado = await compras.ReactivarProveedorAsync(
                    id, contexto.UsuarioIdRequerido(), cancellationToken);

                return resultado.Exito
                    ? Results.Ok(resultado)
                    : RespuestasHttp.Fallo(
                        CodigoDe(resultado.Error), "Proveedor rechazado", resultado.Mensaje);
            })
            .WithName("ComprasReactivarProveedor")
            .WithSummary("Deshace el retiro. Solo Administrador General.")
            .Produces<ResultadoProveedor>()
            .RequireAuthorization(PoliticasAutorizacion.SoloAdminGeneral);

        // ---------------------------------------------------------------------
        // Lista de precios
        // ---------------------------------------------------------------------
        grupo.MapGet("/precios", async (
                int productoId,
                int proveedorId,
                IComprasService compras,
                CancellationToken cancellationToken) =>
            await compras.ObtenerPrecioReferenciaAsync(
                productoId, proveedorId, cancellationToken) is { } precio
                ? Results.Ok(precio)
                : Results.NotFound())
            .WithName("ComprasPrecioReferencia")
            .WithSummary("Precio de lista y ultimo precio pagado de un producto a un proveedor")
            .WithDescription(
                "DEVUELVE DOS COSAS DISTINTAS Y NO LAS MEZCLA. `precioReferencia` es lo pactado " +
                "en la lista, POR UNIDAD BASE del producto (`producto_proveedor` no tiene columna " +
                "de unidad). `ultimaCompra` es lo que de verdad se cobro la ultima vez, CON LA " +
                "UNIDAD EN QUE SE COTIZO y sin normalizar, para que la cifra coincida con la de " +
                "la factura. Que no cuadren es justamente la informacion util. " +
                "Mira el historico de TODA LA RED: lo que cobra un proveedor no depende de a que " +
                "bodega entrega. Se ignoran las ordenes canceladas y las lineas sin precio. " +
                "Cualquier rol autenticado puede consultarlo: es lo que hace falta para pedir " +
                "con criterio.")
            .Produces<PrecioReferenciaDto>()
            .Produces(StatusCodes.Status404NotFound);

        grupo.MapPut("/proveedores/{proveedorId:int}/precios/{productoId:int}", async (
                int proveedorId,
                int productoId,
                GuardarPrecioReferenciaDto peticion,
                IComprasService compras,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                var resultado = await compras.GuardarPrecioReferenciaAsync(
                    proveedorId, productoId, peticion,
                    contexto.UsuarioIdRequerido(), cancellationToken);

                return resultado.Exito
                    ? Results.Ok(resultado)
                    : RespuestasHttp.Fallo(
                        CodigoDe(resultado.Error), "Precio rechazado", resultado.Mensaje);
            })
            .WithName("ComprasGuardarPrecioReferencia")
            .WithSummary("Fija el precio de lista de un producto. Solo Administrador General.")
            .WithDescription(
                "El precio va POR UNIDAD BASE del producto. Un `precioReferencia` nulo QUITA el " +
                "producto de la lista de ese proveedor, sin tocar ninguna orden historica: las " +
                "lineas guardan su propio precio, no una referencia a esta tabla.")
            .Produces<ResultadoProveedor>()
            .RequireAuthorization(PoliticasAutorizacion.SoloAdminGeneral);

        // ---------------------------------------------------------------------
        // Ordenes
        // ---------------------------------------------------------------------
        grupo.MapGet("/ordenes", async (
                IComprasService compras,
                IUsuarioContexto contexto,
                int? sucursalId,
                int? proveedorId,
                EstadoOrdenCompra? estado,
                int? limite,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await compras.ObtenerOrdenesAsync(
                contexto.ResolverFiltroSucursal(sucursalId),
                proveedorId,
                estado,
                limite ?? 100,
                cancellationToken)))
            .WithName("ComprasOrdenes")
            .WithSummary("Ordenes de compra, sin detalle")
            .WithDescription(
                "Estados: Pendiente, Confirmada, ParcialmenteRecibida, Recibida, Cancelada. " +
                "Para ver las lineas, consultar la orden por id.");

        grupo.MapGet("/ordenes/{id:int}", async (
                int id,
                IComprasService compras,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                var orden = await compras.ObtenerOrdenPorIdAsync(id, cancellationToken);

                if (orden is null)
                {
                    return Results.NotFound();
                }

                // Se comprueba DESPUES de leerla, porque hasta leerla no se sabe
                // de que sede es. Un 403 aqui confirma que la orden existe, y es
                // un mal menor asumido: la alternativa -devolver 404- obligaria a
                // mentir sobre lo que se acaba de leer.
                contexto.ExigirAccesoASucursal(orden.SucursalId);

                return Results.Ok(orden);
            })
            .WithName("ComprasOrdenPorId")
            .WithSummary("Una orden con su detalle")
            .Produces<OrdenCompraDto>()
            .Produces(StatusCodes.Status404NotFound);

        grupo.MapPost("/ordenes", async (
                CrearOrdenCompraDto peticion,
                IComprasService compras,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                contexto.ExigirAccesoASucursal(peticion.SucursalId);

                var resultado = await compras.CrearOrdenAsync(
                    peticion, contexto.UsuarioIdRequerido(), cancellationToken);

                return resultado.Exito
                    ? Results.Ok(resultado)
                    : RespuestasHttp.Fallo(
                        CodigoDe(resultado.Error), "Orden rechazada", resultado.Mensaje);
            })
            .WithName("ComprasCrearOrden")
            .WithSummary("Crea una orden en estado Pendiente. Cualquier rol, en su sede.")
            .WithDescription(
                "No mueve stock: una orden es una intencion de compra. Las existencias cambian al " +
                "confirmar la recepcion. Quien la crea sale del token. " +
                "ABIERTO AL OPERADOR: una orden Pendiente es un borrador, no un compromiso. " +
                "La sede si se comprueba: solo se pide para la propia.")
            .Produces<ResultadoOrdenCompra>();

        // ---------------------------------------------------------------------
        // Editar y retirar: SOLO MIENTRAS LA ORDEN SEA UN BORRADOR
        //
        // Las dos exigen estado 'Pendiente', y esa comprobacion vive en el
        // SERVICIO, no aqui: es una regla de negocio sobre el ciclo de vida del
        // documento, y si estuviera en el endpoint una via de entrada nueva
        // podria saltarsela. El endpoint solo comprueba la sede, que es lo suyo.
        //
        // Hay que leer la orden antes para saber de que sede es. Misma razon que
        // en la recepcion: el id de la orden no dice nada de la bodega.
        // ---------------------------------------------------------------------
        grupo.MapPut("/ordenes/{id:int}", async (
                int id,
                CrearOrdenCompraDto peticion,
                IComprasService compras,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                var actual = await compras.ObtenerOrdenPorIdAsync(id, cancellationToken);
                if (actual is null)
                {
                    return RespuestasHttp.Fallo(
                        StatusCodes.Status404NotFound,
                        "Orden no encontrada",
                        $"No existe la orden {id}.");
                }

                contexto.ExigirAccesoASucursal(actual.SucursalId);

                var resultado = await compras.ActualizarOrdenAsync(
                    id, peticion, contexto.UsuarioIdRequerido(), cancellationToken);

                return resultado.Exito
                    ? Results.Ok(resultado)
                    : RespuestasHttp.Fallo(
                        CodigoDe(resultado.Error), "Orden rechazada", resultado.Mensaje);
            })
            .WithName("ComprasActualizarOrden")
            .WithSummary("Edita una orden Pendiente. Cualquier rol, en su sede.")
            .WithDescription(
                "REEMPLAZA las lineas: las que van en el cuerpo sustituyen a las que habia, que " +
                "es lo que significa un PUT. Cambia proveedor, plazo y lineas; NO cambia la sede " +
                "-mover una orden de bodega es otra orden- ni quien la creo. " +
                "Responde 409 si la orden ya salio de 'Pendiente': desde ahi hay un compromiso " +
                "con el proveedor y, si entro mercancia, movimientos en el libro mayor que la " +
                "citan.")
            .Produces<ResultadoOrdenCompra>();

        grupo.MapDelete("/ordenes/{id:int}", async (
                int id,
                IComprasService compras,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                var actual = await compras.ObtenerOrdenPorIdAsync(id, cancellationToken);
                if (actual is null)
                {
                    return RespuestasHttp.Fallo(
                        StatusCodes.Status404NotFound,
                        "Orden no encontrada",
                        $"No existe la orden {id}.");
                }

                contexto.ExigirAccesoASucursal(actual.SucursalId);

                var resultado = await compras.CancelarOrdenAsync(
                    id, contexto.UsuarioIdRequerido(), cancellationToken);

                return resultado.Exito
                    ? Results.Ok(resultado)
                    : RespuestasHttp.Fallo(
                        CodigoDe(resultado.Error), "Orden rechazada", resultado.Mensaje);
            })
            .WithName("ComprasCancelarOrden")
            .WithSummary("Retira una orden Pendiente pasandola a Cancelada. No borra nada.")
            .WithDescription(
                "El estado 'Cancelada' ya existia en el esquema para esto. La fila se conserva " +
                "con su detalle, que es lo que permite responder despues a 'quien pidio esto y " +
                "por que no llego'; la interfaz la esconde del listado del dia. " +
                "Responde 409 si la orden ya fue confirmada o recibida: a partir de ahi es el " +
                "documento que respalda lo que entro a la bodega.")
            .Produces<ResultadoOrdenCompra>();

        // ---------------------------------------------------------------------
        // Recepcion
        // ---------------------------------------------------------------------
        grupo.MapPost("/ordenes/{id:int}/recepciones", async (
                int id,
                ConfirmarRecepcionDto peticion,
                IComprasService compras,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                var orden = await compras.ObtenerOrdenPorIdAsync(id, cancellationToken);

                if (orden is null)
                {
                    return Results.NotFound();
                }

                contexto.ExigirAccesoASucursal(orden.SucursalId);

                // Manda el id de la ruta, no el del cuerpo. Si no coincidieran,
                // hacer caso al cuerpo significaria recibir una orden distinta de
                // la que dice la URL, que es la peor forma de resolver el empate.
                var resultado = await compras.ConfirmarRecepcionAsync(
                    peticion with { OrdenCompraId = id },
                    contexto.UsuarioIdRequerido(),
                    cancellationToken);

                return resultado.Exito
                    ? Results.Ok(resultado)
                    : RespuestasHttp.Fallo(
                        CodigoDe(resultado.Error), "Recepcion rechazada", resultado.Mensaje);
            })
            .WithName("ComprasConfirmarRecepcion")
            .WithSummary("Registra que llego mercancia de la orden. Cualquier rol, en su sede.")
            .WithDescription(
                "Es la unica operacion de compras que toca inventario: sube el saldo, anexa un " +
                "movimiento de Ingreso por linea y crea o engrosa los lotes. Admite entregas " +
                "parciales y se puede llamar varias veces: cada llamada acumula sobre lo ya " +
                "recibido. Con el cuerpo vacio se recibe todo lo que falte. " +
                "ABIERTO AL OPERADOR: contar lo que baja del camion es su trabajo. La sede si " +
                "se comprueba, y los ajustes manuales de stock siguen siendo de supervision.")
            .Produces<ResultadoRecepcion>()
            // ABIERTO AL OPERADOR, a diferencia de los ajustes de inventario.
            //
            // Recibir es ANOTAR UN HECHO: llegaron 250 de los 300 litros, y hay
            // una factura y un camion que lo respaldan. Quien lo presencia es el
            // operador. Cuando esto exigia supervision, el resultado practico era
            // que la mercancia entraba a la bodega horas o dias antes de figurar
            // en el sistema, y todo lo que se apoya en el saldo -las alertas de
            // minimo, el FEFO de los lotes- trabajaba con datos viejos.
            //
            // No se confunda con un ajuste manual de stock, que sigue cerrado al
            // operador: alli no hay hecho que anotar, se corrige la cifra a mano.
            //
            // Es su propia politica y no un rol mas en Supervision, porque
            // ampliar aquella habria abierto de paso las siete rutas de
            // inventario y las de traslados.
            .RequireAuthorization(PoliticasAutorizacion.RecepcionMercancia);

        return rutas;
    }

    private static int CodigoDe(ErrorCompra error) => error switch
    {
        ErrorCompra.ProveedorNoEncontrado
            or ErrorCompra.ProductoNoEncontrado
            or ErrorCompra.UnidadNoEncontrada
            or ErrorCompra.OrdenNoEncontrada
            or ErrorCompra.PrecioNoEncontrado
            => StatusCodes.Status404NotFound,

        // 409: la peticion esta bien formada y quien la manda tiene permiso; lo
        // que no la admite es el estado actual de los datos.
        ErrorCompra.EstadoNoPermiteRecepcion
            or ErrorCompra.NadaPorRecibir
            or ErrorCompra.EstadoNoPermiteEdicion
            or ErrorCompra.ProveedorDuplicado
            or ErrorCompra.ProveedorEstadoSinCambio
            => StatusCodes.Status409Conflict,

        _ => StatusCodes.Status400BadRequest
    };
}
