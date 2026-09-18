using Colorsin.Application.Comun.Auth;
using Microsoft.AspNetCore.Diagnostics;

namespace Colorsin.Api.Auth;

/// <summary>
/// Convierte una <see cref="AccesoDenegadoException"/> en un 403, en vez de
/// dejarla salir como un 500.
///
/// POR QUE IMPORTA QUE NO SEA UN 500. Un error 500 dice "el sistema se rompio",
/// y esto no es una rotura: es el sistema funcionando, negando algo que debe
/// negar. Ademas, un 500 en desarrollo arrastra la traza completa en la
/// respuesta, y en este caso esa traza contiene el mensaje de la excepcion, que
/// lleva a proposito el id del usuario y las dos sedes. Eso no puede viajar al
/// cliente.
///
/// Por eso aqui se separan las dos cosas: el detalle util va al LOG, donde
/// sirve para investigar, y al cliente le llega un texto generico que no
/// confirma ni desmiente que la otra sede exista.
/// </summary>
public sealed class AccesoDenegadoHandler : IExceptionHandler
{
    private readonly ILogger<AccesoDenegadoHandler> _log;

    public AccesoDenegadoHandler(ILogger<AccesoDenegadoHandler> log)
    {
        _log = log;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not AccesoDenegadoException denegado)
        {
            // Cualquier otra cosa si es un fallo de verdad: que siga su camino
            // hacia el manejador que corresponda.
            return false;
        }

        _log.LogWarning(
            "Acceso denegado en {Metodo} {Ruta}: {Motivo}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            denegado.Message);

        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;

        await httpContext.Response.WriteAsJsonAsync(
            new
            {
                title = "Acceso denegado",
                status = StatusCodes.Status403Forbidden,
                detail = "No tienes permiso para consultar u operar sobre esa sede."
            },
            cancellationToken);

        return true;
    }
}
