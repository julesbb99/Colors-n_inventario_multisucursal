using System.Globalization;
using Colorsin.Application.Comun.Auditoria;
using Colorsin.Application.Comun.Services;
using Colorsin.Application.Inventario;
using Colorsin.Application.Inventario.Repositories;
using Colorsin.Application.Ventas.DTOs;
using Colorsin.Application.Ventas.Mapping;
using Colorsin.Application.Ventas.Repositories;
using Colorsin.Domain.Inventario;
using Colorsin.Domain.Ventas;

namespace Colorsin.Application.Ventas.Services;

/// <inheritdoc cref="IVentasService"/>
public sealed class VentasService : IVentasService
{
    /// <summary>Nombre del modulo en los eventos de auditoria.</summary>
    private const string Modulo = "Ventas";

    private readonly IVentaRepository _ventas;
    private readonly IClienteRepository _clientes;
    private readonly IInventarioRepository _inventario;
    private readonly IProductoRepository _productos;
    private readonly IUnidadMedidaService _unidades;
    private readonly IAuditoriaService _auditoria;

    /// <summary>
    /// Dos dependencias mas de las pedidas, y por que:
    ///
    /// - <see cref="IProductoRepository"/>: hace falta la unidad base de cada
    ///   producto para convertir la cantidad vendida, y el nombre para los
    ///   mensajes de error.
    /// - <see cref="IUnidadMedidaService"/>: <c>ConversorUnidades</c> es una
    ///   clase estatica de aritmetica pura, asi que no se inyecta ni consulta
    ///   nada; los factores de conversion hay que buscarlos, y este servicio es
    ///   el que ya lo hace en los otros modulos.
    /// </summary>
    public VentasService(
        IVentaRepository ventas,
        IClienteRepository clientes,
        IInventarioRepository inventario,
        IProductoRepository productos,
        IUnidadMedidaService unidades,
        IAuditoriaService auditoria)
    {
        _ventas = ventas;
        _clientes = clientes;
        _inventario = inventario;
        _productos = productos;
        _unidades = unidades;
        _auditoria = auditoria;
    }

    // =========================================================================
    // CONSULTAS
    // =========================================================================

    public async Task<IReadOnlyList<ClienteDto>> ObtenerClientesAsync(
        CancellationToken cancellationToken = default)
    {
        var clientes = await _clientes.ObtenerTodosAsync(cancellationToken);
        return clientes.Select(c => c.ToDto()).ToList();
    }

    public async Task<ClienteDto?> ObtenerClientePorIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var cliente = await _clientes.ObtenerPorIdAsync(id, cancellationToken);
        return cliente?.ToDto();
    }

    public async Task<ClienteDto?> ObtenerClientePorDocumentoAsync(
        string documento,
        CancellationToken cancellationToken = default)
    {
        var cliente = await _clientes.ObtenerPorDocumentoAsync(documento, cancellationToken);
        return cliente?.ToDto();
    }

    public async Task<IReadOnlyList<VentaDto>> ObtenerVentasAsync(
        int? sucursalId = null,
        int? clienteId = null,
        DateTime? desde = null,
        DateTime? hasta = null,
        int limite = 100,
        CancellationToken cancellationToken = default)
    {
        var ventas = await _ventas.ObtenerAsync(
            sucursalId, clienteId, desde, hasta, limite, cancellationToken);

        return ventas.Select(v => v.ToDto(incluirDetalle: false)).ToList();
    }

    public async Task<VentaDto?> ObtenerVentaPorIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var venta = await _ventas.ObtenerPorIdAsync(id, cancellationToken);
        return venta?.ToDto();
    }

    // =========================================================================
    // CLIENTES
    // =========================================================================

    public async Task<ClienteDto> CrearClienteAsync(
        CrearClienteDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        var razonSocial = Limpiar(peticion.RazonSocial);
        var documento = Limpiar(peticion.Documento);

        if (razonSocial is null)
        {
            throw new VentaInvalidaException("El cliente necesita razon social o nombre.");
        }

        if (documento is null)
        {
            throw new VentaInvalidaException(
                "El cliente necesita documento: es la cedula o el NIT, y es por lo que se " +
                "le busca en el mostrador.");
        }

        // 'Natural' y 'Juridica' se escriben igual en el enum y en la base, asi
        // que basta con parsear sin distinguir mayusculas. El mensaje enumera
        // los valores validos porque el DTO los recibe como texto libre.
        if (!Enum.TryParse<TipoPersona>(peticion.TipoPersona, ignoreCase: true, out var tipo))
        {
            throw new VentaInvalidaException(
                $"El tipo de persona '{peticion.TipoPersona}' no es valido: " +
                "solo 'Natural' o 'Juridica'.");
        }

        // Se comprueba antes de insertar para poder devolver el cliente que ya
        // existe. El indice unico `uq_clientes_documento` sigue siendo la
        // garantia de verdad -entre esta consulta y el INSERT cabe otra alta-,
        // pero sin esto el choque saldria como un error 500 sin explicacion.
        var existente = await _clientes.ObtenerPorDocumentoAsync(documento, cancellationToken);
        if (existente is not null)
        {
            throw new ClienteDuplicadoException(documento, existente.Id, existente.RazonSocial);
        }

        var cliente = new Cliente
        {
            RazonSocial = razonSocial,
            TipoPersona = tipo,
            Documento = documento,
            Telefono = Limpiar(peticion.Telefono),
            Email = Limpiar(peticion.Email),
            Direccion = Limpiar(peticion.Direccion)
        };

        _clientes.Agregar(cliente);
        await _clientes.GuardarCambiosAsync(cancellationToken);

        await _auditoria.RegistrarEventoAsync(
            Modulo,
            "CrearCliente",
            usuarioId,
            $"Cliente {cliente.Id} '{cliente.RazonSocial}' ({tipo}, doc {cliente.Documento}).",
            cancellationToken);

        await _clientes.GuardarCambiosAsync(cancellationToken);

        return cliente.ToDto();
    }

    // =========================================================================
    // PRECIO DE VENTA
    // =========================================================================

    public async Task<PrecioVentaDto> ObtenerPrecioVentaAsync(
        int productoId,
        int? sucursalId = null,
        CancellationToken cancellationToken = default)
    {
        var producto = await ObtenerProductoAsync(productoId, cancellationToken);

        var ultima = await _ventas.ObtenerUltimaVentaDeProductoAsync(
            productoId, sucursalId, cancellationToken);

        var costo = await _inventario.ObtenerCostoPromedioAsync(productoId, cancellationToken);

        // El margen solo tiene sentido con las dos cifras y con un costo que no
        // sea cero. Se calcula SOBRE EL COSTO -"deja un 30% sobre lo que nos
        // cuesta"- y sale negativo cuando el precio fijado esta por debajo, que
        // es justo la senal que hay que ver.
        var margen = producto.PrecioVenta is decimal precio && costo is decimal c && c > 0m
            ? Math.Round(((precio - c) / c) * 100m, 2, MidpointRounding.AwayFromZero)
            : (decimal?)null;

        return new PrecioVentaDto(
            producto.Id,
            producto.Nombre,
            producto.UnidadBase?.Simbolo,
            producto.PrecioVenta,
            ultima is null
                ? null
                : new UltimaVentaDto(
                    ultima.VentaId,
                    ultima.Venta?.Fecha,
                    ultima.Cantidad,
                    ultima.UnidadId,
                    ultima.Unidad?.Simbolo,
                    ultima.PrecioUnitario,
                    ultima.Descuento),
            costo,
            margen);
    }

    public async Task<PrecioVentaDto> FijarPrecioVentaAsync(
        int productoId,
        GuardarPrecioVentaDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        // Con seguimiento: esta fila se modifica. Con AsNoTracking el cambio se
        // perderia sin que nada avisara.
        var producto = await _productos.ObtenerParaActualizarAsync(productoId, cancellationToken)
            ?? throw new ReferenciaVentaNoEncontradaException(
                $"No existe el producto {productoId}.");

        if (peticion.PrecioVenta is decimal nuevo && nuevo <= 0m)
        {
            throw new VentaInvalidaException(
                $"El precio de venta debe ser mayor que cero; llego {Num(nuevo)}. " +
                "Para dejar el producto sin precio, manda el precio nulo.");
        }

        var anterior = producto.PrecioVenta;
        producto.PrecioVenta = peticion.PrecioVenta;

        await _productos.GuardarCambiosAsync(cancellationToken);

        await _auditoria.RegistrarEventoAsync(
            Modulo,
            "FijarPrecioVenta",
            usuarioId,
            $"Producto {producto.Id} '{producto.Nombre}': " +
            $"{(anterior is null ? "sin precio" : Num(anterior.Value))} -> " +
            $"{(producto.PrecioVenta is null ? "sin precio" : Num(producto.PrecioVenta.Value))} " +
            $"por {producto.UnidadBase?.Simbolo ?? "unidad base"}.",
            cancellationToken);

        await _productos.GuardarCambiosAsync(cancellationToken);

        return await ObtenerPrecioVentaAsync(productoId, null, cancellationToken);
    }

    /// <summary>
    /// Recorta los espacios y convierte el vacio en nulo.
    ///
    /// Un campo opcional que llega como cadena vacia no es "sin dato": guardado
    /// asi, una busqueda por telefono vacio encontraria filas, y la pantalla
    /// pintaria un hueco donde deberia decir que no hay.
    /// </summary>
    private static string? Limpiar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    // =========================================================================
    // REGISTRAR VENTA
    // =========================================================================

    public async Task<VentaRegistradaDto> RegistrarVentaAsync(
        CrearVentaDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        ValidarForma(peticion, usuarioId);

        // El cliente se comprueba antes de abrir la transaccion: la FK lo
        // impondria igual, pero como DbUpdateException al guardar, que llega al
        // cliente como error 500 en vez de como una regla de negocio.
        if (!await _clientes.ExisteAsync(peticion.ClienteId, cancellationToken))
        {
            throw new ReferenciaVentaNoEncontradaException(
                $"No existe el cliente {peticion.ClienteId}.");
        }

        return await _ventas.EjecutarEnTransaccionAsync(
            ct => RegistrarEnTransaccionAsync(peticion, usuarioId, ct),
            cancellationToken);
    }

    /// <summary>
    /// Validaciones que no necesitan tocar la base. Se hacen antes de abrir la
    /// transaccion para no mantenerla abierta mientras se revisa lo obvio.
    /// </summary>
    private static void ValidarForma(CrearVentaDto peticion, int usuarioId)
    {
        if (peticion.Lineas is null || peticion.Lineas.Count == 0)
        {
            throw new VentaInvalidaException("La venta debe tener al menos una linea.");
        }

        if (usuarioId <= 0)
        {
            throw new VentaInvalidaException(
                $"Hay que indicar el usuario que registra la venta; llego {usuarioId}.");
        }

        for (var i = 0; i < peticion.Lineas.Count; i++)
        {
            var linea = peticion.Lineas[i];
            var numero = i + 1;

            if (linea.Cantidad <= 0m)
            {
                throw new VentaInvalidaException(
                    $"Linea {numero}: la cantidad debe ser mayor que cero; " +
                    $"llego {Num(linea.Cantidad)}.");
            }

            // La columna `venta_detalle.cantidad` solo guarda 2 decimales. Una
            // cantidad que se redondea a cero se rechaza aqui, con un mensaje
            // util, en vez de dejar que MySQL responda con el error 3819 del
            // CHECK `chk_vd_cantidad`.
            if (MapeosVentas.RedondearCantidad(linea.Cantidad) <= 0m)
            {
                throw new VentaInvalidaException(
                    $"Linea {numero}: la cantidad {Num(linea.Cantidad)} se redondea a cero " +
                    $"con los {MapeosVentas.DecimalesCantidadVenta} decimales que guarda la " +
                    "linea de venta. Registra una cantidad mayor.");
            }

            if (linea.PrecioUnitario is < 0m)
            {
                throw new VentaInvalidaException(
                    $"Linea {numero}: el precio unitario no puede ser negativo; " +
                    $"llego {Num(linea.PrecioUnitario.Value)}.");
            }

            if (linea.Descuento is < 0m or > 100m)
            {
                throw new VentaInvalidaException(
                    $"Linea {numero}: el descuento es un porcentaje entre 0 y 100; " +
                    $"llego {Num(linea.Descuento)}.");
            }
        }
    }

    /// <summary>Una linea de la peticion ya convertida a unidad base y con precio resuelto.</summary>
    /// <param name="Precio">
    /// El precio que de verdad se va a cobrar por unidad de venta: el que trajo
    /// la linea, o el de lista convertido a esa unidad. Nunca es nulo, a
    /// diferencia de <c>Peticion.PrecioUnitario</c>.
    /// </param>
    private sealed record LineaPreparada(
        CrearLineaVentaDto Peticion,
        Producto Producto,
        decimal Cantidad,
        decimal CantidadBase,
        decimal Precio,
        VentaDetalle Detalle);

    private async Task<VentaRegistradaDto> RegistrarEnTransaccionAsync(
        CrearVentaDto peticion,
        int usuarioId,
        CancellationToken cancellationToken)
    {
        // --- 1. Convertir cada linea a unidad base -------------------------------
        var preparadas = new List<LineaPreparada>(peticion.Lineas.Count);

        for (var i = 0; i < peticion.Lineas.Count; i++)
        {
            var linea = peticion.Lineas[i];
            var producto = await ObtenerProductoAsync(linea.ProductoId, cancellationToken);
            var cantidad = MapeosVentas.RedondearCantidad(linea.Cantidad);
            var cantidadBase = await ConvertirAUnidadBaseAsync(
                cantidad, linea.UnidadId, producto, cancellationToken);
            var precio = await ResolverPrecioAsync(linea, producto, i + 1, cancellationToken);

            preparadas.Add(new LineaPreparada(
                linea, producto, cantidad, cantidadBase, precio,
                new VentaDetalle
                {
                    ProductoId = linea.ProductoId,
                    Cantidad = cantidad,
                    UnidadId = linea.UnidadId,
                    // El precio RESUELTO, no el que vino. Antes se guardaba
                    // `linea.PrecioUnitario` tal cual, asi que una linea sin
                    // precio dejaba la columna en NULL y el total la contaba
                    // como cero: la venta quedaba registrada regalada y nada lo
                    // decia.
                    PrecioUnitario = precio,
                    Descuento = linea.Descuento
                }));
        }

        // --- 2. Validacion estricta de stock, AGRUPANDO por producto -------------
        // Agrupar es lo que evita el hueco: con dos lineas de 60 del mismo
        // producto contra un saldo de 100, validar linea por linea las aprobaria
        // las dos y el saldo terminaria en -20. Hay que medir la suma.
        var requeridoPorProducto = preparadas
            .GroupBy(p => p.Producto.Id)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.CantidadBase));

        var saldos = new Dictionary<int, InventarioSucursal>();

        foreach (var (productoId, requerido) in requeridoPorProducto)
        {
            var producto = preparadas.First(p => p.Producto.Id == productoId).Producto;

            // FOR UPDATE: mantiene bloqueada la fila del saldo hasta el final de
            // la transaccion. Sin eso, dos ventas simultaneas leen el mismo
            // saldo y las dos pasan la validacion.
            var saldo = await _inventario.ObtenerSaldoParaActualizarAsync(
                peticion.SucursalId, productoId, cancellationToken);

            var disponible = saldo?.CantidadBase ?? 0m;
            var simbolo = producto.UnidadBase?.Simbolo ?? string.Empty;

            if (saldo is null || disponible < requerido)
            {
                throw new StockInsuficienteException(
                    peticion.SucursalId, productoId, producto.Nombre,
                    disponible, requerido, simbolo);
            }

            saldos[productoId] = saldo;
        }

        // --- 3. Crear la venta con su detalle ------------------------------------
        var total = preparadas.Sum(p => MapeosVentas.CalcularSubtotalNeto(
            p.Cantidad, p.Precio, p.Peticion.Descuento));

        var venta = new Venta
        {
            ClienteId = peticion.ClienteId,
            SucursalId = peticion.SucursalId,
            UsuarioId = usuarioId,
            Total = total
            // Fecha la pone la base con CURRENT_TIMESTAMP: en un libro de
            // ventas todas las marcas de tiempo deben venir del mismo reloj.
        };

        foreach (var preparada in preparadas)
        {
            venta.Detalles.Add(preparada.Detalle);
        }

        _ventas.AgregarVenta(venta);

        // --- 4. Descontar stock y repartir lotes ---------------------------------
        // Los movimientos se van acumulando junto con el lote del que salieron,
        // para poder armar la respuesta despues de que MySQL asigne los ids.
        var consumos = new List<(LineaPreparada Linea, Lote? Lote, decimal CantidadBase,
                                 MovimientoInventario Movimiento)>();

        foreach (var preparada in preparadas)
        {
            var repartos = preparada.Peticion.LoteId is int loteId
                ? [await TomarDeLoteIndicadoAsync(
                    loteId, peticion.SucursalId, preparada, cancellationToken)]
                : await RepartirPorFefoAsync(
                    peticion.SucursalId, preparada, cancellationToken);

            foreach (var (lote, cantidadBase) in repartos)
            {
                var movimiento = new MovimientoInventario
                {
                    SucursalId = peticion.SucursalId,
                    ProductoId = preparada.Producto.Id,
                    UsuarioId = usuarioId,
                    // El ENUM de la base es enum('Ingreso','Retiro'): no existe
                    // el valor 'Salida'. Una salida por venta es un Retiro con
                    // motivo Venta.
                    Tipo = TipoMovimiento.Retiro,
                    Motivo = MotivoMovimiento.Venta,
                    // La cantidad del movimiento es la de ESTE reparto, no la de
                    // la linea: si FEFO encadena dos lotes, cada movimiento
                    // lleva lo suyo y entre los dos suman la linea.
                    //
                    // Va convertida de vuelta a la unidad de venta para que
                    // `cantidad` y `unidad_id` sigan siendo coherentes entre si.
                    Cantidad = ConvertirDeBaseAUnidadVenta(cantidadBase, preparada),
                    UnidadId = preparada.Peticion.UnidadId,
                    CantidadBase = cantidadBase,
                    LoteId = lote?.Id,
                    Observaciones = ConstruirObservacion(peticion.Observaciones)
                };
                _inventario.AgregarMovimiento(movimiento);

                consumos.Add((preparada, lote, cantidadBase, movimiento));
            }
        }

        // El saldo se descuenta una vez por producto, con la suma de sus lineas.
        var saldosAfectados = new List<SaldoAfectadoDto>(requeridoPorProducto.Count);

        foreach (var (productoId, requerido) in requeridoPorProducto)
        {
            var saldo = saldos[productoId];
            var resultante = saldo.CantidadBase - requerido;
            _inventario.ActualizarCantidadBase(saldo, resultante);

            saldosAfectados.Add(new SaldoAfectadoDto(
                productoId,
                preparadas.First(p => p.Producto.Id == productoId).Producto.Nombre,
                requerido,
                resultante));
        }

        // Un solo guardado para la venta, el detalle, los saldos, los lotes y
        // los movimientos. Al volver, MySQL ya asigno todos los ids.
        await _ventas.GuardarCambiosAsync(cancellationToken);

        // --- 5. Auditoria, en la MISMA transaccion -------------------------------
        var lineasVendidas = ArmarLineas(preparadas, consumos);

        await _auditoria.RegistrarEventoAsync(
            Modulo,
            "RegistrarVenta",
            usuarioId,
            ConstruirDetalleAuditoria(venta, total, lineasVendidas, saldosAfectados),
            cancellationToken);

        await _ventas.GuardarCambiosAsync(cancellationToken);

        return new VentaRegistradaDto(venta.Id, total, lineasVendidas, saldosAfectados);
    }

    // =========================================================================
    // REPARTO DE LOTES
    // =========================================================================

    /// <summary>
    /// Descuenta del lote que indica la linea.
    ///
    /// Aqui no hay FEFO que aplicar: quien vende eligio el lote a proposito
    /// (porque lo tiene fisicamente en la mano, por ejemplo). Lo que si se
    /// valida es que el lote sea de esa sede y ese producto, y que alcance para
    /// la linea entera; un lote elegido a mano no se completa con otro.
    /// </summary>
    private async Task<(Lote? Lote, decimal CantidadBase)> TomarDeLoteIndicadoAsync(
        int loteId,
        int sucursalId,
        LineaPreparada preparada,
        CancellationToken cancellationToken)
    {
        var lote = await _inventario.ObtenerLoteParaActualizarAsync(loteId, cancellationToken)
            ?? throw new ReferenciaVentaNoEncontradaException($"No existe el lote {loteId}.");

        // Sin esta validacion se podria descontar del lote de otra sede o de
        // otro producto, y el saldo por lote dejaria de cuadrar con el
        // consolidado sin que nada avisara.
        if (lote.ProductoId != preparada.Producto.Id || lote.SucursalId != sucursalId)
        {
            throw new ReferenciaVentaNoEncontradaException(
                $"El lote '{lote.NumeroLote}' pertenece al producto {lote.ProductoId} " +
                $"en la sede {lote.SucursalId}, pero la venta es del producto " +
                $"{preparada.Producto.Id} en la sede {sucursalId}.");
        }

        var disponible = lote.CantidadBase ?? 0m;
        if (disponible < preparada.CantidadBase)
        {
            throw new StockLoteInsuficienteException(
                lote.Id, lote.NumeroLote, disponible, preparada.CantidadBase);
        }

        _inventario.ActualizarCantidadLote(lote, disponible - preparada.CantidadBase);
        return (lote, preparada.CantidadBase);
    }

    /// <summary>
    /// Reparte la linea entre los lotes por FEFO: primero el que vence antes.
    ///
    /// Encadena lotes si hace falta. Que una linea de 500 L salga de un lote de
    /// 300 y otro de 200 es lo normal en un almacen, y forzar a que un solo
    /// lote cubra la linea obligaria a partir la venta a mano.
    ///
    /// Los lotes se descuentan en el momento, no al final, para que dos lineas
    /// del mismo producto en la misma venta no se repartan el mismo lote dos
    /// veces.
    ///
    /// SOBRE EL SOBRANTE: si los lotes no cubren la cantidad, el resto sale sin
    /// lote asignado, en un movimiento con `lote_id` nulo. Ocurre cuando el
    /// saldo consolidado de la sede es mayor que la suma de sus lotes, que es
    /// posible porque son columnas independientes: un ingreso registrado sin
    /// numero de lote sube el saldo y no crea lote. La alternativa seria
    /// rechazar la venta, pero eso bloquearia vender stock que existe de verdad
    /// y ya paso la validacion del saldo. El sobrante queda visible en la
    /// respuesta y en la auditoria.
    /// </summary>
    private async Task<List<(Lote? Lote, decimal CantidadBase)>> RepartirPorFefoAsync(
        int sucursalId,
        LineaPreparada preparada,
        CancellationToken cancellationToken)
    {
        var repartos = new List<(Lote? Lote, decimal CantidadBase)>();
        var porCubrir = preparada.CantidadBase;

        // Ya vienen ordenados por vencimiento (los sin fecha al final) y solo
        // los que tienen cantidad mayor que cero.
        var candidatos = await _inventario.ObtenerLotesPorVencimientoAsync(
            sucursalId, preparada.Producto.Id, cancellationToken);

        foreach (var candidato in candidatos)
        {
            if (porCubrir <= 0m)
            {
                break;
            }

            // La consulta de arriba viene sin rastreo. Hay que volver a pedir la
            // fila con bloqueo para poder modificarla, y eso ademas la releera
            // ya descontada si otra linea de esta misma venta la toco.
            var lote = await _inventario.ObtenerLoteParaActualizarAsync(
                candidato.Id, cancellationToken);

            var disponible = lote?.CantidadBase ?? 0m;
            if (lote is null || disponible <= 0m)
            {
                continue;
            }

            var aTomar = Math.Min(disponible, porCubrir);
            _inventario.ActualizarCantidadLote(lote, disponible - aTomar);

            repartos.Add((lote, aTomar));
            porCubrir -= aTomar;
        }

        if (porCubrir > 0m)
        {
            // El sobrante sin lote. Ver la nota del resumen del metodo.
            repartos.Add((null, porCubrir));
        }

        return repartos;
    }

    // =========================================================================
    // CONVERSION DE UNIDADES
    // =========================================================================

    /// <summary>
    /// Trae el producto con su unidad base cargada, que es lo que hace falta
    /// para convertir. Lanza si no existe.
    /// </summary>
    private async Task<Producto> ObtenerProductoAsync(
        int productoId,
        CancellationToken cancellationToken) =>
        await _productos.ObtenerPorIdAsync(productoId, cancellationToken)
            ?? throw new ReferenciaVentaNoEncontradaException(
                $"No existe el producto {productoId}.");

    /// <summary>
    /// Pasa la cantidad de la unidad de venta a la unidad base del producto,
    /// delegando la formula en <see cref="ConversorUnidades"/>, que es la misma
    /// que usan Inventario y Compras.
    /// </summary>
    private async Task<decimal> ConvertirAUnidadBaseAsync(
        decimal cantidad,
        int unidadId,
        Producto producto,
        CancellationToken cancellationToken)
    {
        if (producto.UnidadBaseId is null)
        {
            throw new ConversionVentaImposibleException(
                $"El producto '{producto.Nombre}' no tiene unidad base definida, " +
                "asi que no hay unidad a la cual convertir la cantidad vendida.");
        }

        var unidadBaseId = producto.UnidadBaseId.Value;
        decimal? factorUnidad = null;
        decimal? factorUnidadBase = null;

        if (unidadId != unidadBaseId)
        {
            var origen = await _unidades.ObtenerFactorConversionLitrosAsync(
                unidadId, cancellationToken);
            if (!origen.UnidadExiste)
            {
                throw new ReferenciaVentaNoEncontradaException(
                    $"No existe la unidad de medida {unidadId}.");
            }

            var destino = await _unidades.ObtenerFactorConversionLitrosAsync(
                unidadBaseId, cancellationToken);
            if (!destino.UnidadExiste)
            {
                throw new ReferenciaVentaNoEncontradaException(
                    $"No existe la unidad base {unidadBaseId} que declara el producto.");
            }

            factorUnidad = origen.FactorLitros;
            factorUnidadBase = destino.FactorLitros;
        }

        var conversion = ConversorUnidades.ABaseDelProducto(
            cantidad, unidadId, factorUnidad, unidadBaseId, factorUnidadBase);

        return conversion.Estado switch
        {
            EstadoConversion.Ok => conversion.CantidadBase,

            EstadoConversion.SinFactor => throw new ConversionVentaImposibleException(
                $"No hay conversion entre la unidad {unidadId} y la unidad base " +
                $"{unidadBaseId}: alguna de las dos no tiene factor a litros."),

            _ => throw new VentaInvalidaException(
                $"Convertida a unidad base, la cantidad {Num(cantidad)} se redondea a cero " +
                $"con {ConversorUnidades.DecimalesCantidadBase} decimales.")
        };
    }

    /// <summary>
    /// El precio que se va a cobrar por unidad de venta de esta linea.
    ///
    /// DOS FUENTES, EN ESTE ORDEN:
    ///
    ///   1. el precio que trae la linea, si trae alguno. Quien vende puede
    ///      apartarse de la lista -una rebaja, un precio pactado- y eso manda;
    ///   2. el precio de lista del producto, CONVERTIDO de unidad base a la
    ///      unidad de la linea.
    ///
    /// Y SI NO HAY NINGUNA DE LAS DOS, SE RECHAZA. Antes no: la columna se
    /// quedaba en NULL y el total sumaba cero, asi que la venta entraba regalada
    /// con el stock descontado y sin ninguna senal. Un cero en una venta solo
    /// puede ser un descuido, y es mas barato rechazarlo que descubrirlo al
    /// cuadrar la caja.
    ///
    /// LA CONVERSION ES LA INVERSA DE LA DE CANTIDAD. Una cantidad en galones es
    /// un numero MENOR que en litros -3,78541 L caben en un galon-, pero el
    /// precio POR galon es MAYOR en la misma proporcion. De ahi la division por
    /// el factor de la unidad base y la multiplicacion por el de la linea, y no
    /// al reves.
    /// </summary>
    private async Task<decimal> ResolverPrecioAsync(
        CrearLineaVentaDto linea,
        Producto producto,
        int numero,
        CancellationToken cancellationToken)
    {
        if (linea.PrecioUnitario is decimal propio)
        {
            return propio;
        }

        if (producto.PrecioVenta is not decimal precioBase)
        {
            throw new VentaInvalidaException(
                $"Linea {numero}: '{producto.Nombre}' no tiene precio de venta fijado, y la " +
                "linea tampoco trae uno. Escribe el precio en la linea, o fija el del producto " +
                "en Ventas -> Lista de precios.");
        }

        var unidadBaseId = producto.UnidadBaseId
            ?? throw new ConversionVentaImposibleException(
                $"El producto '{producto.Nombre}' no tiene unidad base definida, asi que su " +
                "precio de lista no se puede pasar a la unidad de la linea.");

        // Misma unidad: el precio de lista ya esta en ella y convertir seria
        // multiplicar y dividir por el mismo factor.
        if (linea.UnidadId == unidadBaseId)
        {
            return precioBase;
        }

        var origen = await _unidades.ObtenerFactorConversionLitrosAsync(
            unidadBaseId, cancellationToken);
        var destino = await _unidades.ObtenerFactorConversionLitrosAsync(
            linea.UnidadId, cancellationToken);

        if (!destino.UnidadExiste)
        {
            throw new ReferenciaVentaNoEncontradaException(
                $"No existe la unidad de medida {linea.UnidadId}.");
        }

        if (origen.FactorLitros is not decimal factorBase || factorBase <= 0m ||
            destino.FactorLitros is not decimal factorLinea)
        {
            throw new ConversionVentaImposibleException(
                $"Linea {numero}: no hay conversion entre la unidad base {unidadBaseId} y la " +
                $"unidad {linea.UnidadId}, asi que el precio de lista de '{producto.Nombre}' " +
                "no se puede aplicar. Escribe el precio en la linea.");
        }

        // A 2 decimales, que es lo que guarda `venta_detalle.precio_unitario`.
        // AwayFromZero y no el redondeo bancario por omision de .NET: en dinero
        // se espera que 0,5 suba.
        var convertido = Math.Round(
            precioBase / factorBase * factorLinea, 2, MidpointRounding.AwayFromZero);

        if (convertido <= 0m)
        {
            throw new VentaInvalidaException(
                $"Linea {numero}: el precio de lista de '{producto.Nombre}' convertido a la " +
                "unidad de la linea se redondea a cero. Escribe el precio en la linea.");
        }

        return convertido;
    }

    /// <summary>
    /// Pasa una cantidad en unidad base de vuelta a la unidad de venta, para que
    /// el movimiento guarde `cantidad` y `unidad_id` coherentes entre si.
    ///
    /// Se deriva de la proporcion de la linea en vez de volver a consultar los
    /// factores: la linea entera ya se convirtio, asi que la regla de tres da el
    /// mismo resultado sin otra ida a la base.
    /// </summary>
    private static decimal ConvertirDeBaseAUnidadVenta(
        decimal cantidadBase,
        LineaPreparada preparada)
    {
        if (preparada.CantidadBase <= 0m)
        {
            return 0m;
        }

        var proporcion = cantidadBase / preparada.CantidadBase;
        var cantidad = MapeosVentas.RedondearCantidad(preparada.Cantidad * proporcion);

        // El CHECK `chk_movinv_cantidad` exige > 0. Un reparto tan pequeno que
        // se redondea a cero deja la cantidad en el minimo representable en vez
        // de romper el movimiento; la cifra fiable es `cantidad_base`, que no
        // pasa por este redondeo.
        return cantidad > 0m ? cantidad : 0.01m;
    }

    // =========================================================================
    // AUXILIARES
    // =========================================================================

    private static List<LineaVendidaDto> ArmarLineas(
        List<LineaPreparada> preparadas,
        List<(LineaPreparada Linea, Lote? Lote, decimal CantidadBase,
              MovimientoInventario Movimiento)> consumos) =>
        preparadas.Select(preparada => new LineaVendidaDto(
            preparada.Detalle.Id,
            preparada.Producto.Id,
            preparada.Producto.Nombre,
            preparada.Cantidad,
            preparada.Peticion.UnidadId,
            preparada.CantidadBase,
            consumos
                .Where(c => ReferenceEquals(c.Linea, preparada))
                .Select(c => new ConsumoLoteDto(
                    c.Lote?.Id,
                    c.Lote?.NumeroLote,
                    c.Lote?.FechaVencimiento,
                    c.CantidadBase,
                    c.Movimiento.Id))
                .ToList()))
        .ToList();

    private static string ConstruirObservacion(string? observaciones) =>
        string.IsNullOrWhiteSpace(observaciones) ? "Venta" : $"Venta | {observaciones}";

    private static string ConstruirDetalleAuditoria(
        Venta venta,
        decimal total,
        IReadOnlyList<LineaVendidaDto> lineas,
        IReadOnlyList<SaldoAfectadoDto> saldos)
    {
        var detalle =
            $"venta={venta.Id} | cliente={venta.ClienteId} | " +
            $"sucursal={venta.SucursalId} | total={Num(total)} | " +
            $"lineas={lineas.Count}";

        foreach (var saldo in saldos)
        {
            detalle +=
                $" || producto={saldo.ProductoId} " +
                $"cantidadBase=-{Num(saldo.CantidadBaseDescontada)} " +
                $"saldo={Num(saldo.SaldoResultante)}";
        }

        foreach (var linea in lineas)
        {
            foreach (var consumo in linea.Consumos)
            {
                detalle += consumo.LoteId is int loteId
                    ? $" || lote={loteId}('{consumo.NumeroLote}') " +
                      $"-{Num(consumo.CantidadBase)} mov={consumo.MovimientoId}"
                    // Queda explicito en la bitacora: es la senal de que los
                    // lotes no cubren el saldo de la sede.
                    : $" || lote=SIN_ASIGNAR -{Num(consumo.CantidadBase)} " +
                      $"mov={consumo.MovimientoId}";
            }
        }

        return detalle;
    }

    /// <summary>
    /// Formatea con punto decimal siempre, para que la bitacora no cambie de
    /// formato segun la configuracion regional del servidor.
    /// </summary>
    private static string Num(decimal valor) =>
        valor.ToString("0.####", CultureInfo.InvariantCulture);
}
