using Colorsin.Application.Comun.Auditoria;
using Colorsin.Application.Comun.Auth;
using Colorsin.Application.Comun.DTOs;
using Colorsin.Application.Comun.Mapping;
using Colorsin.Application.Comun.Repositories;
using Colorsin.Domain.Comun;

namespace Colorsin.Application.Comun.Services;

/// <inheritdoc cref="IUsuarioService"/>
public sealed class UsuarioService : IUsuarioService
{
    /// <summary>Nombre del modulo en los eventos de auditoria.</summary>
    private const string Modulo = "Comun";

    /// <summary>
    /// Minimo de la contrasena inicial.
    ///
    /// Doce y no ocho porque quien la escribe NO es su dueno: la teclea un
    /// gerente para otra persona, y en la practica salen cosas como
    /// "operario123". Un minimo largo obliga al menos a construir una frase.
    ///
    /// OJO CON LO QUE ESTO NO RESUELVE: no hay cambio de contrasena en el
    /// sistema todavia, asi que quien la crea la sigue conociendo. Ver la nota
    /// del endpoint.
    /// </summary>
    private const int LargoMinimoPassword = 12;

    private readonly IUsuarioRepository _repositorio;
    private readonly ISucursalRepository _sucursales;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditoriaService _auditoria;

    public UsuarioService(
        IUsuarioRepository repositorio,
        ISucursalRepository sucursales,
        IPasswordHasher hasher,
        IAuditoriaService auditoria)
    {
        _repositorio = repositorio;
        _sucursales = sucursales;
        _hasher = hasher;
        _auditoria = auditoria;
    }

    public async Task<IReadOnlyList<UsuarioDto>> ListarAsync(
        int? sucursalId = null,
        bool incluirInactivos = false,
        CancellationToken cancellationToken = default)
    {
        var usuarios = sucursalId is null
            ? await _repositorio.ObtenerTodosAsync(incluirInactivos, cancellationToken)
            : await _repositorio.ObtenerPorSucursalAsync(
                sucursalId.Value, incluirInactivos, cancellationToken);

        return usuarios.Select(u => u.ToDto()).ToList();
    }

    public async Task<ResultadoUsuario> CambiarEstadoAsync(
        int id,
        bool activo,
        CreadorUsuario actor,
        CancellationToken cancellationToken = default)
    {
        // Con seguimiento: esta fila se modifica.
        var usuario = await _repositorio.ObtenerParaActualizarAsync(id, cancellationToken);

        if (usuario is null)
        {
            return ResultadoUsuario.Fallo(
                ErrorUsuario.NoEncontrado,
                $"No existe el usuario {id}.");
        }

        // LA JERARQUIA, ANTES QUE NADA. Va primero por lo mismo que en el alta:
        // si el rango no alcanza, decirle ademas que el perfil ya estaba
        // deshabilitado seria confirmarle el estado de una cuenta sobre la que
        // no tiene permiso.
        var regla = ReglasGestionUsuario.Validar(
            actor.Rol, actor.SucursalId, actor.UsuarioId,
            usuario.Id, usuario.Rol, usuario.SucursalId);

        if (!regla.Permitido)
        {
            return ResultadoUsuario.Fallo(ErrorUsuario.NoAutorizado, regla.Mensaje);
        }

        if (usuario.Activo == activo)
        {
            return ResultadoUsuario.Fallo(
                ErrorUsuario.SinCambio,
                $"El perfil de {usuario.Nombre} ya estaba " +
                $"{(activo ? "habilitado" : "deshabilitado")}.");
        }

        usuario.Activo = activo;

        await _auditoria.RegistrarEventoAsync(
            Modulo,
            activo ? "HabilitarUsuario" : "DeshabilitarUsuario",
            actor.UsuarioId,
            $"usuario={usuario.Id} | nombre='{usuario.Nombre}' | rol={usuario.Rol} | " +
            $"sede={usuario.SucursalId?.ToString() ?? "(ninguna)"} | " +
            $"activo={(activo ? "0->1" : "1->0")}",
            cancellationToken);

        // Un solo guardado para el cambio y su evento: una cuenta cerrada sin
        // rastro de quien la cerro es justo lo que la bitacora existe para
        // impedir.
        await _repositorio.GuardarCambiosAsync(cancellationToken);

        return ResultadoUsuario.Estado(usuario.ToDto(), activo);
    }

    public async Task<UsuarioDto?> ObtenerPorIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var usuario = await _repositorio.ObtenerPorIdAsync(id, cancellationToken);
        return usuario?.ToDto();
    }

    public async Task<ResultadoUsuario> CrearAsync(
        CrearUsuarioDto peticion,
        CreadorUsuario creador,
        CancellationToken cancellationToken = default)
    {
        // --- 1. La regla de quien puede crear a quien, ANTES que nada ---------
        //
        // Va primero a proposito: si el rol no alcanza, no tiene sentido decirle
        // que ademas el correo esta repetido. Ademas, validar el formato antes
        // convertiria este endpoint en una forma de averiguar que correos
        // existen sin tener permiso para consultarlos.
        var regla = ReglasCreacionUsuario.Validar(
            creador.Rol, creador.SucursalId, peticion.Rol, peticion.SucursalId);

        if (!regla.Permitido)
        {
            return ResultadoUsuario.Fallo(ErrorUsuario.NoAutorizado, regla.Mensaje);
        }

        // --- 2. Forma de los datos --------------------------------------------
        var nombre = peticion.Nombre?.Trim() ?? string.Empty;
        var email = peticion.Email?.Trim() ?? string.Empty;

        if (nombre.Length == 0 || email.Length == 0 || string.IsNullOrEmpty(peticion.Password))
        {
            return ResultadoUsuario.Fallo(
                ErrorUsuario.DatosIncompletos,
                "El nombre, el correo y la contrasena son obligatorios.");
        }

        if (!EsCorreoPlausible(email))
        {
            return ResultadoUsuario.Fallo(
                ErrorUsuario.EmailInvalido,
                $"'{email}' no parece un correo valido.");
        }

        if (peticion.Password.Length < LargoMinimoPassword)
        {
            return ResultadoUsuario.Fallo(
                ErrorUsuario.PasswordDebil,
                $"La contrasena debe tener al menos {LargoMinimoPassword} caracteres.");
        }

        // --- 3. Que la sede exista de verdad -----------------------------------
        // La regla ya comprobo que el creador PUEDE usarla; esto comprueba que
        // EXISTE. Son dos cosas distintas: un administrador puede crear en
        // cualquier sede, incluida una que no esta en la base.
        var sede = await _sucursales.ObtenerPorIdAsync(peticion.SucursalId!.Value, cancellationToken);
        if (sede is null)
        {
            return ResultadoUsuario.Fallo(
                ErrorUsuario.SucursalNoEncontrada,
                $"No existe la sede {peticion.SucursalId}.");
        }

        // --- 4. Correo libre ---------------------------------------------------
        // La columna tiene indice unico, asi que esto es para responder un 409
        // con un mensaje legible en vez de dejar reventar la restriccion. Entre
        // esta lectura y el guardado cabe una carrera; si dos altas simultaneas
        // usaran el mismo correo, la segunda falla contra el indice y se
        // revierte, que es el lado correcto del error.
        var existente = await _repositorio.ObtenerPorEmailAsync(email, cancellationToken);
        if (existente is not null)
        {
            return ResultadoUsuario.Fallo(
                ErrorUsuario.EmailDuplicado,
                $"Ya hay un usuario registrado con el correo '{email}'.");
        }

        // --- 5. Crear ----------------------------------------------------------
        var usuario = new Usuario
        {
            Nombre = nombre,
            Email = email,
            // Hasheada con BCrypt antes de tocar la base. La contrasena en claro
            // no se guarda, no se registra y no vuelve en la respuesta.
            PasswordHash = _hasher.Hash(peticion.Password),
            Rol = peticion.Rol,
            SucursalId = peticion.SucursalId
        };

        _repositorio.Agregar(usuario);

        // Se guarda aqui para que MySQL asigne el id, que hace falta en el
        // detalle de la auditoria.
        await _repositorio.GuardarCambiosAsync(cancellationToken);

        await _auditoria.RegistrarEventoAsync(
            Modulo,
            "CrearUsuario",
            creador.UsuarioId,
            $"usuario={usuario.Id} ('{nombre}') | correo={email} | " +
            $"rol={RolesColorsin.ParaClaim(peticion.Rol)} | " +
            $"sucursal={sede.Id} ('{sede.Nombre}')",
            cancellationToken);

        await _repositorio.GuardarCambiosAsync(cancellationToken);

        // Se arma el DTO a mano para poder incluir el nombre de la sede sin
        // releer: la entidad recien creada no trae cargada la navegacion.
        return ResultadoUsuario.Ok(new UsuarioDto(
            usuario.Id,
            usuario.Nombre,
            usuario.Email,
            TextoDeRol(peticion.Rol),
            usuario.SucursalId,
            sede.Nombre,
            // Nace habilitado: se acaba de crear para que la persona entre.
            usuario.Activo));
    }

    /// <summary>
    /// Comprobacion deliberadamente laxa: un arroba con algo a cada lado y un
    /// punto despues.
    ///
    /// No se intenta validar un correo "de verdad" con una expresion regular. La
    /// gramatica real de una direccion admite cosas que casi nadie espera, y las
    /// expresiones que circulan para esto rechazan correos perfectamente validos.
    /// Lo unico que confirma que una direccion existe es mandarle un mensaje;
    /// esto solo atrapa el dedazo evidente.
    /// </summary>
    private static bool EsCorreoPlausible(string email)
    {
        var arroba = email.IndexOf('@');

        if (arroba <= 0 || arroba == email.Length - 1 || email.Count(c => c == '@') != 1)
        {
            return false;
        }

        var dominio = email[(arroba + 1)..];
        var punto = dominio.IndexOf('.');

        return punto > 0 && punto < dominio.Length - 1 && !email.Any(char.IsWhiteSpace);
    }

    /// <summary>El rol como lo espera el ENUM de MySQL. Espeja el mapeo de MapeosComun.</summary>
    private static string TextoDeRol(RolUsuario rol) => rol switch
    {
        RolUsuario.AdministradorGeneral => "Administrador General",
        RolUsuario.GerenteDeSucursal => "Gerente de Sucursal",
        RolUsuario.Operador => "Operador",
        _ => throw new ArgumentOutOfRangeException(nameof(rol), rol, "Rol sin texto equivalente.")
    };
}
