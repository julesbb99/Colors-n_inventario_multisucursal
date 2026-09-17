namespace Colorsin.Application.Comun.DTOs;

/// <summary>
/// Usuario del sistema tal como se expone hacia afuera.
///
/// Este DTO existe justamente para NO sacar la entidad <c>Usuario</c> de la
/// capa de datos: la entidad lleva <c>PasswordHash</c>, y serializar la entidad
/// directamente lo filtraria en cada respuesta. Aqui ese campo no existe.
/// </summary>
/// <param name="Id">Clave primaria.</param>
/// <param name="Nombre">Nombre completo.</param>
/// <param name="Email">Correo, unico en la base.</param>
/// <param name="Rol">Rol en texto: 'Administrador General', 'Gerente de Sucursal' u 'Operador'.</param>
/// <param name="SucursalId">Sede asignada. Nulo en el Administrador General, que no pertenece a una sede.</param>
/// <param name="SucursalNombre">Nombre de la sede asignada, para no obligar a una segunda consulta.</param>
public sealed record UsuarioDto(
    int Id,
    string Nombre,
    string Email,
    string Rol,
    int? SucursalId,
    string? SucursalNombre);
