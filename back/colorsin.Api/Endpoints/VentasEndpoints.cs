using Colorsin.Api.Auth;
using Colorsin.Application.Comun.Auth;
using Colorsin.Application.Ventas;
using Colorsin.Application.Ventas.DTOs;
using Colorsin.Application.Ventas.Services;

namespace Colorsin.Api.Endpoints;

/// <summary>
/// Endpoints de ventas: clientes y registro de ventas.
///
/// ESTE MODULO LANZA EXCEPCIONES en vez de devolver un objeto de resultado, a
/// diferencia de los otros tres. No es una incoherencia que se colara: fue una
/// decision explicita al construirlo, para que una venta sin stock interrumpiera
/// el flujo en vez de poder ignorarse por descuido. La consecuencia es que la
/// traduccion a HTTP se hace con try/catch, y esta en
/// <see cref="Traducir"/>.
/// </summary>
public static class VentasEndpoints
{
    /// <summary>Registra el grupo <c>/api/ventas</c>.</summary>
    public static IEndpointRouteBuilder MapVentasEndpoints(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/ventas")
            .WithTags("Ventas")
            .RequireAuthorization();

        // ---------------------------------------------------------------------
        // Clientes
        // ---------------------------------------------------------------------
        grupo.MapGet("/clientes", async (
                IVentasService ventas,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await ventas.ObtenerClientesAsync(cancellationToken)))
            .WithName("VentasClientes")
            .WithSummary("Catalogo de clientes")
            .WithDescription(
                "Sin filtro por sede: un cliente le compra a la red, no a una bodega. Lo que si " +
                "es de una sede es la venta.");

        grupo.MapGet("/clientes/{id:int}", async (
                int id,
                IVentasService ventas,
                CancellationToken cancellationToken) =>
            await ventas.ObtenerClientePorIdAsync(id, cancellationToken) is { } cliente
                ? Results.Ok(cliente)
                : Results.NotFound())
            .WithName("VentasClientePorId")
            .WithSummary("Un cliente por id")
            .Produces<ClienteDto>()
            .Produces(StatusCodes.Status404NotFound);

        grupo.MapGet("/clientes/documento/{documento}", async (
                string documento,
                IVentasService ventas,
                CancellationToken cancellationToken) =>
            await ventas.ObtenerClientePorDocumentoAsync(documento, cancellationToken) is { } cliente
                ? Results.Ok(cliente)
                : Results.NotFound())
            .WithName("VentasClientePorDocumento")
            .WithSummary("Un cliente por cedula o NIT")
            .WithDescription(
                "Es la busqueda del mostrador: alli se tiene el documento que trae la persona, " +
                "no el id interno.")
            .Produces<ClienteDto>()
            .Produces(StatusCodes.Status404NotFound);

        grupo.MapPost("/clientes", async (
                CrearClienteDto peticion,
                IVentasService ventas,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var cliente = await ventas.CrearClienteAsync(
                        peticion, contexto.UsuarioIdRequerido(), cancellationToken);

                    // 201 con Location: el mostrador acaba de crear un recurso y
                    // la respuesta dice donde vive, con el id que asigno MySQL.
                    return Results.Created($"/api/ventas/clientes/{cliente.Id}", cliente);
                }
                catch (VentaException ex)
                {
                    return Traducir(ex);
                }
            })
            .WithName("VentasCrearCliente")
            .WithSummary("Da de alta un cliente. Cualquier rol.")
            .WithDescription(
                "ABIERTO A CUALQUIER ROL: en el mostrador aparece un cliente nuevo a diario y no " +
                "poder registrarlo significa no poder facturarle. Sin sede: un cliente le compra " +
                "a la red, no a una bodega. " +
                "Responde 409 si el documento ya existe, con el nombre y el id del que ya esta, " +
                "porque lo habitual es que la persona estuviera registrada y no se encontrara al " +
                "buscar.")
            .Produces<ClienteDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status409Conflict);

        // ---------------------------------------------------------------------
        // Precios de venta
        //
        // Viven en /api/ventas y no en /api/productos porque son una decision
        // COMERCIAL, no del catalogo: el producto existe igual sin precio.
        // ---------------------------------------------------------------------
        grupo.MapGet("/precios/{productoId:int}", async (
                int productoId,
                IVentasService ventas,
                IUsuarioContexto contexto,
                int? sucursalId,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await ventas.ObtenerPrecioVentaAsync(
                        productoId,
                        // La "ultima venta" se acota a lo que quien pregunta
                        // puede ver. Un gerente de Armenia no deberia deducir a
                        // como vende Cali a partir de este endpoint.
                        contexto.ResolverFiltroSucursal(sucursalId),
                        cancellationToken));
                }
                catch (VentaException ex)
                {
                    return Traducir(ex);
                }
            })
            .WithName("VentasPrecioDeProducto")
            .WithSummary("A cuanto se vende un producto: lista, ultima venta y costo")
            .WithDescription(
                "Tres cifras que NO se funden en una: el precio fijado y el costo van POR UNIDAD " +
                "BASE; la ultima venta va en la unidad en que se cotizo, sin normalizar, para que " +
                "coincida con la factura. El margen se calcula sobre el costo y sale NEGATIVO " +
                "cuando el precio fijado esta por debajo.")
            .Produces<PrecioVentaDto>();

        grupo.MapPut("/precios/{productoId:int}", async (
                int productoId,
                GuardarPrecioVentaDto peticion,
                IVentasService ventas,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await ventas.FijarPrecioVentaAsync(
                        productoId, peticion, contexto.UsuarioIdRequerido(), cancellationToken));
                }
                catch (VentaException ex)
                {
                    return Traducir(ex);
                }
            })
            .WithName("VentasFijarPrecio")
            .WithSummary("Fija el precio de venta por unidad base. Solo Administrador General.")
            .WithDescription(
                "Un precio nulo lo QUITA de la lista, que no es lo mismo que ponerlo en cero: sin " +
                "precio, la venta que no traiga uno propio se rechaza; en cero se registraria " +
                "regalada. " +
                "Restringido al Administrador General por el mismo motivo que la lista de precios " +
                "de proveedor: el precio es de toda la red y las tres sedes lo heredan, asi que no " +
                "cabe en el alcance de un gerente de sede.")
            .Produces<PrecioVentaDto>()
            .RequireAuthorization(PoliticasAutorizacion.SoloAdminGeneral);

        // ---------------------------------------------------------------------
        // Ventas
        // ---------------------------------------------------------------------
        grupo.MapGet("/", async (
                IVentasService ventas,
                IUsuarioContexto contexto,
                int? sucursalId,
                int? clienteId,
                DateTime? desde,
                DateTime? hasta,
                int? limite,
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await ventas.ObtenerVentasAsync(
                contexto.ResolverFiltroSucursal(sucursalId),
                clienteId,
                desde,
                hasta,
                limite ?? 100,
                cancellationToken)))
            .WithName("VentasListar")
            .WithSummary("Ventas, de la mas reciente a la mas antigua")
            .WithDescription("Sin detalle de lineas. Para verlo, consultar la venta por id.");

        grupo.MapGet("/{id:int}", async (
                int id,
                IVentasService ventas,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                var venta = await ventas.ObtenerVentaPorIdAsync(id, cancellationToken);

                if (venta is null)
                {
                    return Results.NotFound();
                }

                contexto.ExigirAccesoASucursal(venta.SucursalId);

                return Results.Ok(venta);
            })
            .WithName("VentaPorId")
            .WithSummary("Una venta con su detalle")
            .Produces<VentaDto>()
            .Produces(StatusCodes.Status404NotFound);

        grupo.MapPost("/", async (
                CrearVentaDto peticion,
                IVentasService ventas,
                IUsuarioContexto contexto,
                CancellationToken cancellationToken) =>
            {
                contexto.ExigirAccesoASucursal(peticion.SucursalId);

                try
                {
                    return Results.Ok(await ventas.RegistrarVentaAsync(
                        peticion, contexto.UsuarioIdRequerido(), cancellationToken));
                }
                catch (VentaException ex)
                {
                    return Traducir(ex);
                }
            })
            .WithName("VentasRegistrar")
            .WithSummary("Registra una venta y descuenta el stock")
            .WithDescription(
                "Todo en una transaccion: crea la venta, descuenta el saldo de la sede, consume " +
                "los lotes por FEFO -o el lote que indique cada linea- y anexa un movimiento de " +
                "Retiro por cada lote consumido. Quien vende sale del token. Si falta stock " +
                "responde 409 y no queda nada registrado.")
            .Produces<VentaRegistradaDto>();

        return rutas;
    }

    /// <summary>
    /// Excepcion de negocio a respuesta HTTP.
    ///
    /// El 409 de stock lleva el mensaje del servicio, que dice cuanto habia y
    /// cuanto se pidio. Es informacion de la propia sede de quien pregunta -ya
    /// comprobada arriba-, asi que no revela nada que no pueda ver.
    /// </summary>
    private static IResult Traducir(VentaException ex) => ex switch
    {
        StockInsuficienteException or StockLoteInsuficienteException =>
            RespuestasHttp.Fallo(
                StatusCodes.Status409Conflict, "Stock insuficiente", ex.Message),

        ReferenciaVentaNoEncontradaException =>
            RespuestasHttp.Fallo(
                StatusCodes.Status404NotFound, "Referencia no encontrada", ex.Message),

        // 409 y no 400: la peticion esta bien formada y quien la manda tiene
        // permiso; lo que la impide es una fila que ya existe. El mensaje lleva
        // el nombre y el id del cliente que ya esta, para poder ofrecerlo.
        ClienteDuplicadoException =>
            RespuestasHttp.Fallo(
                StatusCodes.Status409Conflict, "Cliente duplicado", ex.Message),

        // Venta mal formada o conversion de unidades imposible: los dos son
        // errores de lo que se mando, y se arreglan corrigiendolo.
        _ => RespuestasHttp.Fallo(
            StatusCodes.Status400BadRequest, "Venta rechazada", ex.Message)
    };
}
