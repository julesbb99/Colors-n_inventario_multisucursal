namespace Colorsin.Application.Comun.DTOs.Auth;

/// <summary>
/// Credenciales que presenta quien quiere entrar.
///
/// EL IDENTIFICADOR ES EL CORREO, no un nombre de usuario: la tabla `usuarios`
/// no tiene columna de usuario, y `nombre` es el nombre completo de la persona
/// ("Diana Carvajal Londono"), que ni es unico ni sirve para iniciar sesion. El
/// correo si tiene indice unico, que es lo que hace falta para buscar una sola
/// fila.
///
/// ESTE DTO NUNCA DEBE APARECER EN UN LOG. Lleva la contrasena en claro -es
/// inevitable: alguien tiene que recibirla para poder verificarla- asi que no
/// se registra, ni entero ni por campos, ni siquiera al depurar. Por lo mismo
/// solo puede viajar por HTTPS.
/// </summary>
/// <param name="Email">Correo del usuario. Se compara sin distinguir mayusculas.</param>
/// <param name="Password">
/// Contrasena en claro, tal como la escribio la persona. El servicio de inicio
/// de sesion la compara contra el hash almacenado; en ningun momento se guarda
/// ni se devuelve.
/// </param>
public sealed record LoginRequestDto(
    string Email,
    string Password);
