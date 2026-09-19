using Colorsin.Application.Comun.Auth;
using Colorsin.Domain.Comun;

namespace Colorsin.Application.Comun.DTOs;

/// <summary>
/// Alta de un usuario.
///
/// NO LLEVA EL ID DE QUIEN CREA. Ese sale del token, como en el resto del
/// sistema: si viniera en el cuerpo, cualquiera podria dar de alta una cuenta a
/// nombre de otro, y la bitacora de auditoria -que es justo lo que no puede
/// mentir- quedaria inservible.
///
/// ESTE DTO NUNCA DEBE APARECER EN UN LOG: lleva la contrasena en claro.
/// </summary>
/// <param name="Nombre">Nombre completo de la persona.</param>
/// <param name="Email">Correo. Es el identificador con el que inicia sesion, y es unico en la base.</param>
/// <param name="Password">
/// Contrasena inicial, en claro. Se guarda hasheada con BCrypt; en ningun
/// momento se almacena ni se devuelve tal cual.
/// </param>
/// <param name="Rol">
/// Que sera: 'GerenteDeSucursal' u 'Operador'. Quien puede crear cual lo decide
/// <see cref="ReglasCreacionUsuario"/>.
/// </param>
/// <param name="SucursalId">Sede a la que queda asignado. Obligatoria para los dos roles.</param>
public sealed record CrearUsuarioDto(
    string Nombre,
    string Email,
    string Password,
    RolUsuario Rol,
    int? SucursalId);

/// <summary>Por que se rechazo el alta.</summary>
public enum ErrorUsuario
{
    Ninguno = 0,

    /// <summary>Falta el nombre, el correo o la contrasena.</summary>
    DatosIncompletos,

    /// <summary>El correo no tiene forma de correo.</summary>
    EmailInvalido,

    /// <summary>Ya hay un usuario con ese correo.</summary>
    EmailDuplicado,

    /// <summary>La contrasena no llega al minimo.</summary>
    PasswordDebil,

    /// <summary>La sede indicada no existe.</summary>
    SucursalNoEncontrada,

    /// <summary>Quien crea no puede crear ese rol, o no en esa sede.</summary>
    NoAutorizado
}

/// <summary>
/// Desenlace de un alta.
///
/// Las reglas de negocio se devuelven, no se lanzan, igual que en el resto de
/// modulos: un correo repetido es un desenlace corriente del dia a dia.
/// </summary>
/// <param name="Exito">Si el usuario quedo creado.</param>
/// <param name="Error">Motivo del rechazo.</param>
/// <param name="Mensaje">Explicacion legible.</param>
/// <param name="Usuario">El usuario creado. Nulo si hubo rechazo. Nunca lleva el hash.</param>
public sealed record ResultadoUsuario(
    bool Exito,
    ErrorUsuario Error,
    string Mensaje,
    UsuarioDto? Usuario = null)
{
    public static ResultadoUsuario Fallo(ErrorUsuario error, string mensaje) =>
        new(false, error, mensaje);

    public static ResultadoUsuario Ok(UsuarioDto usuario) =>
        new(true, ErrorUsuario.Ninguno, "Usuario creado.", usuario);
}
