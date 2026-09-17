namespace Colorsin.Application.Comun.DTOs;

/// <summary>
/// Sede de la red, en la forma en que se expone hacia afuera.
///
/// OJO con <c>Codigo</c>: la tabla `sucursales` no tiene esa columna, asi que
/// aqui no aparece. Lo que identifica a una sede hoy es el par nombre + ciudad,
/// y <see cref="RolRed"/> distingue la matriz del resto. Si hace falta un codigo
/// corto ('MED-01', 'BOG-02') hay que agregarlo primero al esquema con su propia
/// migracion; inventarlo en el DTO daria un campo vacio o calculado que no
/// corresponde a ningun dato real.
/// </summary>
/// <param name="Id">Clave primaria.</param>
/// <param name="Nombre">Nombre comercial de la sede.</param>
/// <param name="Ciudad">Ciudad donde opera.</param>
/// <param name="Direccion">Direccion fisica. Opcional en la base.</param>
/// <param name="RolRed">'Matriz' o 'Sucursal'.</param>
public sealed record SucursalDto(
    int Id,
    string Nombre,
    string Ciudad,
    string? Direccion,
    string RolRed);
