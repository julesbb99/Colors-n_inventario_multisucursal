using Colorsin.Application.Comun.Repositories;
using Colorsin.Domain.Comun;
using Microsoft.EntityFrameworkCore;

namespace Colorsin.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IUsuarioRepository"/>
public sealed class UsuarioRepository : IUsuarioRepository
{
    private readonly AppDbContext _db;

    public UsuarioRepository(AppDbContext db)
    {
        _db = db;
    }

    // Include(Sucursal) en las tres consultas porque el DTO expone el nombre de
    // la sede. Sin el, con lazy loading apagado el nombre saldria siempre nulo;
    // y si algun dia se enciende, seria una consulta por usuario (N+1).
    // Es un LEFT JOIN: SucursalId es opcional en el Administrador General.
    public async Task<IReadOnlyList<Usuario>> ObtenerTodosAsync(
        bool incluirInactivos = false,
        CancellationToken cancellationToken = default) =>
        await _db.Usuarios
            .AsNoTracking()
            .Include(u => u.Sucursal)
            // Los deshabilitados salen solo si se piden. El orden por estado va
            // primero para que, cuando se pidan, los activos no queden
            // mezclados entre cuentas que ya no entran.
            .Where(u => incluirInactivos || u.Activo)
            .OrderByDescending(u => u.Activo)
            .ThenBy(u => u.Nombre)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Usuario>> ObtenerPorSucursalAsync(
        int sucursalId,
        bool incluirInactivos = false,
        CancellationToken cancellationToken = default) =>
        await _db.Usuarios
            .AsNoTracking()
            .Include(u => u.Sucursal)
            .Where(u => u.SucursalId == sucursalId && (incluirInactivos || u.Activo))
            .OrderByDescending(u => u.Activo)
            .ThenBy(u => u.Nombre)
            .ToListAsync(cancellationToken);

    public Task<Usuario?> ObtenerPorIdAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        _db.Usuarios
            .AsNoTracking()
            .Include(u => u.Sucursal)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<Usuario?> ObtenerParaActualizarAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        _db.Usuarios
            // SIN AsNoTracking: esta fila se modifica. Y SIN filtrar por activo,
            // porque reactivar un perfil exige encontrarlo estando apagado.
            .Include(u => u.Sucursal)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<Usuario?> ObtenerPorEmailAsync(
        string email,
        CancellationToken cancellationToken = default) =>
        _db.Usuarios
            // SIN AsNoTracking, al contrario que las tres de arriba: si el hash
            // hay que actualizarlo por costo, se modifica sobre esta misma
            // instancia y basta con guardar.
            .Include(u => u.Sucursal)
            // Comparacion directa: la intercalacion de la columna
            // (utf8mb4_0900_ai_ci) ya ignora mayusculas, y asi la consulta usa el
            // indice unico uq_usuarios_email en vez de recorrer la tabla.
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public void Agregar(Usuario usuario) => _db.Usuarios.Add(usuario);

    public Task<int> GuardarCambiosAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
