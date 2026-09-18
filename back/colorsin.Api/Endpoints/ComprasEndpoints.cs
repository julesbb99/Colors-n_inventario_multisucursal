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
/// ella, un gerente podria ingresar mercancia a la bodega de otra sede con solo
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
                CancellationToken cancellationToken) =>
            TypedResults.Ok(await compras.ObtenerProveedoresAsync(cancellationToken)))
            .WithName("ComprasProveedores")
            .WithSummary("Catalogo de proveedores")
            .WithDescription(
                "No se filtra por sede: el catalogo de proveedores es de toda la red, no de una " +
                "bodega. Lo que si es de una sede es la orden que se le hace.");

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
            .WithSummary("Crea una orden en estado Pendiente. Solo supervision.")
            .WithDescription(
                "No mueve stock: una orden es una intencion de compra. Las existencias cambian al " +
                "confirmar la recepcion. Quien la crea sale del token. " +
                "Restringido a Administrador General y Gerente de Sucursal.")
            .Produces<ResultadoOrdenCompra>()
            // Una orden compromete dinero con un proveedor. No mueve stock, pero
            // si obliga a la empresa, y eso es de quien responde por la sede.
            .RequireAuthorization(PoliticasAutorizacion.Supervision);

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
            .WithSummary("Registra que llego mercancia de la orden. Solo supervision.")
            .WithDescription(
                "Es la unica operacion de compras que toca inventario: sube el saldo, anexa un " +
                "movimiento de Ingreso por linea y crea o engrosa los lotes. Admite entregas " +
                "parciales y se puede llamar varias veces: cada llamada acumula sobre lo ya " +
                "recibido. Con el cuerpo vacio se recibe todo lo que falte. " +
                "Restringido a Administrador General y Gerente de Sucursal.")
            .Produces<ResultadoRecepcion>()
            // Dar por recibida una compra es aceptar la mercancia y con ella la
            // factura: cierra el documento y habilita el pago. Es la operacion
            // que la especificacion nombra como "Confirmar Compras".
            //
            // Ojo con no confundirla con la recepcion de un TRASLADO, que si
            // puede hacer un operador: alli no se acepta nada de un tercero, solo
            // se mueve stock entre dos bodegas de la misma empresa.
            .RequireAuthorization(PoliticasAutorizacion.Supervision);

        return rutas;
    }

    private static int CodigoDe(ErrorCompra error) => error switch
    {
        ErrorCompra.ProveedorNoEncontrado
            or ErrorCompra.ProductoNoEncontrado
            or ErrorCompra.UnidadNoEncontrada
            or ErrorCompra.OrdenNoEncontrada
            => StatusCodes.Status404NotFound,

        ErrorCompra.EstadoNoPermiteRecepcion
            or ErrorCompra.NadaPorRecibir
            => StatusCodes.Status409Conflict,

        _ => StatusCodes.Status400BadRequest
    };
}
