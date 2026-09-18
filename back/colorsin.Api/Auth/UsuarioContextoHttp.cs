using System.Security.Claims;
using Colorsin.Application.Comun.Auth;

namespace Colorsin.Api.Auth;

/// <inheritdoc cref="IUsuarioContexto"/>
///
/// VIVE EN EL PROYECTO DE API, no en Application ni en Infrastructure como
/// sugeria el plan. El motivo es una dependencia: para leer al usuario hace
/// falta <see cref="IHttpContextAccessor"/>, que es ASP.NET Core. Meterlo en
/// Application ataria la capa de casos de uso a un servidor web, y meterlo en
/// Infrastructure -que hoy solo sabe de base de datos- obligaria a arrastrar
/// todo ASP.NET Core hasta alli. La interfaz si esta en Application, que es lo
/// que importa: los servicios dependen de ella y no de HTTP.
public sealed class UsuarioContextoHttp : IUsuarioContexto
{
    private readonly IHttpContextAccessor _accesor;

    public UsuarioContextoHttp(IHttpContextAccessor accesor)
    {
        _accesor = accesor;
    }

    private ClaimsPrincipal? Usuario => _accesor.HttpContext?.User;

    public bool EstaAutenticado => Usuario?.Identity?.IsAuthenticated == true;

    public int? UsuarioId => Usuario?.LeerUsuarioId();

    public string? Rol => Usuario?.FindFirst(ClaimTypes.Role)?.Value;

    public int? SucursalId => Usuario?.LeerSucursalId();

    public bool EsAdminGeneral =>
        string.Equals(Rol, RolesColorsin.AdminGeneral, StringComparison.Ordinal);

    public int UsuarioIdRequerido() =>
        UsuarioId ?? throw new AccesoDenegadoException(
            "La peticion no lleva un usuario identificado. Solo deberia poder llegar " +
            "aqui algo que paso por un endpoint con RequireAuthorization.");

    public int? ResolverFiltroSucursal(int? sucursalIdSolicitado)
    {
        if (!EstaAutenticado)
        {
            throw new AccesoDenegadoException(
                "Se intento resolver el filtro de sede sin usuario autenticado.");
        }

        // El administrador manda sobre toda la red: si pide una sede se le da
        // esa, y si no pide ninguna se le da el consolidado.
        if (EsAdminGeneral)
        {
            return sucursalIdSolicitado;
        }

        // A partir de aqui, gerente u operador.

        if (SucursalId is not int sucursalPropia)
        {
            // Sin sede asignada y sin ser administrador no hay nada que pueda
            // ver. Interpretar el nulo como "todas" seria convertir un dato
            // faltante en el permiso mas alto del sistema.
            throw new AccesoDenegadoException(
                $"El usuario {UsuarioId} tiene rol '{Rol}' pero ninguna sede asignada " +
                "en el token, asi que no se le puede acotar la consulta.");
        }

        // Omitir el parametro no da acceso a la red: se le acota a la suya.
        if (sucursalIdSolicitado is not int solicitada)
        {
            return sucursalPropia;
        }

        if (solicitada != sucursalPropia)
        {
            // El detalle es para el log. Al cliente le llega un 403 generico:
            // decirle "esa sede no es la tuya" ya le confirma que existe.
            throw new AccesoDenegadoException(
                $"El usuario {UsuarioId} (rol '{Rol}', sede {sucursalPropia}) pidio datos " +
                $"de la sede {solicitada}.");
        }

        return sucursalPropia;
    }
}
