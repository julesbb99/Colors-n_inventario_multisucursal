namespace Colorsin.Application.Comun.Auth;

/// <summary>
/// Quien pide esto esta identificado, pero no tiene permiso para lo que pide.
///
/// ES DISTINTO DE NO ESTAR AUTENTICADO, y la diferencia importa porque son dos
/// respuestas HTTP distintas:
///
///   401 no se sabe quien eres          -> presenta un token y vuelve a intentar
///   403 se sabe, y aun asi no puedes   -> volver a entrar no va a cambiar nada
///
/// Confundirlas manda al frontend a pedir credenciales una y otra vez por algo
/// que ninguna credencial de ese usuario va a resolver.
///
/// EL MENSAJE NO VIAJA AL CLIENTE tal cual. Aqui se escribe para el log, con el
/// detalle que hace falta para investigar -que sede se pidio, cual tiene
/// asignada-; la respuesta HTTP lleva un texto generico. Decirle a alguien
/// "esa sede no es la tuya" ya le confirma que la sede existe.
/// </summary>
public sealed class AccesoDenegadoException : Exception
{
    public AccesoDenegadoException(string message) : base(message)
    {
    }

    public AccesoDenegadoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
