namespace Colorsin.Domain.Comun;

/// <summary>Papel de una sede dentro de la red Colorsin.</summary>
public enum RolRed
{
    Matriz,
    Sucursal
}

/// <summary>
/// Rol del usuario en el sistema.
/// Los valores en MySQL llevan espacios ('Administrador General'); la
/// traduccion se hace con un convertidor en la capa de Infrastructure, para
/// que el dominio no cargue con el formato de la base de datos.
/// </summary>
public enum RolUsuario
{
    AdministradorGeneral,
    GerenteDeSucursal,
    Operador
}
