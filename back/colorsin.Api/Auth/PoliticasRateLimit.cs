namespace Colorsin.Api.Auth;

/// <summary>
/// Nombres de las politicas de limitacion de peticiones.
///
/// Como constantes y no como cadenas sueltas porque el nombre se escribe en dos
/// sitios -donde se define la politica y donde se aplica al endpoint- y si no
/// coinciden ASP.NET Core lanza al arrancar. Con la constante, eso no puede
/// pasar.
/// </summary>
public static class PoliticasRateLimit
{
    /// <summary>Intentos de inicio de sesion, contados por IP.</summary>
    public const string Login = "login-por-ip";
}
