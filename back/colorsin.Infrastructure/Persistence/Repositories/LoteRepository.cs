using Colorsin.Application.Inventario.Repositories;
using Colorsin.Domain.Inventario;
using Microsoft.EntityFrameworkCore;

namespace Colorsin.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="ILoteRepository"/>
public sealed class LoteRepository : ILoteRepository
{
    /// <summary>
    /// Tope duro de filas. La tabla de lotes crece con cada recepcion y una
    /// consulta sin LIMIT terminaria trayendola entera.
    /// </summary>
    private const int LimiteMinimo = 1;
    private const int LimiteMaximo = 1000;

    private readonly AppDbContext _db;

    public LoteRepository(AppDbContext db)
    {
        _db = db;
    }

    // =========================================================================
    // CONSULTAS
    // =========================================================================

    public async Task<IReadOnlyList<Lote>> ObtenerAsync(
        int? sucursalId = null,
        int? productoId = null,
        bool soloConSaldo = false,
        int limite = 200,
        CancellationToken cancellationToken = default)
    {
        var consulta = ConsultaBase();

        if (sucursalId is int sid)
        {
            consulta = consulta.Where(l => l.SucursalId == sid);
        }

        if (productoId is int pid)
        {
            consulta = consulta.Where(l => l.ProductoId == pid);
        }

        if (soloConSaldo)
        {
            consulta = consulta.Where(l => l.CantidadBase > 0);
        }

        return await OrdenarFefo(consulta)
            .Take(Math.Clamp(limite, LimiteMinimo, LimiteMaximo))
            .ToListAsync(cancellationToken);
    }

    public Task<Lote?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default) =>
        ConsultaBase().FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public Task<Lote?> ObtenerParaEditarAsync(int id, CancellationToken cancellationToken = default) =>
        // Sin AsNoTracking a proposito: esta entidad se va a modificar. Las
        // navegaciones se incluyen igual porque el resultado se devuelve como
        // DTO, y sin ellas el nombre del producto y el de la sede saldrian
        // vacios en la respuesta de la edicion.
        _db.Lotes
            .Include(l => l.Sucursal)
            .Include(l => l.Producto)
                .ThenInclude(p => p.UnidadBase)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public Task<bool> ExisteNumeroAsync(
        int productoId,
        int sucursalId,
        string numeroLote,
        int? excluyendoId = null,
        CancellationToken cancellationToken = default) =>
        _db.Lotes
            .AsNoTracking()
            // La comparacion la resuelve MySQL con la collation del esquema,
            // utf8mb4_0900_ai_ci, que NO distingue mayusculas ni acentos. O sea
            // que 'lote-a' y 'LOTE-A' cuentan como el mismo numero, igual que
            // para el indice unico. Es lo que se quiere: si no coincidieran, la
            // comprobacion diria que no hay duplicado y el INSERT fallaria
            // despues contra el indice.
            .Where(l => l.ProductoId == productoId
                     && l.SucursalId == sucursalId
                     && l.NumeroLote == numeroLote)
            .Where(l => excluyendoId == null || l.Id != excluyendoId)
            .AnyAsync(cancellationToken);

    public async Task<IReadOnlyList<Lote>> ObtenerProximosAVencerAsync(
        int? sucursalId,
        DateOnly? desde,
        DateOnly hasta,
        int limite = 200,
        CancellationToken cancellationToken = default)
    {
        var consulta = ConsultaBase()
            // Los agotados no interesan: un lote en cero ya no se puede
            // despachar y su caducidad da igual. Los que no caducan tampoco
            // entran: no hay nada que avisar.
            .Where(l => l.CantidadBase > 0
                     && l.FechaVencimiento != null
                     && l.FechaVencimiento <= hasta);

        // ESTE FILTRO FALTABA, y es el unico metodo del repositorio al que le
        // faltaba. El parametro estaba declarado, el endpoint lo resolvia bien
        // con ResolverFiltroSucursal y el servicio lo pasaba intacto: se perdia
        // aqui, en la ultima linea del camino.
        //
        // LO QUE PROVOCABA. Las alertas de caducidad de CUALQUIER sede salian en
        // el panel de TODAS, asi que un operario de Cali veia un lote de Armenia
        // -una fuga del aislamiento por sede- y ademas el tablero se contradecia
        // consigo mismo: la tarjeta "Por vencer" viene de otra consulta, en
        // DashboardRepository, que si filtra. El resumen decia 0 y la tabla de
        // abajo pintaba una fila.
        //
        // Se comprueba contra `null` explicitamente y no con `is int`: da igual
        // en la traduccion, pero deja a la vista que el nulo significa "toda la
        // red" y no "sin filtro por descuido", que es como se perdio la primera
        // vez.
        if (sucursalId is int sid)
        {
            consulta = consulta.Where(l => l.SucursalId == sid);
        }

        if (desde is DateOnly inicio)
        {
            consulta = consulta.Where(l => l.FechaVencimiento >= inicio);
        }

        return await consulta
            // Aqui no hace falta el truco de mandar los nulos al final: el filtro
            // ya dejo fuera los lotes sin vencimiento.
            .OrderBy(l => l.FechaVencimiento)
            .ThenBy(l => l.FechaIngreso)
            .ThenBy(l => l.Id)
            .Take(Math.Clamp(limite, LimiteMinimo, LimiteMaximo))
            .ToListAsync(cancellationToken);
    }

    // =========================================================================
    // ESCRITURA
    // =========================================================================

    public void Agregar(Lote lote) => _db.Lotes.Add(lote);

    public Task<int> GuardarCambiosAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);

    // =========================================================================
    // APOYO
    // =========================================================================

    /// <summary>
    /// El punto de partida de todas las lecturas: sin rastreo y con las tres
    /// navegaciones que necesita <c>LoteDto</c> -sede, producto y unidad base del
    /// producto-.
    ///
    /// Esta en un solo sitio porque olvidar uno de los Include no rompe nada
    /// visible: la consulta funciona y el DTO sale con el nombre en blanco.
    /// </summary>
    private IQueryable<Lote> ConsultaBase() =>
        _db.Lotes
            .AsNoTracking()
            .Include(l => l.Sucursal)
            .Include(l => l.Producto)
                .ThenInclude(p => p.UnidadBase);

    /// <summary>
    /// Orden FEFO: primero el que vence antes.
    ///
    /// El primer OrderBy es el que manda los lotes sin vencimiento al final.
    /// MySQL ordena los NULL primero, y sin esta linea lo que no caduca se
    /// despacharia antes que lo que si caduca, justo al reves de lo que pide
    /// FEFO. El bool se traduce a 0/1 y el 0 -tiene fecha- va delante.
    /// </summary>
    private static IOrderedQueryable<Lote> OrdenarFefo(IQueryable<Lote> consulta) =>
        consulta
            .OrderBy(l => l.FechaVencimiento == null)
            .ThenBy(l => l.FechaVencimiento)
            // Entre lotes que vencen el mismo dia, primero el que llego primero.
            .ThenBy(l => l.FechaIngreso)
            .ThenBy(l => l.Id);
}
