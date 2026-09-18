using System.Globalization;
using Colorsin.Application.Comun.Auditoria;
using Colorsin.Application.Comun.Services;
using Colorsin.Application.Inventario;
using Colorsin.Application.Inventario.Repositories;
using Colorsin.Application.Transferencias.DTOs;
using Colorsin.Application.Transferencias.Mapping;
using Colorsin.Application.Transferencias.Repositories;
using Colorsin.Domain.Inventario;
using Colorsin.Domain.Transferencias;

namespace Colorsin.Application.Transferencias.Services;

/// <inheritdoc cref="ITransferenciasService"/>
public sealed class TransferenciasService : ITransferenciasService
{
    /// <summary>Nombre del modulo en los eventos de auditoria.</summary>
    private const string Modulo = "Transferencias";

    private readonly ITransferenciaRepository _transferencias;
    private readonly ITransportadoraRepository _transportadoras;
    private readonly IInventarioRepository _inventario;
    private readonly IProductoRepository _productos;
    private readonly IUnidadMedidaService _unidades;
    private readonly IAuditoriaService _auditoria;

    /// <summary>
    /// Dos dependencias mas de las pedidas, por lo mismo que en Ventas:
    ///
    /// - <see cref="IProductoRepository"/>: hace falta la unidad base del
    ///   producto para convertir la cantidad, y su nombre para los mensajes.
    /// - <see cref="IUnidadMedidaService"/>: <c>ConversorUnidades</c> es una
    ///   clase estatica de aritmetica pura, asi que no se inyecta ni consulta
    ///   nada; los factores hay que buscarlos, y este servicio es el que ya lo
    ///   hace en los otros modulos.
    /// </summary>
    public TransferenciasService(
        ITransferenciaRepository transferencias,
        ITransportadoraRepository transportadoras,
        IInventarioRepository inventario,
        IProductoRepository productos,
        IUnidadMedidaService unidades,
        IAuditoriaService auditoria)
    {
        _transferencias = transferencias;
        _transportadoras = transportadoras;
        _inventario = inventario;
        _productos = productos;
        _unidades = unidades;
        _auditoria = auditoria;
    }

    // =========================================================================
    // CONSULTAS
    // =========================================================================

    public async Task<IReadOnlyList<TransportadoraDto>> ObtenerTransportadorasAsync(
        CancellationToken cancellationToken = default)
    {
        var transportadoras = await _transportadoras.ObtenerTodasAsync(cancellationToken);
        return transportadoras.Select(t => t.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<TransferenciaDto>> ObtenerTransferenciasAsync(
        int? sucursalOrigenId = null,
        int? sucursalDestinoId = null,
        EstadoTransferencia? estado = null,
        int limite = 100,
        CancellationToken cancellationToken = default)
    {
        var transferencias = await _transferencias.ObtenerAsync(
            sucursalOrigenId, sucursalDestinoId, estado, limite, cancellationToken);

        return transferencias.Select(t => t.ToDto()).ToList();
    }

    public async Task<TransferenciaDto?> ObtenerTransferenciaPorIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var transferencia = await _transferencias.ObtenerPorIdAsync(id, cancellationToken);
        if (transferencia is null)
        {
            return null;
        }

        var movimientos = await _transferencias.ObtenerMovimientosAsync(id, cancellationToken);

        return transferencia.ToDto(
            movimientos.Select(m => m.ToDetalleDto()).ToList(),
            transferencia.Novedades.Select(n => n.ToDto()).ToList());
    }

    // =========================================================================
    // CREAR
    // =========================================================================

    public async Task<ResultadoTransferencia> CrearAsync(
        CrearTransferenciaDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (usuarioId <= 0)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.UsuarioNoIndicado,
                $"Hay que indicar el usuario que solicita el traslado; llego {usuarioId}.");
        }

        if (peticion.Cantidad <= 0m)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.CantidadInvalida,
                $"La cantidad debe ser mayor que cero; llego {Num(peticion.Cantidad)}.");
        }

        // Se valida aqui ademas del CHECK `chk_transf_sedes_distintas` para que
        // el rechazo llegue con motivo, y no como un error 500 de MySQL.
        if (peticion.SucursalOrigenId == peticion.SucursalDestinoId)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.SedesIguales,
                $"Origen y destino son la misma sede ({peticion.SucursalOrigenId}). " +
                "Una sede no se traslada mercancia a si misma.");
        }

        var producto = await _productos.ObtenerPorIdAsync(peticion.ProductoId, cancellationToken);
        if (producto is null)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.ProductoNoEncontrado,
                $"No existe el producto {peticion.ProductoId}.");
        }

        // La conversion se valida ya, aunque el stock no se toque hasta el
        // despacho: si la unidad pedida no es convertible a la unidad base del
        // producto, el traslado nace condenado a fallar y es mejor saberlo aqui.
        var conversion = await ConvertirAsync(
            peticion.Cantidad, peticion.UnidadId, producto, cancellationToken);

        if (conversion.Error is ErrorTransferencia errorConversion)
        {
            return ResultadoTransferencia.Fallo(errorConversion, conversion.Mensaje!);
        }

        var transferencia = new Transferencia
        {
            ProductoId = peticion.ProductoId,
            SucursalOrigenId = peticion.SucursalOrigenId,
            SucursalDestinoId = peticion.SucursalDestinoId,
            UsuarioId = usuarioId,
            // Transportadora y guia van nulas: se asignan al despachar.
            CantidadSolicitada = peticion.Cantidad,
            UnidadId = peticion.UnidadId,
            Estado = EstadoTransferencia.Solicitada,
            Urgencia = peticion.Urgencia
            // FechaSolicitud la pone la base con CURRENT_TIMESTAMP.
        };

        return await _transferencias.EjecutarEnTransaccionAsync(async ct =>
        {
            _transferencias.AgregarTransferencia(transferencia);
            await _transferencias.GuardarCambiosAsync(ct);

            await _auditoria.RegistrarEventoAsync(
                Modulo, "SolicitarTransferencia", usuarioId,
                $"transferencia={transferencia.Id} | producto={peticion.ProductoId} " +
                $"('{producto.Nombre}') | origen={peticion.SucursalOrigenId} | " +
                $"destino={peticion.SucursalDestinoId} | " +
                $"cantidad={Num(peticion.Cantidad)} unidad={peticion.UnidadId} | " +
                $"cantidadBase={Num(conversion.CantidadBase)} | " +
                $"urgencia={peticion.Urgencia} | estado=Solicitada",
                ct);

            await _transferencias.GuardarCambiosAsync(ct);

            return ResultadoTransferencia.Ok(
                transferencia.Id, EstadoTransferencia.Solicitada,
                "Traslado solicitado. El stock no se mueve ni se reserva hasta el despacho.",
                conversion.CantidadBase);
        }, cancellationToken);
    }

    // =========================================================================
    // DESPACHO: la mercancia sale de la sede origen
    // =========================================================================

    public async Task<ResultadoTransferencia> DespacharAsync(
        DespacharTransferenciaDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (usuarioId <= 0)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.UsuarioNoIndicado,
                $"Hay que indicar el usuario que despacha; llego {usuarioId}.");
        }

        if (string.IsNullOrWhiteSpace(peticion.Guia))
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.GuiaNoIndicada,
                "Hay que indicar el numero de guia: es con lo que se reclama si la " +
                "carga llega mal o no llega.");
        }

        if (!await _transportadoras.ExisteAsync(peticion.TransportadoraId, cancellationToken))
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.TransportadoraNoEncontrada,
                $"No existe la transportadora {peticion.TransportadoraId}.");
        }

        return await _transferencias.EjecutarEnTransaccionAsync(
            ct => DespacharEnTransaccionAsync(peticion, usuarioId, ct),
            cancellationToken);
    }

    private async Task<ResultadoTransferencia> DespacharEnTransaccionAsync(
        DespacharTransferenciaDto peticion,
        int usuarioId,
        CancellationToken cancellationToken)
    {
        // Fila bloqueada: dos despachos simultaneos del mismo traslado no pueden
        // ver los dos 'Solicitada' y sacar el stock dos veces.
        var transferencia = await _transferencias.ObtenerParaOperarAsync(
            peticion.TransferenciaId, cancellationToken);

        if (transferencia is null)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.TransferenciaNoEncontrada,
                $"No existe el traslado {peticion.TransferenciaId}.");
        }

        if (transferencia.Estado != EstadoTransferencia.Solicitada)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.EstadoNoPermiteOperacion,
                $"El traslado {transferencia.Id} esta en estado " +
                $"'{transferencia.Estado?.ToString() ?? "sin estado"}' y solo se puede " +
                "despachar uno Solicitado.");
        }

        var producto = await _productos.ObtenerPorIdAsync(transferencia.ProductoId, cancellationToken);
        if (producto is null)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.ProductoNoEncontrado,
                $"No existe el producto {transferencia.ProductoId}.");
        }

        var cantidad = transferencia.CantidadSolicitada ?? 0m;
        if (cantidad <= 0m)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.CantidadInvalida,
                $"El traslado {transferencia.Id} no tiene cantidad que despachar.");
        }

        var conversion = await ConvertirAsync(
            cantidad, transferencia.UnidadId, producto, cancellationToken);

        if (conversion.Error is ErrorTransferencia errorConversion)
        {
            return ResultadoTransferencia.Fallo(errorConversion, conversion.Mensaje!);
        }

        var cantidadBase = conversion.CantidadBase;

        // --- Validacion de disponibilidad en el origen ---------------------------
        var saldoOrigen = await _inventario.ObtenerSaldoParaActualizarAsync(
            transferencia.SucursalOrigenId, transferencia.ProductoId, cancellationToken);

        if (saldoOrigen is null)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.SaldoNoEncontrado,
                $"La sede {transferencia.SucursalOrigenId} no tiene saldo registrado del " +
                $"producto '{producto.Nombre}'; no se puede despachar lo que nunca entro.");
        }

        if (saldoOrigen.CantidadBase < cantidadBase)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.StockInsuficiente,
                $"Stock insuficiente de '{producto.Nombre}' en la sede " +
                $"{transferencia.SucursalOrigenId}: hay {Num(saldoOrigen.CantidadBase)} y el " +
                $"traslado pide {Num(cantidadBase)}.");
        }

        // --- Aplicar. Desde aqui ya no hay rechazos ------------------------------
        var repartos = await RepartirPorFefoAsync(
            transferencia.SucursalOrigenId, transferencia.ProductoId,
            cantidadBase, cancellationToken);

        var movimientos = new List<MovimientoInventario>(repartos.Count);

        foreach (var (lote, cantidadLote) in repartos)
        {
            var movimiento = new MovimientoInventario
            {
                SucursalId = transferencia.SucursalOrigenId,
                ProductoId = transferencia.ProductoId,
                UsuarioId = usuarioId,
                // El ENUM de la base es enum('Ingreso','Retiro'): no existe el
                // valor 'Salida'. Una salida por traslado es un Retiro con
                // motivo Transferencia.
                Tipo = TipoMovimiento.Retiro,
                Motivo = MotivoMovimiento.Transferencia,
                Cantidad = ProporcionEnUnidadTraslado(cantidadLote, cantidadBase, cantidad),
                UnidadId = transferencia.UnidadId,
                CantidadBase = cantidadLote,
                LoteId = lote?.Id,
                TransferenciaId = transferencia.Id,
                Observaciones = ConstruirObservacion(
                    $"Despacho traslado {transferencia.Id}", peticion.Observaciones)
            };
            _inventario.AgregarMovimiento(movimiento);
            movimientos.Add(movimiento);
        }

        var saldoResultante = saldoOrigen.CantidadBase - cantidadBase;
        _inventario.ActualizarCantidadBase(saldoOrigen, saldoResultante);

        transferencia.TransportadoraId = peticion.TransportadoraId;
        transferencia.Guia = peticion.Guia.Trim();
        transferencia.FechaEstimadaLlegada = peticion.FechaEstimadaLlegada;
        transferencia.Estado = EstadoTransferencia.EnTransito;

        await _transferencias.GuardarCambiosAsync(cancellationToken);

        await _auditoria.RegistrarEventoAsync(
            Modulo, "DespacharTransferencia", usuarioId,
            $"transferencia={transferencia.Id} | estado=Solicitada->EnTransito | " +
            $"origen={transferencia.SucursalOrigenId} | producto={transferencia.ProductoId} | " +
            $"cantidadBase=-{Num(cantidadBase)} | saldo={Num(saldoResultante)} | " +
            $"transportadora={peticion.TransportadoraId} | guia={transferencia.Guia}" +
            DetalleLotes(movimientos),
            cancellationToken);

        await _transferencias.GuardarCambiosAsync(cancellationToken);

        return ResultadoTransferencia.Ok(
            transferencia.Id, EstadoTransferencia.EnTransito,
            $"Traslado {transferencia.Id} despachado con guia {transferencia.Guia}.",
            cantidadBase, saldoResultante,
            movimientos.Select(m => m.ToDetalleDto()).ToList());
    }

    // =========================================================================
    // RECEPCION: la mercancia entra a la sede destino
    // =========================================================================

    public async Task<ResultadoTransferencia> RecibirAsync(
        RecibirTransferenciaDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (usuarioId <= 0)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.UsuarioNoIndicado,
                $"Hay que indicar el usuario que recibe; llego {usuarioId}.");
        }

        if (peticion.CantidadRecibida is <= 0m)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.CantidadInvalida,
                $"La cantidad recibida debe ser mayor que cero; llego " +
                $"{Num(peticion.CantidadRecibida!.Value)}. Si no llego nada, registra una " +
                "novedad de tipo Faltante en vez de recibir cero.");
        }

        return await _transferencias.EjecutarEnTransaccionAsync(
            ct => RecibirEnTransaccionAsync(peticion, usuarioId, ct),
            cancellationToken);
    }

    private async Task<ResultadoTransferencia> RecibirEnTransaccionAsync(
        RecibirTransferenciaDto peticion,
        int usuarioId,
        CancellationToken cancellationToken)
    {
        var transferencia = await _transferencias.ObtenerParaOperarAsync(
            peticion.TransferenciaId, cancellationToken);

        if (transferencia is null)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.TransferenciaNoEncontrada,
                $"No existe el traslado {peticion.TransferenciaId}.");
        }

        if (transferencia.Estado != EstadoTransferencia.EnTransito)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.EstadoNoPermiteOperacion,
                $"El traslado {transferencia.Id} esta en estado " +
                $"'{transferencia.Estado?.ToString() ?? "sin estado"}' y solo se puede " +
                "recibir uno EnTransito.");
        }

        var producto = await _productos.ObtenerPorIdAsync(transferencia.ProductoId, cancellationToken);
        if (producto is null)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.ProductoNoEncontrado,
                $"No existe el producto {transferencia.ProductoId}.");
        }

        // Los movimientos del despacho dicen EXACTAMENTE de que lotes salio la
        // mercancia y cuanto de cada uno, en orden FEFO. Es lo que permite
        // recrearlos en el destino sin perder el numero ni el vencimiento.
        var despacho = await _transferencias.ObtenerMovimientosDespachoAsync(
            transferencia.Id, cancellationToken);

        var despachadoBase = despacho.Sum(m => m.CantidadBase ?? 0m);
        if (despachadoBase <= 0m)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.CantidadInvalida,
                $"El traslado {transferencia.Id} no tiene movimientos de despacho " +
                "registrados; no hay nada que recibir.");
        }

        var solicitada = transferencia.CantidadSolicitada ?? 0m;
        var recibida = peticion.CantidadRecibida ?? solicitada;

        if (recibida > solicitada)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.RecibidaExcedeDespachada,
                $"El traslado despacho {Num(solicitada)} y se intentan recibir " +
                $"{Num(recibida)}. Recibir de mas crearia stock de la nada; si llego " +
                "producto extra, registra una novedad de tipo Sobrante.");
        }

        // Se convierte lo RECIBIDO, no lo despachado: en una recepcion parcial
        // son distintos, y lo que entra al destino es lo primero.
        var conversion = await ConvertirAsync(
            recibida, transferencia.UnidadId, producto, cancellationToken);

        if (conversion.Error is ErrorTransferencia errorConversion)
        {
            return ResultadoTransferencia.Fallo(errorConversion, conversion.Mensaje!);
        }

        var recibidaBase = conversion.CantidadBase;

        // --- Aplicar. Desde aqui ya no hay rechazos ------------------------------
        // Lo recibido se reparte entre los mismos lotes que salieron, en el
        // orden en que salieron. Si llego menos, se completa primero el lote que
        // vencia antes: es lo coherente con haberlo despachado primero.
        var porCubrir = recibidaBase;
        var movimientos = new List<MovimientoInventario>();

        foreach (var salida in despacho)
        {
            if (porCubrir <= 0m)
            {
                break;
            }

            var deEsteLote = Math.Min(salida.CantidadBase ?? 0m, porCubrir);
            if (deEsteLote <= 0m)
            {
                continue;
            }

            int? loteDestinoId = salida.Lote is null
                ? null
                : await RecrearLoteEnDestinoAsync(
                    transferencia.SucursalDestinoId, transferencia.ProductoId,
                    salida.Lote, deEsteLote, cancellationToken);

            var movimiento = new MovimientoInventario
            {
                SucursalId = transferencia.SucursalDestinoId,
                ProductoId = transferencia.ProductoId,
                UsuarioId = usuarioId,
                Tipo = TipoMovimiento.Ingreso,
                Motivo = MotivoMovimiento.Transferencia,
                Cantidad = ProporcionEnUnidadTraslado(deEsteLote, recibidaBase, recibida),
                UnidadId = transferencia.UnidadId,
                CantidadBase = deEsteLote,
                LoteId = loteDestinoId,
                TransferenciaId = transferencia.Id,
                Observaciones = ConstruirObservacion(
                    $"Recepcion traslado {transferencia.Id}", peticion.Observaciones)
            };
            _inventario.AgregarMovimiento(movimiento);
            movimientos.Add(movimiento);

            porCubrir -= deEsteLote;
        }

        var saldoResultante = await IngresarAlSaldoDestinoAsync(
            transferencia.SucursalDestinoId, transferencia.ProductoId,
            recibidaBase, cancellationToken);

        // Completada solo si llego todo. Si falta, RecibidaParcial: la
        // diferencia salio del origen y nunca entro al destino, asi que es una
        // baja neta de la red y el libro mayor la refleja tal cual.
        var completa = recibida >= solicitada;
        var estadoNuevo = completa
            ? EstadoTransferencia.Completada
            : EstadoTransferencia.RecibidaParcial;

        transferencia.CantidadRecibida = recibida;
        transferencia.Estado = estadoNuevo;

        await _transferencias.GuardarCambiosAsync(cancellationToken);

        var faltante = solicitada - recibida;

        await _auditoria.RegistrarEventoAsync(
            Modulo, "RecibirTransferencia", usuarioId,
            $"transferencia={transferencia.Id} | estado=EnTransito->{estadoNuevo} | " +
            $"destino={transferencia.SucursalDestinoId} | producto={transferencia.ProductoId} | " +
            $"solicitada={Num(solicitada)} recibida={Num(recibida)} " +
            $"faltante={Num(faltante)} | cantidadBase=+{Num(recibidaBase)} | " +
            $"saldo={Num(saldoResultante)}" +
            DetalleLotes(movimientos),
            cancellationToken);

        await _transferencias.GuardarCambiosAsync(cancellationToken);

        var mensaje = completa
            ? $"Traslado {transferencia.Id} recibido completo en la sede " +
              $"{transferencia.SucursalDestinoId}."
            : $"Traslado {transferencia.Id} recibido parcial: llegaron {Num(recibida)} de " +
              $"{Num(solicitada)}. Faltan {Num(faltante)}, que se dan por perdidos en " +
              "transito; conviene registrar una novedad de tipo Faltante.";

        return ResultadoTransferencia.Ok(
            transferencia.Id, estadoNuevo, mensaje,
            recibidaBase, saldoResultante,
            movimientos.Select(m => m.ToDetalleDto()).ToList());
    }

    // =========================================================================
    // RECHAZO Y CANCELACION
    // =========================================================================

    public Task<ResultadoTransferencia> RechazarAsync(
        int transferenciaId, int usuarioId, string? motivo = null,
        CancellationToken cancellationToken = default) =>
        CerrarSinDespacharAsync(
            transferenciaId, usuarioId, motivo,
            EstadoTransferencia.Rechazada, "RechazarTransferencia", cancellationToken);

    public Task<ResultadoTransferencia> CancelarAsync(
        int transferenciaId, int usuarioId, string? motivo = null,
        CancellationToken cancellationToken = default) =>
        CerrarSinDespacharAsync(
            transferenciaId, usuarioId, motivo,
            EstadoTransferencia.Cancelada, "CancelarTransferencia", cancellationToken);

    /// <summary>
    /// Rechazo y cancelacion hacen lo mismo y solo cambian en la etiqueta: se
    /// comparten para que no puedan divergir. Ninguno toca stock, porque solo
    /// aplican antes del despacho.
    /// </summary>
    private async Task<ResultadoTransferencia> CerrarSinDespacharAsync(
        int transferenciaId,
        int usuarioId,
        string? motivo,
        EstadoTransferencia estadoNuevo,
        string accionAuditoria,
        CancellationToken cancellationToken)
    {
        if (usuarioId <= 0)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.UsuarioNoIndicado,
                $"Hay que indicar el usuario responsable; llego {usuarioId}.");
        }

        return await _transferencias.EjecutarEnTransaccionAsync(async ct =>
        {
            var transferencia = await _transferencias.ObtenerParaOperarAsync(transferenciaId, ct);

            if (transferencia is null)
            {
                return ResultadoTransferencia.Fallo(
                    ErrorTransferencia.TransferenciaNoEncontrada,
                    $"No existe el traslado {transferenciaId}.");
            }

            if (transferencia.Estado != EstadoTransferencia.Solicitada)
            {
                return ResultadoTransferencia.Fallo(
                    ErrorTransferencia.EstadoNoPermiteOperacion,
                    $"El traslado {transferencia.Id} esta en estado " +
                    $"'{transferencia.Estado?.ToString() ?? "sin estado"}'. Solo se puede " +
                    $"pasar a {estadoNuevo} uno Solicitado: despues del despacho la " +
                    "mercancia ya salio del origen y anularlo dejaria stock sin dueno.");
            }

            transferencia.Estado = estadoNuevo;
            await _transferencias.GuardarCambiosAsync(ct);

            await _auditoria.RegistrarEventoAsync(
                Modulo, accionAuditoria, usuarioId,
                $"transferencia={transferencia.Id} | estado=Solicitada->{estadoNuevo} | " +
                $"origen={transferencia.SucursalOrigenId} | " +
                $"destino={transferencia.SucursalDestinoId} | " +
                $"motivo={motivo ?? "(sin indicar)"} | sinMovimientoDeStock",
                ct);

            await _transferencias.GuardarCambiosAsync(ct);

            return ResultadoTransferencia.Ok(
                transferencia.Id, estadoNuevo,
                $"Traslado {transferencia.Id} en estado {estadoNuevo}. No se movio stock.");
        }, cancellationToken);
    }

    // =========================================================================
    // NOVEDADES
    // =========================================================================

    public async Task<ResultadoNovedad> RegistrarNovedadAsync(
        RegistrarNovedadDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (usuarioId <= 0)
        {
            return ResultadoNovedad.Fallo(
                ErrorTransferencia.UsuarioNoIndicado,
                $"Hay que indicar el usuario que reporta; llego {usuarioId}.");
        }

        // El CHECK `chk_novtransf_cantidad` solo exige que no sea negativa.
        if (peticion.CantidadAfectada is < 0m)
        {
            return ResultadoNovedad.Fallo(
                ErrorTransferencia.CantidadInvalida,
                $"La cantidad afectada no puede ser negativa; llego " +
                $"{Num(peticion.CantidadAfectada!.Value)}.");
        }

        return await _transferencias.EjecutarEnTransaccionAsync(async ct =>
        {
            var transferencia = await _transferencias.ObtenerParaOperarAsync(
                peticion.TransferenciaId, ct);

            if (transferencia is null)
            {
                return ResultadoNovedad.Fallo(
                    ErrorTransferencia.TransferenciaNoEncontrada,
                    $"No existe el traslado {peticion.TransferenciaId}.");
            }

            // NO se valida el estado a proposito: los danos se descubren al
            // abrir las cajas, que suele ser despues de cerrar el traslado.
            // Una novedad documenta, no mueve stock ni cambia el estado, asi
            // que registrarla tarde no rompe nada.
            var novedad = new NovedadTransferencia
            {
                TransferenciaId = peticion.TransferenciaId,
                UsuarioId = usuarioId,
                Tipo = peticion.Tipo,
                CantidadAfectada = peticion.CantidadAfectada,
                Observaciones = peticion.Observaciones
                // Fecha la pone la base con CURRENT_TIMESTAMP.
            };
            _transferencias.AgregarNovedad(novedad);

            await _transferencias.GuardarCambiosAsync(ct);

            await _auditoria.RegistrarEventoAsync(
                Modulo, "RegistrarNovedadTransferencia", usuarioId,
                $"novedad={novedad.Id} | transferencia={peticion.TransferenciaId} | " +
                $"tipo={peticion.Tipo} | " +
                $"cantidadAfectada={(peticion.CantidadAfectada is null ? "(sin indicar)" : Num(peticion.CantidadAfectada.Value))} | " +
                $"estadoTraslado={transferencia.Estado} (sin cambios) | " +
                $"obs={peticion.Observaciones ?? "(sin observaciones)"}",
                ct);

            await _transferencias.GuardarCambiosAsync(ct);

            return ResultadoNovedad.Ok(novedad.Id, peticion.Tipo);
        }, cancellationToken);
    }

    // =========================================================================
    // AUXILIARES DE STOCK
    // =========================================================================

    /// <summary>
    /// Reparte una salida entre los lotes del origen por FEFO: primero el que
    /// vence antes, encadenando si uno no alcanza.
    ///
    /// Los lotes se descuentan en el momento para que el recorrido vea siempre
    /// saldos actualizados.
    ///
    /// Si los lotes no cubren la cantidad, el resto sale sin lote asignado. Pasa
    /// cuando el saldo consolidado de la sede es mayor que la suma de sus lotes,
    /// que es posible porque son columnas independientes. La validacion de
    /// disponibilidad ya se hizo contra el saldo, que es el dato autoritativo.
    /// </summary>
    private async Task<List<(Lote? Lote, decimal CantidadBase)>> RepartirPorFefoAsync(
        int sucursalId,
        int productoId,
        decimal cantidadBase,
        CancellationToken cancellationToken)
    {
        var repartos = new List<(Lote? Lote, decimal CantidadBase)>();
        var porCubrir = cantidadBase;

        // Vienen ordenados por vencimiento, con los sin fecha al final, y solo
        // los que tienen cantidad mayor que cero.
        var candidatos = await _inventario.ObtenerLotesPorVencimientoAsync(
            sucursalId, productoId, cancellationToken);

        foreach (var candidato in candidatos)
        {
            if (porCubrir <= 0m)
            {
                break;
            }

            // La consulta de arriba viene sin rastreo: hay que volver a pedir la
            // fila con bloqueo para poder modificarla.
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
            repartos.Add((null, porCubrir));
        }

        return repartos;
    }

    /// <summary>
    /// Pone en la sede destino el lote que salio del origen, con su MISMO numero
    /// y vencimiento.
    ///
    /// Si el destino ya tiene un lote con ese numero, se le suma; si no, se crea.
    /// Es lo que conserva la trazabilidad del fabricante de punta a punta: sin
    /// esto, el producto llegaria al destino como stock anonimo y se perderia el
    /// vencimiento, que es lo que ordena la cola FEFO.
    ///
    /// La fecha de vencimiento del lote que ya existe NO se pisa, por lo mismo
    /// que en la recepcion de compras: si difiere, lo probable es un error de
    /// digitacion, y sobrescribirla podria alargar la vida util de un producto
    /// vencido.
    /// </summary>
    private async Task<int> RecrearLoteEnDestinoAsync(
        int sucursalDestinoId,
        int productoId,
        Lote loteOrigen,
        decimal cantidadBase,
        CancellationToken cancellationToken)
    {
        var existente = await _inventario.ObtenerLotePorNumeroParaActualizarAsync(
            sucursalDestinoId, productoId, loteOrigen.NumeroLote, cancellationToken);

        if (existente is not null)
        {
            _inventario.ActualizarCantidadLote(
                existente, (existente.CantidadBase ?? 0m) + cantidadBase);
            return existente.Id;
        }

        var nuevo = new Lote
        {
            ProductoId = productoId,
            SucursalId = sucursalDestinoId,
            NumeroLote = loteOrigen.NumeroLote,
            FechaVencimiento = loteOrigen.FechaVencimiento,
            CantidadBase = cantidadBase
            // FechaIngreso la pone la base: es la llegada a ESTA sede, no la
            // del origen. El vencimiento, que es lo que importa para FEFO, si
            // se conserva.
        };
        _inventario.AgregarLote(nuevo);

        // Hace falta guardar ya: el movimiento de esta linea lleva `lote_id`, y
        // hasta que MySQL no asigne el id no hay valor que poner.
        await _transferencias.GuardarCambiosAsync(cancellationToken);

        return nuevo.Id;
    }

    /// <summary>Sube el saldo de la sede destino y devuelve como quedo.</summary>
    private async Task<decimal> IngresarAlSaldoDestinoAsync(
        int sucursalId,
        int productoId,
        decimal cantidadBase,
        CancellationToken cancellationToken)
    {
        var saldo = await _inventario.ObtenerSaldoParaActualizarAsync(
            sucursalId, productoId, cancellationToken);

        if (saldo is null)
        {
            // Primera vez que este producto llega a esta sede.
            _inventario.AgregarSaldo(new InventarioSucursal
            {
                SucursalId = sucursalId,
                ProductoId = productoId,
                CantidadBase = cantidadBase,
                StockMinimo = 0m,
                CostoPromedio = 0m
            });
            return cantidadBase;
        }

        var resultante = saldo.CantidadBase + cantidadBase;
        _inventario.ActualizarCantidadBase(saldo, resultante);
        return resultante;
    }

    // =========================================================================
    // CONVERSION DE UNIDADES
    // =========================================================================

    private readonly record struct ConversionTraslado(
        decimal CantidadBase,
        ErrorTransferencia? Error,
        string? Mensaje);

    /// <summary>
    /// Busca los factores y delega en <see cref="ConversorUnidades"/>, la misma
    /// formula que usan Inventario, Compras y Ventas. Aqui solo se traduce el
    /// resultado a los codigos de error de este modulo.
    /// </summary>
    private async Task<ConversionTraslado> ConvertirAsync(
        decimal cantidad,
        int unidadId,
        Producto producto,
        CancellationToken cancellationToken)
    {
        if (producto.UnidadBaseId is null)
        {
            return new ConversionTraslado(0m, ErrorTransferencia.ProductoSinUnidadBase,
                $"El producto '{producto.Nombre}' no tiene unidad base definida, " +
                "asi que no hay unidad a la cual convertir la cantidad del traslado.");
        }

        var unidadBaseId = producto.UnidadBaseId.Value;
        decimal? factorUnidad = null;
        decimal? factorUnidadBase = null;

        if (unidadId != unidadBaseId)
        {
            var origen = await _unidades.ObtenerFactorConversionLitrosAsync(unidadId, cancellationToken);
            if (!origen.UnidadExiste)
            {
                return new ConversionTraslado(0m, ErrorTransferencia.UnidadNoEncontrada,
                    $"No existe la unidad de medida {unidadId}.");
            }

            var destino = await _unidades.ObtenerFactorConversionLitrosAsync(unidadBaseId, cancellationToken);
            if (!destino.UnidadExiste)
            {
                return new ConversionTraslado(0m, ErrorTransferencia.UnidadNoEncontrada,
                    $"No existe la unidad base {unidadBaseId} que declara el producto.");
            }

            factorUnidad = origen.FactorLitros;
            factorUnidadBase = destino.FactorLitros;
        }

        var conversion = ConversorUnidades.ABaseDelProducto(
            cantidad, unidadId, factorUnidad, unidadBaseId, factorUnidadBase);

        return conversion.Estado switch
        {
            EstadoConversion.Ok =>
                new ConversionTraslado(conversion.CantidadBase, null, null),

            EstadoConversion.SinFactor =>
                new ConversionTraslado(0m, ErrorTransferencia.ConversionImposible,
                    $"No hay conversion entre la unidad {unidadId} y la unidad base " +
                    $"{unidadBaseId}: alguna de las dos no tiene factor a litros."),

            _ =>
                new ConversionTraslado(0m, ErrorTransferencia.CantidadBaseCero,
                    $"Convertida a unidad base, la cantidad {Num(cantidad)} se redondea a " +
                    $"cero con {ConversorUnidades.DecimalesCantidadBase} decimales.")
        };
    }

    // =========================================================================
    // AUXILIARES
    // =========================================================================

    /// <summary>
    /// Pasa una parte de la cantidad base de vuelta a la unidad del traslado,
    /// para que `cantidad` y `unidad_id` del movimiento sean coherentes.
    ///
    /// Se deriva por regla de tres en vez de volver a consultar los factores: la
    /// cantidad entera ya se convirtio, asi que da el mismo resultado sin otra
    /// ida a la base.
    /// </summary>
    private static decimal ProporcionEnUnidadTraslado(
        decimal parteBase,
        decimal totalBase,
        decimal totalEnUnidad)
    {
        if (totalBase <= 0m)
        {
            return 0m;
        }

        var valor = Math.Round(
            totalEnUnidad * parteBase / totalBase,
            ConversorUnidades.DecimalesCantidadBase,
            MidpointRounding.AwayFromZero);

        // El CHECK `chk_movinv_cantidad` exige > 0. Un reparto tan pequeno que
        // se redondea a cero deja la cantidad en el minimo representable; la
        // cifra fiable es `cantidad_base`, que no pasa por este redondeo.
        return valor > 0m ? valor : 0.0001m;
    }

    private static string ConstruirObservacion(string prefijo, string? observaciones) =>
        string.IsNullOrWhiteSpace(observaciones) ? prefijo : $"{prefijo} | {observaciones}";

    private static string DetalleLotes(IEnumerable<MovimientoInventario> movimientos)
    {
        var detalle = string.Empty;

        foreach (var movimiento in movimientos)
        {
            detalle += movimiento.LoteId is int loteId
                ? $" || lote={loteId} {Num(movimiento.CantidadBase ?? 0m)} mov={movimiento.Id}"
                // Queda explicito: es la senal de que los lotes no cubren el
                // saldo de la sede.
                : $" || lote=SIN_ASIGNAR {Num(movimiento.CantidadBase ?? 0m)} mov={movimiento.Id}";
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
