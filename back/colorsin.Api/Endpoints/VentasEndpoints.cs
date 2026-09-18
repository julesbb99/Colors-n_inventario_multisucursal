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

        // Venta mal formada o conversion de unidades imposible: los dos son
        // errores de lo que se mando, y se arreglan corrigiendolo.
        _ => RespuestasHttp.Fallo(
            StatusCodes.Status400BadRequest, "Venta rechazada", ex.Message)
    };
}
