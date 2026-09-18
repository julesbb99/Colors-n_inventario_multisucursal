using Colorsin.Application.Comun.DTOs.Auth;

namespace Colorsin.Application.Comun.Services;

/// <summary>Inicio de sesion.</summary>
public interface IAuthService
{
    /// <summary>
    /// Comprueba las credenciales y, si son correctas, emite el token.
    ///
    /// DEVUELVE <c>null</c> EN VEZ DE LANZAR cuando no lo son. Una contrasena
    /// mal escrita es lo mas corriente que le puede pasar a este metodo, no algo
    /// excepcional, y tratarla como excepcion invita a distinguir casos -"no
    /// existe el usuario", "clave incorrecta"- que es justo lo que no se debe
    /// hacer: quien intenta entrar recibe siempre la misma respuesta, porque la
    /// diferencia le diria cuales correos estan registrados.
    ///
    /// Ese silencio es solo hacia afuera. Hacia adentro el intento queda
    /// registrado; ver la implementacion.
    /// </summary>
    Task<LoginResponseDto?> IniciarSesionAsync(
        LoginRequestDto peticion,
        CancellationToken cancellationToken = default);
}
