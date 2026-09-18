namespace Colorsin.Application.Comun.Services;

/// <summary>
/// Calcula y comprueba los hash de las contrasenas.
///
/// LA INTERFAZ NO NOMBRA NINGUN ALGORITMO, a proposito. Hoy detras hay BCrypt,
/// pero elegir un algoritmo de hash es una decision que se revisa cada pocos
/// anos, y lo que no se puede permitir es que el nombre del de turno quede
/// escrito por toda la capa de aplicacion.
///
/// NO ES CIFRADO NI ES REVERSIBLE. De un hash no se saca la contrasena: por eso
/// no existe un metodo para "obtener la contrasena" y por eso un olvido se
/// resuelve poniendo una nueva, nunca recordando la anterior.
///
/// ES LENTO A PROPOSITO. Un hash de contrasena que corre rapido no sirve: toda
/// su defensa consiste en que probar millones de candidatas cueste demasiado
/// tiempo. Por eso no se debe sustituir por SHA-256 ni por nada "mas eficiente".
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hash de una contrasena nueva, listo para guardar en
    /// <c>usuarios.password_hash</c>.
    ///
    /// Cada llamada devuelve un valor DISTINTO para la misma contrasena, porque
    /// lleva sal aleatoria dentro. No se pueden comparar dos hash entre si; para
    /// comprobar una contrasena esta <see cref="Verificar"/>.
    /// </summary>
    string Hash(string password);

    /// <summary>
    /// Comprueba una contrasena contra un hash guardado.
    ///
    /// Devuelve <c>false</c>, sin lanzar, cuando el hash almacenado no tiene
    /// formato valido. Ese caso es real y no teorico: los usuarios sembrados
    /// llevan un marcador que no es un hash, y la libreria lanza ante una cadena
    /// asi. Dejar que la excepcion suba convertiria un intento fallido de inicio
    /// de sesion en un error 500.
    /// </summary>
    bool Verificar(string password, string hashAlmacenado);

    /// <summary>
    /// Si el hash se calculo con un costo mas bajo del que se usa hoy.
    ///
    /// Es lo que permite subir la dificultad sin pedirle a nadie que cambie su
    /// contrasena: en el unico momento en que el sistema conoce la contrasena en
    /// claro -el inicio de sesion- se vuelve a calcular el hash con el costo
    /// actual y se guarda. Es tambien el punto por donde se migraria a otro
    /// algoritmo.
    /// </summary>
    bool NecesitaRehash(string hashAlmacenado);
}
