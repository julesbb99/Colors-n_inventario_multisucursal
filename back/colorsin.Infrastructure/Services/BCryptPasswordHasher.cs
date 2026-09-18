using Colorsin.Application.Comun.Services;

namespace Colorsin.Infrastructure.Services;

/// <inheritdoc cref="IPasswordHasher"/>
///
/// POR QUE BCRYPT. Es lento por diseno y lleva la sal dentro del propio hash,
/// asi que no hace falta una columna aparte ni acordarse de generarla. Lleva
/// mas de veinte anos en produccion en todas partes, la libreria de .NET es de
/// una sola llamada y no hay parametros que se puedan configurar mal, que es el
/// riesgo real de Argon2: es mejor algoritmo, pero con tres parametros que hay
/// que entender y que se ajustan mal con facilidad.
///
/// SI ALGUN DIA SE CAMBIA, el punto de entrada es este archivo mas
/// <see cref="NecesitaRehash"/>: al iniciar sesion se detecta el hash viejo y se
/// reemplaza por el nuevo, sin pedirle nada a nadie. Por eso la interfaz no
/// nombra al algoritmo.
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    /// <summary>
    /// Costo del hash. Cada punto DUPLICA el tiempo de calculo.
    ///
    /// Con 12, un inicio de sesion medido de punta a punta en esta maquina sale
    /// en torno a medio segundo: lo bastante lento para que probar contrasenas
    /// en masa no compense, y lo bastante rapido para que no moleste. Subirlo es
    /// seguro
    /// -los hash viejos se actualizan solos al iniciar sesion- pero cada punto
    /// se paga en cada intento, tambien en los fallidos, que es justo lo que un
    /// ataque por fuerza bruta provoca a proposito.
    /// </summary>
    private const int Costo = 12;

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, Costo);

    public bool Verificar(string password, string hashAlmacenado)
    {
        if (string.IsNullOrWhiteSpace(hashAlmacenado))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hashAlmacenado);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // El valor guardado no es un hash de BCrypt. Pasa con los usuarios
            // sembrados, que llevan un marcador en vez de un hash real, y pasaria
            // con cualquier fila editada a mano en la base.
            //
            // Es un fallo de autenticacion, no un fallo del sistema: quien lo
            // intenta recibe "credenciales incorrectas" como en cualquier otro
            // caso. Devolver false y no relanzar tambien evita que la respuesta
            // distinga esa cuenta de las demas.
            return false;
        }
    }

    public bool NecesitaRehash(string hashAlmacenado)
    {
        if (string.IsNullOrWhiteSpace(hashAlmacenado))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.PasswordNeedsRehash(hashAlmacenado, Costo);
        }
        catch (Exception ex) when (ex is BCrypt.Net.SaltParseException or ArgumentException)
        {
            // Un hash ilegible no se "actualiza": no hay nada que migrar, porque
            // nunca correspondio a una contrasena. Decir que si obligaria a quien
            // llama a intentar un rehash que tampoco tendria sentido.
            return false;
        }
    }
}
