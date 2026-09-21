using Colorsin.Application.Comun.Auditoria;
using Colorsin.Application.Comun.Auth;
using Colorsin.Application.Comun.DTOs.Auth;
using Colorsin.Application.Comun.Mapping;
using Colorsin.Application.Comun.Repositories;
using Colorsin.Domain.Comun;
using Microsoft.Extensions.Logging;

namespace Colorsin.Application.Comun.Services;

/// <inheritdoc cref="IAuthService"/>
///
/// TODOS LOS CAMINOS DE FALLO DEVUELVEN LO MISMO: null, y el endpoint lo
/// convierte en un 401 identico. No hay mensaje que distinga "ese correo no
/// existe" de "la contrasena no es esa", ni codigo distinto, ni nada en el
/// cuerpo. Si los distinguiera, cualquiera podria averiguar que correos estan
/// registrados probandolos de uno en uno, y eso es media brecha: ya solo
/// faltaria la contrasena.
///
/// Y TAMPOCO DEBEN DISTINGUIRSE POR EL TIEMPO QUE TARDAN. Comprobar una
/// contrasena con BCrypt cuesta a proposito un cuarto de segundo; si con un
/// correo desconocido se respondiera al instante, la diferencia seria medible y
/// diria lo mismo que el mensaje que nos cuidamos de no dar. Por eso, cuando el
/// correo no existe, igualmente se comprueba la contrasena contra un hash
/// senuelo: se tira ese trabajo, pero el tiempo se parece.
///
/// EL INTENTO SI QUEDA REGISTRADO, hacia adentro:
///   - Correcto             -> evento de auditoria a nombre del usuario.
///   - Clave incorrecta     -> evento de auditoria a nombre del usuario. Es la
///                             senal con la que se detecta que alguien esta
///                             probando contra una cuenta concreta.
///   - Correo desconocido   -> solo al log. No puede ir a `auditoria_eventos`:
///                             esa tabla tiene clave foranea obligatoria a
///                             `usuarios`, y aqui no hay ningun usuario al que
///                             atribuirselo.
public sealed class AuthService : IAuthService
{
    private const string ModuloAuditoria = "Autenticacion";

    /// <summary>
    /// Hash con el que se comprueba una contrasena cuando el correo no existe,
    /// solo para gastar el mismo tiempo. Se calcula una vez por proceso, sobre
    /// un valor aleatorio que nadie conoce ni necesita conocer.
    ///
    /// Si dos peticiones simultaneas lo calculan a la vez, se calcula dos veces
    /// y gana una: no importa cual, porque el valor da igual mientras sea un
    /// hash valido.
    /// </summary>
    private static string? _hashSenuelo;

    private readonly IUsuarioRepository _usuarios;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<AuthService> _log;

    public AuthService(
        IUsuarioRepository usuarios,
        IPasswordHasher hasher,
        IJwtTokenGenerator jwt,
        IAuditoriaService auditoria,
        ILogger<AuthService> log)
    {
        _usuarios = usuarios;
        _hasher = hasher;
        _jwt = jwt;
        _auditoria = auditoria;
        _log = log;
    }

    public async Task<LoginResponseDto?> IniciarSesionAsync(
        LoginRequestDto peticion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        var email = peticion.Email?.Trim() ?? string.Empty;
        var password = peticion.Password ?? string.Empty;

        // Sin datos no hay nada que comprobar, y aqui no hace falta disimular el
        // tiempo: que falte un campo no revela nada de ninguna cuenta.
        if (email.Length == 0 || password.Length == 0)
        {
            return null;
        }

        var usuario = await _usuarios.ObtenerPorEmailAsync(email, cancellationToken);

        if (usuario is null)
        {
            GastarElMismoTiempo(password);
            _log.LogWarning(
                "Inicio de sesion rechazado: no hay ningun usuario con el correo {Email}.",
                email);
            return null;
        }

        if (!_hasher.Verificar(password, usuario.PasswordHash))
        {
            await RegistrarIntentoFallidoAsync(usuario, cancellationToken);
            return null;
        }

        // Desde aqui, las credenciales son correctas.

        // PERFIL DESHABILITADO: no entra.
        //
        // SE COMPRUEBA DESPUES DE VERIFICAR LA CONTRASENA, y el orden es lo
        // unico que importa aqui. Comprobarlo antes convertiria este endpoint en
        // un detector de cuentas: cualquiera podria distinguir un correo que no
        // existe de uno deshabilitado probando contrasenas al azar. Comprobado
        // despues, solo se entera quien ya tenia las credenciales, que es su
        // propio dueno.
        //
        // Por eso mismo el motivo SI se puede registrar en la bitacora y SI se
        // le puede decir a quien lo intenta -lo hace el endpoint, con su propio
        // mensaje-: llamar a soporte sin saber por que no entras es peor.
        if (!usuario.Activo)
        {
            await _auditoria.RegistrarEventoAsync(
                ModuloAuditoria,
                "InicioSesionRechazadoPerfilInactivo",
                usuario.Id,
                $"Credenciales correctas pero el perfil esta deshabilitado. " +
                $"Correo: {usuario.Email}. Rol: {RolesColorsin.ParaClaim(usuario.Rol)}.",
                cancellationToken);

            await _usuarios.GuardarCambiosAsync(cancellationToken);

            _log.LogWarning(
                "Inicio de sesion rechazado: el perfil {UsuarioId} esta deshabilitado.",
                usuario.Id);

            return null;
        }

        // Unico momento en que el sistema tiene la contrasena en claro, y por
        // tanto el unico en que puede rehacer el hash con el costo de hoy sin
        // pedirle nada a nadie.
        if (_hasher.NecesitaRehash(usuario.PasswordHash))
        {
            usuario.PasswordHash = _hasher.Hash(password);
            _log.LogInformation(
                "Hash de contrasena actualizado al costo actual para el usuario {UsuarioId}.",
                usuario.Id);
        }

        await _auditoria.RegistrarEventoAsync(
            ModuloAuditoria,
            "InicioSesion",
            usuario.Id,
            // El rol se escribe con la MISMA cadena que va en el token, no con
            // el ToString() del enum. Interpolar el enum directamente metia una
            // cuarta grafia del rol en el sistema ('AdministradorGeneral'),
            // ademas de las tres que ya tenia; y esta es la que interesa, porque
            // es la que permite cruzar un evento de la bitacora con el token que
            // se emitio en ese momento.
            $"Inicio de sesion correcto. Correo: {usuario.Email}. " +
            $"Rol: {RolesColorsin.ParaClaim(usuario.Rol)}.",
            cancellationToken);

        // Se guarda ANTES de emitir el token, no despues. Si la auditoria no se
        // puede escribir, tampoco se entra: este proyecto trata la bitacora como
        // parte de la operacion, no como un extra. El rehash, si lo hubo, se
        // confirma en este mismo guardado.
        await _usuarios.GuardarCambiosAsync(cancellationToken);

        var token = _jwt.Generar(usuario);

        return new LoginResponseDto(token.Token, token.Expiracion, usuario.ToDto());
    }

    /// <summary>
    /// Deja constancia del intento fallido contra una cuenta que SI existe.
    ///
    /// No incluye nada de lo que se escribio: ni la contrasena, ni un fragmento,
    /// ni su longitud. Una bitacora con pistas de la contrasena es peor que no
    /// tenerla, porque quien lea la bitacora no deberia poder deducirla.
    /// </summary>
    private async Task RegistrarIntentoFallidoAsync(
        Usuario usuario,
        CancellationToken cancellationToken)
    {
        await _auditoria.RegistrarEventoAsync(
            ModuloAuditoria,
            "InicioSesionFallido",
            usuario.Id,
            $"Contrasena incorrecta. Correo: {usuario.Email}.",
            cancellationToken);

        await _usuarios.GuardarCambiosAsync(cancellationToken);

        _log.LogWarning(
            "Contrasena incorrecta para el usuario {UsuarioId}.",
            usuario.Id);
    }

    /// <summary>
    /// Comprueba la contrasena contra un hash que no es de nadie, solo para que
    /// un correo inexistente cueste mas o menos lo mismo que uno real. El
    /// resultado se descarta a proposito.
    /// </summary>
    private void GastarElMismoTiempo(string password)
    {
        _hashSenuelo ??= _hasher.Hash(Guid.NewGuid().ToString("N"));
        _ = _hasher.Verificar(password, _hashSenuelo);
    }
}
