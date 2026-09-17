using Colorsin.Application.Comun.DTOs;
using Colorsin.Domain.Comun;
using Colorsin.Domain.Inventario;

namespace Colorsin.Application.Comun.Mapping;

/// <summary>
/// Conversion de entidades del modulo Comun a sus DTOs.
///
/// Se hace a mano y no con AutoMapper a proposito: son tres tipos, el mapeo
/// cabe en una pantalla y cualquier error lo atrapa el compilador. Un mapeador
/// por convencion, en cambio, falla en tiempo de ejecucion cuando alguien
/// renombra una propiedad.
/// </summary>
public static class MapeosComun
{
    public static SucursalDto ToDto(this Sucursal sucursal) => new(
        sucursal.Id,
        sucursal.Nombre,
        sucursal.Ciudad,
        sucursal.Direccion,
        // 'Matriz' y 'Sucursal' se escriben igual en el enum y en la base,
        // asi que ToString() basta y no hay tabla de equivalencias que mantener.
        sucursal.RolRed.ToString());

    public static UsuarioDto ToDto(this Usuario usuario) => new(
        usuario.Id,
        usuario.Nombre,
        usuario.Email,
        TextoDe(usuario.Rol),
        usuario.SucursalId,
        // Nulo cuando es el Administrador General (sin sede) o cuando quien
        // consulto no incluyo la navegacion.
        usuario.Sucursal?.Nombre);

    public static UnidadMedidaDto ToDto(this UnidadMedida unidad) => new(
        unidad.Id,
        unidad.Nombre,
        unidad.Simbolo,
        unidad.FactorConversionLitros);

    /// <summary>
    /// Etiqueta legible del rol.
    ///
    /// Coincide con los valores del ENUM en MySQL, pero es una coincidencia
    /// buscada, no una dependencia: esto es el texto que ve el usuario final.
    /// Quien traduce enum &lt;-&gt; columna es el ValueConverter del AppDbContext.
    /// Si algun dia cambia el texto de la interfaz, se cambia aqui y el
    /// convertidor de la base sigue igual.
    /// </summary>
    private static string TextoDe(RolUsuario rol) => rol switch
    {
        RolUsuario.AdministradorGeneral => "Administrador General",
        RolUsuario.GerenteDeSucursal => "Gerente de Sucursal",
        RolUsuario.Operador => "Operador",
        // Si alguien agrega un valor al enum y olvida esta linea, revienta aqui
        // con el valor exacto, en vez de devolver una cadena vacia en silencio.
        _ => throw new ArgumentOutOfRangeException(
            nameof(rol), rol, "Rol de usuario sin etiqueta definida.")
    };
}
