namespace Colorsin.Application.Comun.DTOs.Auth;

/// <summary>
/// Lo que se devuelve tras un inicio de sesion correcto.
///
/// NO LLEVA NADA SECRETO SALVO EL PROPIO TOKEN. El <see cref="Usuario"/> es el
/// <see cref="UsuarioDto"/> de siempre, que a proposito no expone
/// <c>PasswordHash</c>; va incluido para que el frontend pueda pintar el nombre
/// y decidir el menu segun el rol sin tener que descifrar el token ni pedir el
/// perfil en una segunda llamada.
///
/// EL TOKEN ES UNA CREDENCIAL, no un identificador: quien lo tenga es, para
/// efectos de la API, ese usuario, hasta que expire. No debe registrarse en
/// logs, ni mandarse por URL, ni guardarse donde otra pagina pueda leerlo.
/// </summary>
/// <param name="Token">
/// El JWT firmado. Se manda en cada peticion como
/// <c>Authorization: Bearer &lt;token&gt;</c>.
/// </param>
/// <param name="Expiracion">
/// Momento exacto en que deja de valer, en UTC.
///
/// Va en UTC y no en hora local a proposito: el frontend puede estar en otra
/// zona, y comparar dos horas locales de husos distintos para decidir si un
/// token sigue vivo es una fuente segura de errores. Es informativa -quien
/// valida de verdad es el servidor en cada peticion- y sirve para que el
/// frontend renueve la sesion antes de que caduque en medio de algo.
/// </param>
/// <param name="Usuario">Quien inicio sesion, con su rol y su sede.</param>
public sealed record LoginResponseDto(
    string Token,
    DateTime Expiracion,
    UsuarioDto Usuario);
