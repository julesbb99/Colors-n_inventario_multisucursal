using System.Globalization;
using Colorsin.Application.Comun.Auditoria;
using Colorsin.Application.Comun.Repositories;
using Colorsin.Application.Comun.Services;
using Colorsin.Application.Inventario.DTOs;
using Colorsin.Application.Inventario.Mapping;
using Colorsin.Application.Inventario.Repositories;
using Colorsin.Domain.Inventario;

namespace Colorsin.Application.Inventario.Services;

/// <inheritdoc cref="IInventarioService"/>
public sealed class InventarioService : IInventarioService
{
    /// <summary>Nombre del modulo en los eventos de auditoria.</summary>
    private const string Modulo = "Inventario";

    private readonly IInventarioRepository _inventario;
    private readonly IUnidadMedidaService _unidades;
    private readonly IProductoRepository _productos;
    private readonly ILoteRepository _lotes;
    private readonly ISucursalRepository _sucursales;
    private readonly IAuditoriaService _auditoria;
    private readonly OpcionesAlertasInventario _alertas;

    public InventarioService(
        IInventarioRepository inventario,
        IUnidadMedidaService unidades,
        IProductoRepository productos,
        ILoteRepository lotes,
        ISucursalRepository sucursales,
        IAuditoriaService auditoria,
        OpcionesAlertasInventario alertas)
    {
        _inventario = inventario;
        _unidades = unidades;
        _productos = productos;
        _lotes = lotes;
        _sucursales = sucursales;
        _auditoria = auditoria;
        _alertas = alertas;
    }

    // =========================================================================
    // CONSULTAS
    // =========================================================================

    public async Task<IReadOnlyList<ProductoDto>> ObtenerProductosAsync(
        string? categoria = null,
        CancellationToken cancellationToken = default)
    {
        // Dos consultas distintas y no una con filtro opcional, porque el
        // repositorio ya las tiene separadas y cada una tiene su propio orden.
        var productos = string.IsNullOrWhiteSpace(categoria)
            ? await _productos.ObtenerTodosAsync(cancellationToken)
            : await _productos.ObtenerPorCategoriaAsync(categoria.Trim(), cancellationToken);

        return productos.Select(p => p.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<InventarioSucursalDto>> ObtenerExistenciasAsync(
        int? sucursalId = null,
        bool incluirInactivas = false,
        CancellationToken cancellationToken = default)
    {
        var saldos = await _inventario.ObtenerExistenciasAsync(
            sucursalId, incluirInactivas, cancellationToken);

        return saldos.Select(s => s.ToDto()).ToList();
    }

    public async Task<InventarioSucursalDto?> ObtenerExistenciaPorIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var saldo = await _inventario.ObtenerExistenciaPorIdAsync(id, cancellationToken);
        return saldo?.ToDto();
    }

    // =========================================================================
    // ALTAS, BAJAS Y EDICION DE EXISTENCIAS
    // =========================================================================

    public async Task<ResultadoExistencia> CrearExistenciaAsync(
        CrearExistenciaDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (peticion.StockMinimo < 0m)
        {
            return ResultadoExistencia.Fallo(
                ErrorExistencia.StockMinimoInvalido,
                "El minimo de reposicion no puede ser negativo.");
        }

        // Producto y sede antes de tocar nada: la base los rechazaria igual por
        // clave foranea, pero un 404 con el id que fallo se lee y un error de
        // restriccion de MySQL, no. Mismo criterio que en CrearLoteAsync.
        var producto = await _productos.ObtenerPorIdAsync(peticion.ProductoId, cancellationToken);
        if (producto is null)
        {
            return ResultadoExistencia.Fallo(
                ErrorExistencia.ProductoNoEncontrado,
                $"No existe el producto {peticion.ProductoId}.");
        }

        var sucursal = await _sucursales.ObtenerPorIdAsync(peticion.SucursalId, cancellationToken);
        if (sucursal is null)
        {
            return ResultadoExistencia.Fallo(
                ErrorExistencia.SucursalNoEncontrada,
                $"No existe la sede {peticion.SucursalId}.");
        }

        var existente = await _inventario.ObtenerSaldoAsync(
            peticion.SucursalId, peticion.ProductoId, cancellationToken);

        if (existente is not null && existente.Activo)
        {
            return ResultadoExistencia.Fallo(
                ErrorExistencia.ExistenciaDuplicada,
                $"La sede '{sucursal.Nombre}' ya maneja '{producto.Nombre}'. " +
                "Si lo que quieres es cambiar el minimo de reposicion, editalo en la fila " +
                "que ya esta en el listado.");
        }

        return await _inventario.EjecutarEnTransaccionAsync(
            ct => existente is null
                // Fila nueva.
                ? AltaEnTransaccionAsync(peticion, producto.Nombre, sucursal.Nombre, usuarioId, ct)
                // Habia una dada de baja: se reactiva. El indice unico
                // (sucursal, producto) no deja crear otra, asi que sin esta rama
                // la persona se quedaria sin ninguna via desde la interfaz.
                : ReactivarPorAltaAsync(existente.Id, peticion.StockMinimo, usuarioId, ct),
            cancellationToken);
    }

    private async Task<ResultadoExistencia> AltaEnTransaccionAsync(
        CrearExistenciaDto peticion,
        string nombreProducto,
        string nombreSucursal,
        int usuarioId,
        CancellationToken cancellationToken)
    {
        var saldo = new InventarioSucursal
        {
            SucursalId = peticion.SucursalId,
            ProductoId = peticion.ProductoId,
            // En cero, no nulo: se sabe que no hay nada. La mercancia entra
            // despues por un ingreso, una compra o un traslado, que son las vias
            // que dejan asiento en el libro mayor.
            CantidadBase = 0m,
            StockMinimo = peticion.StockMinimo,
            // Sin compras todavia no hay costo que promediar. Lo fija la primera
            // entrada de mercancia.
            CostoPromedio = 0m,
            Activo = true
        };

        _inventario.AgregarSaldo(saldo);

        // Se guarda aqui para que MySQL asigne el id, que hace falta abajo.
        // Sigue dentro de la transaccion.
        await _inventario.GuardarCambiosAsync(cancellationToken);

        await _auditoria.RegistrarEventoAsync(
            Modulo,
            "CrearExistencia",
            usuarioId,
            $"existencia={saldo.Id} | " +
            $"producto={peticion.ProductoId} ('{nombreProducto}') | " +
            $"sucursal={peticion.SucursalId} ('{nombreSucursal}') | " +
            $"stockMinimo={Numero(peticion.StockMinimo)} | cantidadBase=0",
            cancellationToken);

        await _inventario.GuardarCambiosAsync(cancellationToken);

        return await ResultadoTrasGuardar(
            saldo.Id,
            $"'{nombreProducto}' queda habilitado en '{nombreSucursal}', con saldo cero. " +
            "La mercancia entra con un ingreso, una recepcion de compra o un traslado.",
            cancellationToken);
    }

    private async Task<ResultadoExistencia> ReactivarPorAltaAsync(
        int id,
        decimal stockMinimo,
        int usuarioId,
        CancellationToken cancellationToken)
    {
        var saldo = await _inventario.ObtenerExistenciaPorIdAsync(id, cancellationToken);
        if (saldo is null)
        {
            return ResultadoExistencia.Fallo(
                ErrorExistencia.ExistenciaNoEncontrada,
                $"No existe la existencia {id}.");
        }

        saldo.Activo = true;
        saldo.StockMinimo = stockMinimo;

        await _auditoria.RegistrarEventoAsync(
            Modulo,
            "ReactivarExistencia",
            usuarioId,
            $"existencia={saldo.Id} | via=alta | " +
            $"stockMinimo={Numero(stockMinimo)} | " +
            $"cantidadBase={Numero(saldo.CantidadBase)}",
            cancellationToken);

        await _inventario.GuardarCambiosAsync(cancellationToken);

        return await ResultadoTrasGuardar(
            saldo.Id,
            $"'{saldo.Producto?.Nombre}' ya existia en '{saldo.Sucursal?.Nombre}' pero estaba " +
            "deshabilitado, asi que se reactivo con el saldo que tenia.",
            cancellationToken);
    }

    public async Task<ResultadoExistencia> ActualizarExistenciaAsync(
        int id,
        ActualizarExistenciaDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (peticion.StockMinimo < 0m)
        {
            return ResultadoExistencia.Fallo(
                ErrorExistencia.StockMinimoInvalido,
                "El minimo de reposicion no puede ser negativo.");
        }

        var saldo = await _inventario.ObtenerExistenciaPorIdAsync(id, cancellationToken);
        if (saldo is null)
        {
            return ResultadoExistencia.Fallo(
                ErrorExistencia.ExistenciaNoEncontrada,
                $"No existe la existencia {id}.");
        }

        var anterior = saldo.StockMinimo;
        saldo.StockMinimo = peticion.StockMinimo;

        await _auditoria.RegistrarEventoAsync(
            Modulo,
            "ActualizarExistencia",
            usuarioId,
            $"existencia={saldo.Id} | " +
            $"stockMinimo={Numero(anterior)} -> {Numero(peticion.StockMinimo)}",
            cancellationToken);

        await _inventario.GuardarCambiosAsync(cancellationToken);

        return await ResultadoTrasGuardar(
            saldo.Id, "Minimo de reposicion actualizado.", cancellationToken);
    }

    public async Task<ResultadoExistencia> DesactivarExistenciaAsync(
        int id,
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        var saldo = await _inventario.ObtenerExistenciaPorIdAsync(id, cancellationToken);
        if (saldo is null)
        {
            return ResultadoExistencia.Fallo(
                ErrorExistencia.ExistenciaNoEncontrada,
                $"No existe la existencia {id}.");
        }

        if (!saldo.Activo)
        {
            return ResultadoExistencia.Fallo(
                ErrorExistencia.EstadoSinCambio,
                "Esa existencia ya estaba deshabilitada.");
        }

        // LA REGLA QUE PROTEGE EL CUADRE. Esconder una fila con mercancia dentro
        // haria que la suma de las existencias activas dejara de coincidir con lo
        // que hay en la bodega, y ningun asiento del libro mayor explicaria la
        // diferencia. Primero se saca el saldo por donde corresponde.
        if (saldo.CantidadBase != 0m)
        {
            var simbolo = saldo.Producto?.UnidadBase?.Simbolo ?? string.Empty;

            return ResultadoExistencia.Fallo(
                ErrorExistencia.TieneSaldo,
                $"'{saldo.Producto?.Nombre}' todavia tiene {Numero(saldo.CantidadBase)} {simbolo} " +
                $"en '{saldo.Sucursal?.Nombre}'. Saca primero esa mercancia -con un traslado, una " +
                "venta o un ajuste de salida- y entonces si se puede deshabilitar. Asi el " +
                "movimiento queda explicado en el libro mayor en vez de desaparecer del listado.");
        }

        saldo.Activo = false;

        await _auditoria.RegistrarEventoAsync(
            Modulo,
            "DesactivarExistencia",
            usuarioId,
            $"existencia={saldo.Id} | " +
            $"producto={saldo.ProductoId} ('{saldo.Producto?.Nombre}') | " +
            $"sucursal={saldo.SucursalId} ('{saldo.Sucursal?.Nombre}') | cantidadBase=0",
            cancellationToken);

        await _inventario.GuardarCambiosAsync(cancellationToken);

        return await ResultadoTrasGuardar(
            saldo.Id,
            $"'{saldo.Producto?.Nombre}' queda deshabilitado en '{saldo.Sucursal?.Nombre}'. " +
            "No se borro nada: su historial sigue completo y se puede volver a habilitar.",
            cancellationToken);
    }

    public async Task<ResultadoExistencia> ReactivarExistenciaAsync(
        int id,
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        var saldo = await _inventario.ObtenerExistenciaPorIdAsync(id, cancellationToken);
        if (saldo is null)
        {
            return ResultadoExistencia.Fallo(
                ErrorExistencia.ExistenciaNoEncontrada,
                $"No existe la existencia {id}.");
        }

        if (saldo.Activo)
        {
            return ResultadoExistencia.Fallo(
                ErrorExistencia.EstadoSinCambio,
                "Esa existencia ya estaba habilitada.");
        }

        saldo.Activo = true;

        await _auditoria.RegistrarEventoAsync(
            Modulo,
            "ReactivarExistencia",
            usuarioId,
            $"existencia={saldo.Id} | via=reactivar | " +
            $"cantidadBase={Numero(saldo.CantidadBase)}",
            cancellationToken);

        await _inventario.GuardarCambiosAsync(cancellationToken);

        return await ResultadoTrasGuardar(
            saldo.Id,
            $"'{saldo.Producto?.Nombre}' vuelve al listado de '{saldo.Sucursal?.Nombre}'.",
            cancellationToken);
    }

    /// <summary>
    /// Relee la fila ya guardada y arma el DTO.
    ///
    /// Se relee en vez de mapear la entidad en memoria porque en el alta las
    /// navegaciones -sede, producto, unidad- no estan cargadas, y el DTO las
    /// necesita para los nombres. Una consulta por operacion de escritura es
    /// barata y evita tener dos formas distintas de construir el mismo DTO.
    /// </summary>
    private async Task<ResultadoExistencia> ResultadoTrasGuardar(
        int id,
        string mensaje,
        CancellationToken cancellationToken)
    {
        var guardado = await _inventario.ObtenerExistenciaPorIdAsync(id, cancellationToken);

        return guardado is null
            ? ResultadoExistencia.Fallo(
                ErrorExistencia.ExistenciaNoEncontrada,
                $"La existencia {id} no se pudo releer despues de guardarla.")
            : ResultadoExistencia.Ok(guardado.ToDto(), mensaje);
    }

    /// <summary>Un decimal para la bitacora, con punto y sin separador de miles.</summary>
    private static string Numero(decimal valor) =>
        valor.ToString("0.####", CultureInfo.InvariantCulture);

    public async Task<IReadOnlyList<LoteDto>> ObtenerLotesPorVencimientoAsync(
        int sucursalId,
        int productoId,
        CancellationToken cancellationToken = default)
    {
        var lotes = await _inventario.ObtenerLotesPorVencimientoAsync(
            sucursalId, productoId, cancellationToken);

        return ADto(lotes);
    }

    public async Task<IReadOnlyList<MovimientoInventarioDto>> ObtenerMovimientosAsync(
        int? sucursalId = null,
        int? productoId = null,
        int limite = 100,
        CancellationToken cancellationToken = default)
    {
        var movimientos = await _inventario.ObtenerMovimientosAsync(
            sucursalId, productoId, limite, cancellationToken);
        return movimientos.Select(m => m.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<StockAlertaDto>> ObtenerAlertasStockBajoAsync(
        int? sucursalId = null,
        CancellationToken cancellationToken = default)
    {
        var saldos = await _inventario.ObtenerAlertasStockBajoAsync(sucursalId, cancellationToken);
        return saldos.Select(s => s.ToAlertaDto()).ToList();
    }

    // =========================================================================
    // LOTES: consulta
    // =========================================================================

    public async Task<IReadOnlyList<LoteDto>> ObtenerLotesAsync(
        int? sucursalId = null,
        int? productoId = null,
        bool soloConSaldo = false,
        int limite = 200,
        CancellationToken cancellationToken = default)
    {
        var lotes = await _lotes.ObtenerAsync(
            sucursalId, productoId, soloConSaldo, limite, cancellationToken);

        return ADto(lotes);
    }

    public async Task<LoteDto?> ObtenerLotePorIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var lote = await _lotes.ObtenerPorIdAsync(id, cancellationToken);
        return lote?.ToDto(HoyLocal());
    }

    public async Task<IReadOnlyList<LoteDto>> ObtenerLotesProximosAVencerAsync(
        int? sucursalId = null,
        int? diasUmbral = null,
        bool incluirVencidos = true,
        int limite = 200,
        CancellationToken cancellationToken = default)
    {
        // El umbral configurado cuando no se pide otro. Acotar y resolver el
        // valor por defecto vive en OpcionesAlertasInventario para que el tablero
        // y este modulo no puedan hacerlo distinto.
        var dias = _alertas.ResolverHorizonte(diasUmbral);

        // Una sola lectura del reloj para toda la consulta: leerlo dos veces -una
        // para el rango y otra para calcular los dias que faltan- daria resultados
        // incoherentes en una llamada lanzada justo a medianoche.
        var hoy = HoyLocal();

        var lotes = await _lotes.ObtenerProximosAVencerAsync(
            sucursalId,
            // Sin limite inferior entran los que ya vencieron, que es el caso
            // por defecto: son los mas urgentes de la lista.
            incluirVencidos ? null : hoy,
            hoy.AddDays(dias),
            limite,
            cancellationToken);

        return lotes.Select(l => l.ToDto(hoy)).ToList();
    }

    // =========================================================================
    // LOTES: alta y correccion
    //
    // LOS DOS VAN EN TRANSACCION, y no porque escriban en varias tablas -cada uno
    // toca `lotes` y `auditoria_eventos`- sino porque necesitan DOS guardados: el
    // primero para que MySQL asigne el id del lote, que hace falta en el detalle
    // de la auditoria. Sin transaccion, cada guardado se confirma por su cuenta y
    // el lote podria quedar creado con su evento perdido.
    //
    // La transaccion se pide a IInventarioRepository y no a ILoteRepository: los
    // dos repositorios comparten el AppDbContext de la peticion, asi que es la
    // misma unidad de trabajo, y tener un solo sitio donde se abre transaccion en
    // el modulo evita que se anide una dentro de otra por descuido.
    // =========================================================================

    public async Task<ResultadoLote> CrearLoteAsync(
        CrearLoteDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        var numero = peticion.NumeroLote?.Trim();
        if (string.IsNullOrEmpty(numero))
        {
            return ResultadoLote.Fallo(
                ErrorLote.NumeroLoteVacio,
                "El numero de lote es obligatorio: es lo que permite rastrear la mercancia " +
                "hasta el fabricante.");
        }

        // Se comprueban producto y sede antes de tocar nada. La base tiene claves
        // foraneas y rechazaria igual, pero un 404 con el id que fallo se lee; un
        // error de restriccion de MySQL, no.
        var producto = await _productos.ObtenerPorIdAsync(peticion.ProductoId, cancellationToken);
        if (producto is null)
        {
            return ResultadoLote.Fallo(
                ErrorLote.ProductoNoEncontrado,
                $"No existe el producto {peticion.ProductoId}.");
        }

        var sucursal = await _sucursales.ObtenerPorIdAsync(peticion.SucursalId, cancellationToken);
        if (sucursal is null)
        {
            return ResultadoLote.Fallo(
                ErrorLote.SucursalNoEncontrada,
                $"No existe la sede {peticion.SucursalId}.");
        }

        if (await _lotes.ExisteNumeroAsync(
                peticion.ProductoId, peticion.SucursalId, numero,
                excluyendoId: null, cancellationToken))
        {
            return ResultadoLote.Fallo(
                ErrorLote.NumeroLoteDuplicado,
                $"La sede '{sucursal.Nombre}' ya tiene un lote '{numero}' de " +
                $"'{producto.Nombre}'. Si volvio a llegar el mismo lote, no se abre otro: " +
                "se le suma cantidad con un ingreso o con una recepcion de compra.");
        }

        return await _inventario.EjecutarEnTransaccionAsync(
            ct => CrearEnTransaccionAsync(peticion, numero, usuarioId, producto, sucursal.Nombre, ct),
            cancellationToken);
    }

    private async Task<ResultadoLote> CrearEnTransaccionAsync(
        CrearLoteDto peticion,
        string numero,
        int usuarioId,
        Producto producto,
        string nombreSucursal,
        CancellationToken cancellationToken)
    {
        var lote = new Lote
        {
            ProductoId = peticion.ProductoId,
            SucursalId = peticion.SucursalId,
            NumeroLote = numero,
            FechaVencimiento = peticion.FechaVencimiento,
            // VACIO, no nulo. Los dos se comportan igual en las consultas -la
            // condicion `> 0` descarta ambos- pero un cero dice "este lote no
            // tiene existencias" y un nulo dice "no se sabe". Aqui si se sabe.
            CantidadBase = 0m,
            FechaIngreso = peticion.FechaIngreso
            // Si FechaIngreso viene nula la pone la base con CURRENT_TIMESTAMP.
        };

        _lotes.Agregar(lote);

        // Se guarda aqui para que MySQL asigne el id, que hace falta abajo.
        // Sigue dentro de la transaccion.
        await _lotes.GuardarCambiosAsync(cancellationToken);

        await _auditoria.RegistrarEventoAsync(
            Modulo,
            "CrearLote",
            usuarioId,
            $"lote={lote.Id} ('{numero}') | " +
            $"producto={peticion.ProductoId} ('{producto.Nombre}') | " +
            $"sucursal={peticion.SucursalId} ('{nombreSucursal}') | " +
            $"vencimiento={Fecha(peticion.FechaVencimiento)} | cantidadBase=0",
            cancellationToken);

        await _lotes.GuardarCambiosAsync(cancellationToken);

        // Se rearma el DTO a mano en vez de releer el lote: las navegaciones no
        // estan cargadas -la entidad se acaba de crear- y una consulta extra solo
        // para los nombres que ya tenemos aqui no aporta nada.
        var hoy = HoyLocal();
        var dias = lote.FechaVencimiento is null
            ? (int?)null
            : lote.FechaVencimiento.Value.DayNumber - hoy.DayNumber;

        return ResultadoLote.Ok(
            new LoteDto(
                lote.Id,
                lote.ProductoId,
                producto.Nombre,
                lote.SucursalId,
                nombreSucursal,
                lote.NumeroLote,
                lote.FechaVencimiento,
                lote.CantidadBase,
                producto.UnidadBase?.Simbolo,
                lote.FechaIngreso,
                dias,
                dias < 0),
            "Lote creado. Queda en cero: la mercancia entra con un movimiento de ingreso " +
            "o con una recepcion de compra.");
    }

    public async Task<ResultadoLote> ActualizarLoteAsync(
        int id,
        ActualizarLoteDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        var numero = peticion.NumeroLote?.Trim();
        if (string.IsNullOrEmpty(numero))
        {
            return ResultadoLote.Fallo(
                ErrorLote.NumeroLoteVacio,
                "El numero de lote es obligatorio y no se puede dejar en blanco.");
        }

        var lote = await _lotes.ObtenerParaEditarAsync(id, cancellationToken);
        if (lote is null)
        {
            return ResultadoLote.Fallo(ErrorLote.LoteNoEncontrado, $"No existe el lote {id}.");
        }

        // Se excluye el propio lote, o una edicion que no cambia el numero
        // chocaria consigo misma.
        if (await _lotes.ExisteNumeroAsync(
                lote.ProductoId, lote.SucursalId, numero, id, cancellationToken))
        {
            return ResultadoLote.Fallo(
                ErrorLote.NumeroLoteDuplicado,
                $"Ya hay otro lote con el numero '{numero}' para ese producto en esa sede.");
        }

        return await _inventario.EjecutarEnTransaccionAsync(
            ct => ActualizarEnTransaccionAsync(lote, numero, peticion, usuarioId, ct),
            cancellationToken);
    }

    private async Task<ResultadoLote> ActualizarEnTransaccionAsync(
        Lote lote,
        string numero,
        ActualizarLoteDto peticion,
        int usuarioId,
        CancellationToken cancellationToken)
    {
        // Los valores anteriores se guardan ANTES de tocar la entidad: sin ellos
        // la auditoria diria en que quedo el lote pero no de donde venia, que es
        // justo lo que se necesita para revisar una correccion.
        var numeroAnterior = lote.NumeroLote;
        var vencimientoAnterior = lote.FechaVencimiento;

        lote.NumeroLote = numero;
        lote.FechaVencimiento = peticion.FechaVencimiento;

        await _auditoria.RegistrarEventoAsync(
            Modulo,
            "ActualizarLote",
            usuarioId,
            $"lote={lote.Id} | producto={lote.ProductoId} | sucursal={lote.SucursalId} | " +
            $"numero: '{numeroAnterior}' -> '{numero}' | " +
            $"vencimiento: {Fecha(vencimientoAnterior)} -> {Fecha(peticion.FechaVencimiento)}",
            cancellationToken);

        // Un solo guardado: el UPDATE del lote y el INSERT del evento salen
        // juntos porque comparten el mismo contexto.
        await _lotes.GuardarCambiosAsync(cancellationToken);

        return ResultadoLote.Ok(lote.ToDto(HoyLocal()), "Lote actualizado.");
    }

    // =========================================================================
    // REGISTRO DE MOVIMIENTOS
    // =========================================================================

    public async Task<ResultadoMovimiento> RegistrarMovimientoAsync(
        RegistrarMovimientoDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        // --- 1. Validaciones que no tocan la base ------------------------------
        if (peticion.Cantidad <= 0)
        {
            return ResultadoMovimiento.Fallo(
                ErrorMovimiento.CantidadInvalida,
                $"La cantidad debe ser mayor que cero; llego {Num(peticion.Cantidad)}. " +
                "El signo lo determina el tipo de movimiento, no el numero.");
        }

        // --- 2. Producto y su unidad base --------------------------------------
        var producto = await _productos.ObtenerPorIdAsync(peticion.ProductoId, cancellationToken);
        if (producto is null)
        {
            return ResultadoMovimiento.Fallo(
                ErrorMovimiento.ProductoNoEncontrado,
                $"No existe el producto {peticion.ProductoId}.");
        }

        if (producto.UnidadBaseId is null)
        {
            return ResultadoMovimiento.Fallo(
                ErrorMovimiento.ProductoSinUnidadBase,
                $"El producto '{producto.Nombre}' no tiene unidad base definida, " +
                "asi que no hay unidad a la cual convertir la cantidad.");
        }

        // --- 3. Conversion a unidad base ---------------------------------------
        var conversion = await ConvertirAUnidadBaseAsync(
            peticion.Cantidad, peticion.UnidadId, producto.UnidadBaseId.Value, cancellationToken);

        if (!conversion.Exito)
        {
            return conversion.Resultado!;
        }

        var cantidadBase = conversion.CantidadBase;

        // --- 4. Escritura, toda dentro de una transaccion -----------------------
        // El bloqueo de las filas de saldo y de lote vive mientras la
        // transaccion este abierta; por eso la lectura del saldo va adentro.
        //
        // Dentro de la transaccion NO se prepara ningun cambio hasta que todas
        // las validaciones pasan: asi una regla de negocio incumplida sale con
        // la transaccion limpia, sin nada que revertir.
        return await _inventario.EjecutarEnTransaccionAsync(
            ct => RegistrarEnTransaccionAsync(peticion, usuarioId, producto, cantidadBase, ct),
            cancellationToken);
    }

    private async Task<ResultadoMovimiento> RegistrarEnTransaccionAsync(
        RegistrarMovimientoDto peticion,
        int usuarioId,
        Producto producto,
        decimal cantidadBase,
        CancellationToken cancellationToken)
    {
        var esIngreso = peticion.TipoMovimiento == TipoMovimiento.Ingreso;

        var saldo = await _inventario.ObtenerSaldoParaActualizarAsync(
            peticion.SucursalId, peticion.ProductoId, cancellationToken);

        // Sin fila de saldo el disponible es cero. Para un retiro eso ya es
        // motivo de rechazo; para un ingreso, la fila se crea mas abajo.
        var disponible = saldo?.CantidadBase ?? 0m;

        if (saldo is null && !esIngreso)
        {
            return ResultadoMovimiento.Fallo(
                ErrorMovimiento.SaldoNoEncontrado,
                $"La sede {peticion.SucursalId} no tiene saldo registrado del producto " +
                $"'{producto.Nombre}'; no se puede retirar de lo que nunca entro.");
        }

        // --- 5. Lote, si el movimiento se imputa a uno --------------------------
        Lote? lote = null;
        if (peticion.LoteId is int loteId)
        {
            lote = await _inventario.ObtenerLoteParaActualizarAsync(loteId, cancellationToken);

            if (lote is null)
            {
                return ResultadoMovimiento.Fallo(
                    ErrorMovimiento.LoteNoEncontrado, $"No existe el lote {loteId}.");
            }

            // Sin esta validacion se podria descontar del lote de otra sede o de
            // otro producto, y el saldo por lote dejaria de cuadrar con el
            // consolidado sin que nada avisara.
            if (lote.ProductoId != peticion.ProductoId || lote.SucursalId != peticion.SucursalId)
            {
                return ResultadoMovimiento.Fallo(
                    ErrorMovimiento.LoteNoCorresponde,
                    $"El lote '{lote.NumeroLote}' pertenece al producto {lote.ProductoId} " +
                    $"en la sede {lote.SucursalId}, pero el movimiento es del producto " +
                    $"{peticion.ProductoId} en la sede {peticion.SucursalId}.");
            }

            var cantidadLote = lote.CantidadBase ?? 0m;
            if (!esIngreso && cantidadLote < cantidadBase)
            {
                return ResultadoMovimiento.Fallo(
                    ErrorMovimiento.StockLoteInsuficiente,
                    $"El lote '{lote.NumeroLote}' tiene {Num(cantidadLote)} y se " +
                    $"intentan retirar {Num(cantidadBase)}.");
            }
        }

        // --- 6. Regla de negocio: un retiro no puede dejar el saldo negativo ----
        if (!esIngreso && disponible < cantidadBase)
        {
            return ResultadoMovimiento.Fallo(
                ErrorMovimiento.StockInsuficiente,
                $"Stock insuficiente de '{producto.Nombre}' en la sede {peticion.SucursalId}: " +
                $"hay {Num(disponible)} y se intentan retirar {Num(cantidadBase)}.");
        }

        // --- 7. Aplicar. Desde aqui ya no hay rechazos ---------------------------
        var delta = esIngreso ? cantidadBase : -cantidadBase;
        var saldoResultante = disponible + delta;

        if (saldo is null)
        {
            // Primer ingreso de este producto en esta sede: se abre la fila. El
            // indice unico (sucursal, producto) impide que dos ingresos
            // simultaneos creen dos filas; el segundo falla y se revierte.
            _inventario.AgregarSaldo(new InventarioSucursal
            {
                SucursalId = peticion.SucursalId,
                ProductoId = peticion.ProductoId,
                CantidadBase = saldoResultante,
                StockMinimo = 0m,
                CostoPromedio = 0m
            });
        }
        else
        {
            _inventario.ActualizarCantidadBase(saldo, saldoResultante);
        }

        if (lote is not null)
        {
            _inventario.ActualizarCantidadLote(lote, (lote.CantidadBase ?? 0m) + delta);
        }

        var movimiento = new MovimientoInventario
        {
            SucursalId = peticion.SucursalId,
            ProductoId = peticion.ProductoId,
            UsuarioId = usuarioId,
            Tipo = peticion.TipoMovimiento,
            Motivo = peticion.Motivo,
            Cantidad = peticion.Cantidad,
            UnidadId = peticion.UnidadId,
            CantidadBase = cantidadBase,
            LoteId = peticion.LoteId,
            Observaciones = peticion.Observaciones
            // Fecha la pone la base con CURRENT_TIMESTAMP: en un libro mayor
            // todas las marcas de tiempo deben venir del mismo reloj, no del
            // de cada maquina que ejecute la API.
        };
        _inventario.AgregarMovimiento(movimiento);

        // Se guarda aqui para que MySQL asigne el id del movimiento, que hace
        // falta en el detalle de la auditoria. Sigue dentro de la transaccion.
        await _inventario.GuardarCambiosAsync(cancellationToken);

        // --- 8. Auditoria, en la MISMA transaccion ------------------------------
        await _auditoria.RegistrarEventoAsync(
            Modulo,
            lote is null ? "RegistrarMovimiento" : "RegistrarMovimientoConLote",
            usuarioId,
            ConstruirDetalle(peticion, producto.Nombre, cantidadBase, saldoResultante, lote),
            cancellationToken);

        await _inventario.GuardarCambiosAsync(cancellationToken);

        return ResultadoMovimiento.Ok(movimiento.Id, cantidadBase, saldoResultante);
    }

    // =========================================================================
    // CONVERSION DE UNIDADES
    // =========================================================================

    private readonly record struct ResultadoConversion(
        bool Exito,
        decimal CantidadBase,
        ResultadoMovimiento? Resultado);

    /// <summary>
    /// Busca los factores de las dos unidades y delega la cuenta en
    /// <see cref="ConversorUnidades"/>, que es donde vive la formula.
    ///
    /// Aqui solo queda lo que necesita la base de datos (buscar los factores) y
    /// la traduccion del resultado a los codigos de error de este modulo.
    /// Compras hace lo mismo con sus propios codigos, sobre la misma formula.
    /// </summary>
    private async Task<ResultadoConversion> ConvertirAUnidadBaseAsync(
        decimal cantidad,
        int unidadId,
        int unidadBaseId,
        CancellationToken cancellationToken)
    {
        decimal? factorUnidad = null;
        decimal? factorUnidadBase = null;

        // Si las unidades coinciden no hace falta consultar nada: el conversor
        // devuelve la cantidad tal cual.
        if (unidadId != unidadBaseId)
        {
            var origen = await _unidades.ObtenerFactorConversionLitrosAsync(unidadId, cancellationToken);
            if (!origen.UnidadExiste)
            {
                return new ResultadoConversion(false, 0m, ResultadoMovimiento.Fallo(
                    ErrorMovimiento.UnidadNoEncontrada,
                    $"No existe la unidad de medida {unidadId}."));
            }

            var destino = await _unidades.ObtenerFactorConversionLitrosAsync(unidadBaseId, cancellationToken);
            if (!destino.UnidadExiste)
            {
                return new ResultadoConversion(false, 0m, ResultadoMovimiento.Fallo(
                    ErrorMovimiento.UnidadNoEncontrada,
                    $"No existe la unidad base {unidadBaseId} que declara el producto."));
            }

            factorUnidad = origen.FactorLitros;
            factorUnidadBase = destino.FactorLitros;
        }

        var conversion = ConversorUnidades.ABaseDelProducto(
            cantidad, unidadId, factorUnidad, unidadBaseId, factorUnidadBase);

        return conversion.Estado switch
        {
            EstadoConversion.Ok =>
                new ResultadoConversion(true, conversion.CantidadBase, null),

            EstadoConversion.SinFactor =>
                new ResultadoConversion(false, 0m, ResultadoMovimiento.Fallo(
                    ErrorMovimiento.ConversionImposible,
                    $"No hay conversion entre la unidad {unidadId} y la unidad base " +
                    $"{unidadBaseId}: alguna de las dos no tiene factor a litros.")),

            _ =>
                new ResultadoConversion(false, 0m, ResultadoMovimiento.Fallo(
                    ErrorMovimiento.CantidadBaseCero,
                    $"Convertida a unidad base, la cantidad {Num(cantidad)} se redondea a cero " +
                    $"con {ConversorUnidades.DecimalesCantidadBase} decimales. " +
                    "Registra una cantidad mayor."))
        };
    }

    // =========================================================================
    // AUXILIARES
    // =========================================================================

    private static string ConstruirDetalle(
        RegistrarMovimientoDto peticion,
        string nombreProducto,
        decimal cantidadBase,
        decimal saldoResultante,
        Lote? lote)
    {
        var detalle =
            $"{peticion.TipoMovimiento}/{peticion.Motivo} | " +
            $"sucursal={peticion.SucursalId} | " +
            $"producto={peticion.ProductoId} ('{nombreProducto}') | " +
            $"cantidad={Num(peticion.Cantidad)} unidad={peticion.UnidadId} | " +
            $"cantidadBase={Num(cantidadBase)} | " +
            $"saldoResultante={Num(saldoResultante)}";

        if (lote is not null)
        {
            detalle += $" | lote={lote.Id} ('{lote.NumeroLote}')";
        }

        if (!string.IsNullOrWhiteSpace(peticion.Observaciones))
        {
            detalle += $" | obs={peticion.Observaciones}";
        }

        return detalle;
    }

    /// <summary>
    /// Fecha de hoy segun el reloj local del servidor de la aplicacion.
    ///
    /// OJO CON LA ZONA HORARIA. `lotes.fecha_ingreso` la llena MySQL con
    /// CURRENT_TIMESTAMP, o sea con el reloj del CONTENEDOR, no con el de la
    /// aplicacion. Hoy los dos estan en -05:00 y coinciden, pero eso no esta
    /// fijado en el docker-compose: si el contenedor quedara en UTC, un lote que
    /// vence hoy contaria como vencido cinco horas antes de tiempo.
    /// </summary>
    private static DateOnly HoyLocal() => DateOnly.FromDateTime(DateTime.Now);

    /// <summary>
    /// Convierte una lista de lotes leyendo el reloj UNA sola vez.
    ///
    /// Si se leyera por lote, una consulta lanzada justo a medianoche daria dias
    /// distintos para lotes con la misma fecha de vencimiento.
    /// </summary>
    private static IReadOnlyList<LoteDto> ADto(IReadOnlyList<Lote> lotes)
    {
        var hoy = HoyLocal();
        return lotes.Select(l => l.ToDto(hoy)).ToList();
    }

    /// <summary>
    /// Formatea una fecha para el detalle de auditoria, en formato ISO y con el
    /// caso nulo explicito: "(sin fecha)" se lee, una cadena vacia no.
    /// </summary>
    private static string Fecha(DateOnly? fecha) =>
        fecha?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "(sin fecha)";

    /// <summary>
    /// Formatea con punto decimal siempre.
    ///
    /// Sin esto, el detalle de auditoria saldria con coma en un servidor con
    /// configuracion regional colombiana y con punto en otro, y la misma
    /// bitacora quedaria con dos formatos segun donde se ejecute.
    /// </summary>
    private static string Num(decimal valor) =>
        valor.ToString("0.####", CultureInfo.InvariantCulture);
}
