using Colorsin.Domain.Comun;

namespace Colorsin.Application.Comun.Auth;

/// <summary>Por que no se puede crear ese usuario.</summary>
public enum ErrorCreacionUsuario
{
    Ninguno = 0,

    /// <summary>Quien crea no tiene rango para dar de alta a nadie.</summary>
    RolSinPermisoParaCrear,

    /// <summary>Tiene rango, pero no para ESE rol.</summary>
    RolDestinoNoPermitido,

    /// <summary>Un gerente solo da de alta en SU sede.</summary>
    SedeNoPermitida,

    /// <summary>Un gerente o un operador sin sede no puede existir: todo lo que hacen es por sede.</summary>
    SedeRequerida,

    /// <summary>Quien crea no tiene sede asignada y su rol la exige.</summary>
    CreadorSinSede
}

/// <summary>Desenlace de la comprobacion.</summary>
/// <param name="Permitido">Si la creacion puede seguir.</param>
/// <param name="Error">Motivo del rechazo.</param>
/// <param name="Mensaje">Explicacion para una persona.</param>
public readonly record struct ResultadoRegla(
    bool Permitido,
    ErrorCreacionUsuario Error,
    string Mensaje)
{
    public static ResultadoRegla Ok() => new(true, ErrorCreacionUsuario.Ninguno, string.Empty);

    public static ResultadoRegla No(ErrorCreacionUsuario error, string mensaje) =>
        new(false, error, mensaje);
}

/// <summary>
/// QUIEN PUEDE DAR DE ALTA A QUIEN.
///
///   AdministradorGeneral  crea Gerentes y Operadores, en cualquier sede.
///   GerenteDeSucursal     crea Operadores, SOLO en su propia sede.
///   Operador              no crea a nadie.
///
/// NADIE CREA OTRO ADMINISTRADOR GENERAL por esta via, ni siquiera un
/// administrador. Es deliberado y merece explicacion: el administrador no
/// pertenece a ninguna sede y ve la red entera, asi que una cuenta suya
/// comprometida podria fabricar mas cuentas de su mismo nivel y volver el
/// incidente imposible de acotar. Dar de alta un administrador queda como una
/// operacion de base de datos, que deja rastro fuera de la aplicacion y exige
/// otro tipo de acceso. Si el negocio necesita hacerlo desde la interfaz, se
/// agrega aqui una linea; pero conviene que sea una decision tomada.
///
/// ESTA CLASE ES PURA: no consulta la base, no depende de HTTP y no conoce el
/// token. Recibe los datos y responde. Por eso se puede razonar y probar sin
/// levantar nada, y por eso el servicio la llama SIEMPRE antes de escribir, en
/// vez de repartir la regla entre el endpoint y la capa de datos, donde una
/// mitad puede quedarse sin la otra.
/// </summary>
public static class ReglasCreacionUsuario
{
    /// <summary>Los roles que <paramref name="rolCreador"/> puede dar de alta.</summary>
    public static IReadOnlyList<RolUsuario> RolesQuePuedeCrear(RolUsuario rolCreador) =>
        rolCreador switch
        {
            RolUsuario.AdministradorGeneral =>
                [RolUsuario.GerenteDeSucursal, RolUsuario.Operador],

            RolUsuario.GerenteDeSucursal => [RolUsuario.Operador],

            _ => []
        };

    /// <summary>
    /// Si <paramref name="rolCreador"/>, que pertenece a
    /// <paramref name="sedeCreador"/>, puede crear un usuario con
    /// <paramref name="rolNuevo"/> en <paramref name="sedeNueva"/>.
    /// </summary>
    public static ResultadoRegla Validar(
        RolUsuario rolCreador,
        int? sedeCreador,
        RolUsuario rolNuevo,
        int? sedeNueva)
    {
        var permitidos = RolesQuePuedeCrear(rolCreador);

        if (permitidos.Count == 0)
        {
            return ResultadoRegla.No(
                ErrorCreacionUsuario.RolSinPermisoParaCrear,
                "Tu rol no puede crear usuarios. Pideselo a la gerencia de tu sede.");
        }

        if (!permitidos.Contains(rolNuevo))
        {
            return ResultadoRegla.No(
                ErrorCreacionUsuario.RolDestinoNoPermitido,
                rolNuevo == RolUsuario.AdministradorGeneral
                    ? "Un Administrador General no se crea desde la aplicacion."
                    : "Tu rol no puede crear usuarios de ese tipo. Un gerente solo da de alta operadores.");
        }

        // Todo lo que hace un gerente o un operador ocurre en una sede: sin ella
        // el usuario existiria sin poder consultar ni registrar nada, y cada
        // peticion suya moriria en un 403 sin explicacion.
        if (sedeNueva is null)
        {
            return ResultadoRegla.No(
                ErrorCreacionUsuario.SedeRequerida,
                "Hay que asignarle una sede: gerentes y operadores trabajan siempre sobre una.");
        }

        if (rolCreador == RolUsuario.AdministradorGeneral)
        {
            // No pertenece a ninguna sede, y eso significa TODAS.
            return ResultadoRegla.Ok();
        }

        if (sedeCreador is not int propia)
        {
            return ResultadoRegla.No(
                ErrorCreacionUsuario.CreadorSinSede,
                "Tu usuario no tiene sede asignada, asi que no se puede determinar donde daria de alta.");
        }

        if (sedeNueva != propia)
        {
            return ResultadoRegla.No(
                ErrorCreacionUsuario.SedeNoPermitida,
                "Solo puedes crear usuarios en tu propia sede.");
        }

        return ResultadoRegla.Ok();
    }
}
