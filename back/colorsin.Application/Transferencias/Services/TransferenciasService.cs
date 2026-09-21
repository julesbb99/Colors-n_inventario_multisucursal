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
        bool incluirRetiradas = false,
        CancellationToken cancellationToken = default)
    {
        var transportadoras = await _transportadoras.ObtenerTodasAsync(
            incluirRetiradas, cancellationToken);

        return transportadoras.Select(t => t.ToDto()).ToList();
    }

    // =========================================================================
    // CATALOGO DE TRANSPORTADORAS
    // =========================================================================

    public async Task<ResultadoTransportadora> CrearTransportadoraAsync(
        GuardarTransportadoraDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        var validacion = await ValidarTransportadoraAsync(peticion, null, cancellationToken);
        if (validacion is not null)
        {
            return validacion;
        }

        var transportadora = new Transportadora
        {
            Nombre = peticion.Nombre.Trim(),
            TipoServicio = LeerTipoServicio(peticion.TipoServicio)!.Value,
            DiasEntrega = peticion.DiasEntrega
        };

        _transportadoras.Agregar(transportadora);
        await _transportadoras.GuardarCambiosAsync(cancellationToken);

        await _auditoria.RegistrarEventoAsync(
            Modulo, "CrearTransportadora", usuarioId,
            $"transportadora={transportadora.Id} | nombre='{transportadora.Nombre}' | " +
            $"tipo={transportadora.TipoServicio} | diasEntrega={transportadora.DiasEntrega}",
            cancellationToken);

        await _transportadoras.GuardarCambiosAsync(cancellationToken);

        return ResultadoTransportadora.Ok(
            transportadora.ToDto(),
            $"Transportadora '{transportadora.Nombre}' creada con un plazo de " +
            $"{transportadora.DiasEntrega} dia(s).");
    }

    public async Task<ResultadoTransportadora> ActualizarTransportadoraAsync(
        int id,
        GuardarTransportadoraDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        // Con seguimiento: esta fila se modifica.
        var transportadora = await _transportadoras.ObtenerParaActualizarAsync(
            id, cancellationToken);

        if (transportadora is null)
        {
            return ResultadoTransportadora.Fallo(
                ErrorTransportadora.NoEncontrada,
                $"No existe la transportadora {id}.");
        }

        var validacion = await ValidarTransportadoraAsync(peticion, id, cancellationToken);
        if (validacion is not null)
        {
            return validacion;
        }

        var antes =
            $"'{transportadora.Nombre}' {transportadora.TipoServicio} " +
            $"{transportadora.DiasEntrega}d";

        transportadora.Nombre = peticion.Nombre.Trim();
        transportadora.TipoServicio = LeerTipoServicio(peticion.TipoServicio)!.Value;
        transportadora.DiasEntrega = peticion.DiasEntrega;

        await _transportadoras.GuardarCambiosAsync(cancellationToken);

        await _auditoria.RegistrarEventoAsync(
            Modulo, "ActualizarTransportadora", usuarioId,
            $"transportadora={id} | {antes} -> '{transportadora.Nombre}' " +
            $"{transportadora.TipoServicio} {transportadora.DiasEntrega}d",
            cancellationToken);

        await _transportadoras.GuardarCambiosAsync(cancellationToken);

        return ResultadoTransportadora.Ok(
            transportadora.ToDto(),
            $"Transportadora '{transportadora.Nombre}' actualizada. El cambio de plazo afecta a " +
            "los despachos que vengan; los ya despachados conservan su fecha estimada.");
    }

    public async Task<ResultadoTransportadora> CambiarEstadoTransportadoraAsync(
        int id,
        bool activa,
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        var transportadora = await _transportadoras.ObtenerParaActualizarAsync(
            id, cancellationToken);

        if (transportadora is null)
        {
            return ResultadoTransportadora.Fallo(
                ErrorTransportadora.NoEncontrada,
                $"No existe la transportadora {id}.");
        }

        if (transportadora.Activo == activa)
        {
            return ResultadoTransportadora.Fallo(
                ErrorTransportadora.EstadoSinCambio,
                $"La transportadora '{transportadora.Nombre}' ya estaba " +
                $"{(activa ? "activa" : "retirada")}.");
        }

        transportadora.Activo = activa;
        await _transportadoras.GuardarCambiosAsync(cancellationToken);

        await _auditoria.RegistrarEventoAsync(
            Modulo,
            activa ? "ReactivarTransportadora" : "RetirarTransportadora",
            usuarioId,
            $"transportadora={id} | nombre='{transportadora.Nombre}' | " +
            $"activo={(activa ? "0->1" : "1->0")}",
            cancellationToken);

        await _transportadoras.GuardarCambiosAsync(cancellationToken);

        return ResultadoTransportadora.Ok(
            transportadora.ToDto(),
            activa
                ? $"Transportadora '{transportadora.Nombre}' reactivada: vuelve a ofrecerse al " +
                  "despachar."
                : $"Transportadora '{transportadora.Nombre}' retirada. No se borro: los traslados " +
                  "que llevo la siguen citando, con su guia y su fecha.");
    }

    /// <summary>
    /// Lo comun a crear y editar. Devuelve el rechazo, o <c>null</c> si todo
    /// esta bien.
    ///
    /// Va en un solo sitio a proposito: dos copias de estas cuatro reglas es
    /// como el alta termina aceptando lo que la edicion rechaza.
    /// </summary>
    private async Task<ResultadoTransportadora?> ValidarTransportadoraAsync(
        GuardarTransportadoraDto peticion,
        int? exceptoId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(peticion.Nombre))
        {
            return ResultadoTransportadora.Fallo(
                ErrorTransportadora.NombreNoIndicado,
                "La transportadora necesita nombre.");
        }

        if (LeerTipoServicio(peticion.TipoServicio) is null)
        {
            return ResultadoTransportadora.Fallo(
                ErrorTransportadora.TipoServicioInvalido,
                $"El tipo de servicio '{peticion.TipoServicio}' no es valido: " +
                "solo 'urgente' o 'estandar'.");
        }

        if (peticion.DiasEntrega < 1)
        {
            return ResultadoTransportadora.Fallo(
                ErrorTransportadora.DiasEntregaInvalido,
                $"El plazo de entrega debe ser de al menos 1 dia; llego {peticion.DiasEntrega}. " +
                "Un traslado que llega el mismo dia que sale no necesita transportadora.");
        }

        // Se comprueba antes de guardar para dar un mensaje util. No es la
        // garantia: entre esta consulta y el INSERT cabe otra alta, y ahi lo que
        // protege es que el catalogo sea corto y lo mantengan pocas personas.
        if (await _transportadoras.ExisteNombreAsync(
                peticion.Nombre, exceptoId, cancellationToken))
        {
            return ResultadoTransportadora.Fallo(
                ErrorTransportadora.NombreDuplicado,
                $"Ya hay una transportadora llamada '{peticion.Nombre.Trim()}'.");
        }

        return null;
    }

    /// <summary>
    /// El tipo de servicio que llega como texto, o <c>null</c> si no es ninguno
    /// de los dos.
    ///
    /// Sin distinguir mayusculas porque el DTO lo recibe como texto libre y
    /// "Urgente" es lo que escribiria cualquiera.
    /// </summary>
    private static TipoServicio? LeerTipoServicio(string? valor) =>
        valor?.Trim().ToLowerInvariant() switch
        {
            "urgente" => TipoServicio.Urgente,
            "estandar" => TipoServicio.Estandar,
            _ => null
        };

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

        // Cero o negativo no es "ajustar lo que puedo dar": es no despachar. Y
        // para eso esta el rechazo, que ademas deja dicho por que.
        if (peticion.CantidadDespachada is <= 0m)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.CantidadInvalida,
                $"La cantidad a despachar debe ser mayor que cero; llego " +
                $"{Num(peticion.CantidadDespachada!.Value)}. Si no se puede mandar nada, " +
                "rechaza el traslado en vez de despachar cero.");
        }

        // Se trae la fila entera en vez de preguntar solo si existe: hay que
        // mirar tambien que no este retirada, y el mensaje necesita su nombre.
        var transportadora = await _transportadoras.ObtenerPorIdAsync(
            peticion.TransportadoraId, cancellationToken);

        if (transportadora is null)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.TransportadoraNoEncontrada,
                $"No existe la transportadora {peticion.TransportadoraId}.");
        }

        if (!transportadora.Activo)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.TransportadoraRetirada,
                $"La transportadora '{transportadora.Nombre}' esta retirada del catalogo y no " +
                "se puede usar en despachos nuevos. Elige otra, o reactivala desde " +
                "Traslados -> Transportadoras.");
        }

        // LA FECHA ESTIMADA ES OBLIGATORIA, y antes no lo era.
        //
        // Un traslado EnTransito sin fecha de llegada no se puede reclamar: no
        // hay a partir de cuando decir que va tarde, ni con que comparar cuando
        // llegue. La pantalla la calcula sola con los dias de la transportadora,
        // asi que exigirla no le anade trabajo a nadie; lo que evita es que otra
        // via de entrada vuelva a dejarla vacia.
        if (peticion.FechaEstimadaLlegada is not DateTime estimada)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.FechaEstimadaNoIndicada,
                "Hay que indicar la fecha estimada de llegada: es con lo que se sabe si el " +
                "traslado va tarde. Se calcula sumando los dias de entrega de la " +
                "transportadora a la fecha de despacho.");
        }

        // Una llegada anterior al despacho solo puede ser un error de digitacion,
        // y deja un traslado que nace tarde. Se compara contra el dia, no contra
        // el instante, para que despachar a las 6 de la tarde con llegada "hoy"
        // -mismo dia, servicio de una hora- siga valiendo.
        if (estimada.Date < DateTime.Now.Date)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.FechaEstimadaInvalida,
                $"La fecha estimada de llegada ({estimada:yyyy-MM-dd}) es anterior a hoy. " +
                "Un traslado no puede llegar antes de salir.");
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

        var solicitada = transferencia.CantidadSolicitada ?? 0m;
        if (solicitada <= 0m)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.CantidadInvalida,
                $"El traslado {transferencia.Id} no tiene cantidad que despachar.");
        }

        // EL ORIGEN AJUSTA LO QUE PUEDE MANDAR. Sin cantidad se manda lo
        // pedido, que es el caso normal; con una menor, se manda esa y el
        // traslado queda diciendo las dos cifras.
        //
        // SOLO HACIA ABAJO. Mandar de mas seria stock que el destino no pidio
        // y cuya entrada su bodega no espera; ademas lo impide el CHECK
        // `chk_transf_despachada`, asi que aqui se rechaza con motivo en vez de
        // dejar que salte la base con un error sin explicacion.
        var cantidad = peticion.CantidadDespachada ?? solicitada;

        if (cantidad > solicitada)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.DespachadaExcedeSolicitada,
                $"Se intentan despachar {Num(cantidad)} y el traslado pidio {Num(solicitada)}. " +
                "El ajuste del origen sirve para mandar MENOS de lo pedido, no mas: la sede " +
                "destino no espera ese excedente. Si de verdad hace falta mas, que lo pidan en " +
                "otro traslado.");
        }

        var ajustado = cantidad < solicitada;

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
                $"{transferencia.SucursalOrigenId}: hay {Num(saldoOrigen.CantidadBase)} y se " +
                $"intentan despachar {Num(cantidadBase)}. " +
                "Se puede despachar menos de lo pedido indicando la cantidad que si hay.");
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
        transferencia.CantidadDespachada = cantidad;
        transferencia.FechaEstimadaLlegada = peticion.FechaEstimadaLlegada;
        // Se guarda aqui y no se deduce del movimiento: el informe de
        // cumplimiento la necesita en cada fila, y cruzar el libro mayor para
        // obtenerla convertiria el informe en una consulta por traslado.
        transferencia.FechaDespacho = DateTime.Now;
        transferencia.Estado = EstadoTransferencia.EnTransito;

        await _transferencias.GuardarCambiosAsync(cancellationToken);

        await _auditoria.RegistrarEventoAsync(
            Modulo, "DespacharTransferencia", usuarioId,
            $"transferencia={transferencia.Id} | estado=Solicitada->EnTransito | " +
            $"origen={transferencia.SucursalOrigenId} | producto={transferencia.ProductoId} | " +
            $"solicitada={Num(solicitada)} despachada={Num(cantidad)}" +
            (ajustado ? $" AJUSTADA(-{Num(solicitada - cantidad)})" : string.Empty) + " | " +
            $"cantidadBase=-{Num(cantidadBase)} | saldo={Num(saldoResultante)} | " +
            $"transportadora={peticion.TransportadoraId} | guia={transferencia.Guia}" +
            DetalleLotes(movimientos),
            cancellationToken);

        await _transferencias.GuardarCambiosAsync(cancellationToken);

        // El mensaje dice el ajuste cuando lo hay, y dice lo que NO significa:
        // la diferencia no se perdio, se quedo en el estante del origen. Quien
        // lo lea tiene que saber que ese traslado ya no va a traer los 5.
        var mensaje = ajustado
            ? $"Traslado {transferencia.Id} despachado con guia {transferencia.Guia}, ajustado a " +
              $"{Num(cantidad)} de los {Num(solicitada)} pedidos. Los {Num(solicitada - cantidad)} " +
              "restantes NO se perdieron: no salieron, siguen en el origen. Si el destino los " +
              "necesita, hay que pedirlos en otro traslado."
            : $"Traslado {transferencia.Id} despachado con guia {transferencia.Guia}.";

        return ResultadoTransferencia.Ok(
            transferencia.Id, EstadoTransferencia.EnTransito, mensaje,
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

        // NO SE RECIBE ANTES DE LA FECHA ESTIMADA DE LLEGADA.
        //
        // Se compara por DIA, no por instante: la fecha estimada se guarda a
        // medianoche, asi que comparar instantes haria que el dia de la llegada
        // no se pudiera recibir hasta las 00:00 del siguiente.
        //
        // Vale para todos los roles, incluido el Administrador General: quien
        // cuenta la mercancia es quien la tiene delante, y el rango no adelanta
        // el camion.
        if (transferencia.FechaEstimadaLlegada is DateTime estimada
            && DateTime.Now.Date < estimada.Date)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.RecepcionAnticipada,
                $"El traslado {transferencia.Id} no llega hasta el {estimada:yyyy-MM-dd} y hoy es " +
                $"{DateTime.Now:yyyy-MM-dd}. La recepcion se habilita ese dia: contar mercancia " +
                "que segun la guia todavia viaja solo puede salir de un conteo a ojo.");
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

        // SE COMPARA CONTRA LO DESPACHADO, NO CONTRA LO SOLICITADO, y este es el
        // punto donde mas se notaria la diferencia: si pidieron 5, el origen
        // solo tenia 3 y llegaron los 3, el traslado llego COMPLETO. Medirlo
        // contra los 5 lo marcaria como perdida en transito de dos unidades que
        // nunca subieron al camion, y ademas le cargaria esa merma a la
        // transportadora.
        //
        // El `?? CantidadSolicitada` cubre los traslados despachados antes de
        // que existiera la columna, que salieron por lo pedido.
        var solicitada = transferencia.CantidadSolicitada ?? 0m;
        var despachada = transferencia.CantidadDespachada ?? solicitada;
        var recibida = peticion.CantidadRecibida ?? despachada;

        if (recibida > despachada)
        {
            return ResultadoTransferencia.Fallo(
                ErrorTransferencia.RecibidaExcedeDespachada,
                $"El traslado despacho {Num(despachada)}" +
                (despachada < solicitada ? $" (de {Num(solicitada)} pedidos)" : string.Empty) +
                $" y se intentan recibir {Num(recibida)}. Recibir de mas crearia stock de la " +
                "nada; si llego producto extra, registra una novedad de tipo Sobrante.");
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

        // Completada solo si llego todo LO DESPACHADO. Si falta, RecibidaParcial:
        // esa diferencia salio del origen y nunca entro al destino, asi que es
        // una baja neta de la red y el libro mayor la refleja tal cual.
        var completa = recibida >= despachada;
        var estadoNuevo = completa
            ? EstadoTransferencia.Completada
            : EstadoTransferencia.RecibidaParcial;

        transferencia.CantidadRecibida = recibida;
        transferencia.FechaRecepcion = DateTime.Now;
        transferencia.Estado = estadoNuevo;

        await _transferencias.GuardarCambiosAsync(cancellationToken);

        // Perdido en el camino. NO es solicitada - recibida: eso mezclaria lo
        // que el origen no mando con lo que se extravio, y son cosas distintas
        // con responsables distintos.
        var faltante = despachada - recibida;
        var noDespachado = solicitada - despachada;
        var tarde = transferencia.FechaEstimadaLlegada is DateTime fe
                    && DateTime.Now.Date > fe.Date;

        await _auditoria.RegistrarEventoAsync(
            Modulo, "RecibirTransferencia", usuarioId,
            $"transferencia={transferencia.Id} | estado=EnTransito->{estadoNuevo} | " +
            $"destino={transferencia.SucursalDestinoId} | producto={transferencia.ProductoId} | " +
            $"solicitada={Num(solicitada)} despachada={Num(despachada)} " +
            $"recibida={Num(recibida)} | faltanteEnTransito={Num(faltante)} " +
            $"noDespachado={Num(noDespachado)} | plazo={(tarde ? "TARDE" : "a tiempo")} | " +
            $"cantidadBase=+{Num(recibidaBase)} | saldo={Num(saldoResultante)}" +
            DetalleLotes(movimientos),
            cancellationToken);

        await _transferencias.GuardarCambiosAsync(cancellationToken);

        var mensaje = completa
            ? $"Traslado {transferencia.Id} recibido completo en la sede " +
              $"{transferencia.SucursalDestinoId}: llego todo lo que se despacho " +
              $"({Num(despachada)})." +
              (noDespachado > 0m
                  ? $" El origen habia ajustado el envio: de los {Num(solicitada)} pedidos no " +
                    $"salieron {Num(noDespachado)}, que siguen en su bodega."
                  : string.Empty)
            : $"Traslado {transferencia.Id} recibido parcial: llegaron {Num(recibida)} de los " +
              $"{Num(despachada)} despachados. Faltan {Num(faltante)}, perdidos en transito. " +
              "Hay que registrar la novedad y decir que se hace con ellos: reenvio, " +
              "reclamacion a la transportadora, o darlos por perdidos.";

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

            // EL TRATAMIENTO DECIDE SI LA NOVEDAD NACE ABIERTA.
            //
            // Reenvio y Reclamacion dejan trabajo por delante: alguien tiene que
            // volver a mandar la mercancia, o pelear el cobro con la
            // transportadora. Mientras eso no se resuelva la novedad sigue
            // abierta y el traslado NO puede cerrarse.
            //
            // Ninguno y Asumido no esperan nada: el apunte esta completo en el
            // momento en que se escribe.
            var quedaAbierta =
                peticion.Tratamiento is TratamientoNovedad.Reenvio
                                     or TratamientoNovedad.Reclamacion;

            // NO se valida el estado del traslado a proposito: los danos se
            // descubren al abrir las cajas, que suele ser despues de cerrarlo.
            var novedad = new NovedadTransferencia
            {
                TransferenciaId = peticion.TransferenciaId,
                UsuarioId = usuarioId,
                Tipo = peticion.Tipo,
                CantidadAfectada = peticion.CantidadAfectada,
                Tratamiento = peticion.Tratamiento,
                Estado = quedaAbierta ? EstadoNovedad.Abierta : EstadoNovedad.Cerrada,
                Observaciones = peticion.Observaciones
                // Fecha la pone la base con CURRENT_TIMESTAMP.
            };
            _transferencias.AgregarNovedad(novedad);

            // LA NOVEDAD CIERRA EL TRASLADO QUE LLEGO CORTO, pero solo si no
            // deja nada pendiente.
            //
            // Antes lo cerraba siempre, y eso daba por terminado lo que seguia
            // en curso: un faltante que el origen va a reenviar no esta
            // resuelto, esta esperando. Ahora el traslado se queda en
            // 'RecibidaParcial' -o sea, por recibir- hasta que esa novedad se
            // cierre con su motivo.
            //
            // SOLO DESDE RecibidaParcial. Una novedad sobre uno EnTransito es un
            // retraso o un aviso, y ese traslado sigue viajando: cerrarlo ahi
            // daria por terminada mercancia que aun no ha llegado. Y sobre uno ya
            // cerrado no hay nada que cambiar.
            var estadoAnterior = transferencia.Estado;
            var cierra = !quedaAbierta
                      && estadoAnterior == EstadoTransferencia.RecibidaParcial;

            if (cierra)
            {
                transferencia.Estado = EstadoTransferencia.Cerrada;
            }

            await _transferencias.GuardarCambiosAsync(ct);

            await _auditoria.RegistrarEventoAsync(
                Modulo, "RegistrarNovedadTransferencia", usuarioId,
                $"novedad={novedad.Id} | transferencia={peticion.TransferenciaId} | " +
                $"tipo={peticion.Tipo} | tratamiento={peticion.Tratamiento} | " +
                $"novedad={(quedaAbierta ? "ABIERTA (pendiente de desenlace)" : "cerrada al nacer")} | " +
                $"cantidadAfectada={(peticion.CantidadAfectada is null ? "(sin indicar)" : Num(peticion.CantidadAfectada.Value))} | " +
                $"estadoTraslado={(cierra ? $"{estadoAnterior}->Cerrada" : $"{estadoAnterior} (sin cambios)")} | " +
                $"obs={peticion.Observaciones ?? "(sin observaciones)"}",
                ct);

            await _transferencias.GuardarCambiosAsync(ct);

            return ResultadoNovedad.Ok(novedad.Id, peticion.Tipo, cierra, quedaAbierta);
        }, cancellationToken);
    }

    public async Task<ResultadoNovedad> CerrarNovedadAsync(
        int novedadId,
        string? motivo,
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (usuarioId <= 0)
        {
            return ResultadoNovedad.Fallo(
                ErrorTransferencia.UsuarioNoIndicado,
                $"Hay que indicar el usuario que cierra; llego {usuarioId}.");
        }

        // EL MOTIVO ES OBLIGATORIO, y no por formalismo. Sin el, cerrar una
        // novedad seria indistinguible de borrarla: dentro de seis meses nadie
        // sabria si aquel faltante aparecio, si lo pago la transportadora o si
        // se dio por perdido. Esa frase ES el valor de guardar la novedad.
        if (string.IsNullOrWhiteSpace(motivo))
        {
            return ResultadoNovedad.Fallo(
                ErrorTransferencia.MotivoCierreNoIndicado,
                "Hay que decir POR QUE se cierra la novedad: si llego lo que faltaba, si lo " +
                "respondio la transportadora o si se da por perdido. Es lo unico que queda para " +
                "revisarlo despues.");
        }

        return await _transferencias.EjecutarEnTransaccionAsync(async ct =>
        {
            var novedad = await _transferencias.ObtenerNovedadParaOperarAsync(novedadId, ct);

            if (novedad is null)
            {
                return ResultadoNovedad.Fallo(
                    ErrorTransferencia.NovedadNoEncontrada,
                    $"No existe la novedad {novedadId}.");
            }

            if (novedad.Estado == EstadoNovedad.Cerrada)
            {
                return ResultadoNovedad.Fallo(
                    ErrorTransferencia.NovedadYaCerrada,
                    $"La novedad {novedadId} ya estaba cerrada" +
                    (novedad.FechaCierre is DateTime f ? $" el {f:yyyy-MM-dd}" : string.Empty) +
                    ". No hay desenlace que registrar.");
            }

            // Se bloquea el traslado ANTES de tocar nada: el cierre puede
            // cambiarle el estado, y dos cierres simultaneos de dos novedades
            // del mismo traslado no pueden ver los dos "me queda una abierta".
            var transferencia = await _transferencias.ObtenerParaOperarAsync(
                novedad.TransferenciaId, ct);

            if (transferencia is null)
            {
                return ResultadoNovedad.Fallo(
                    ErrorTransferencia.TransferenciaNoEncontrada,
                    $"No existe el traslado {novedad.TransferenciaId} de la novedad {novedadId}.");
            }

            var tratamiento = novedad.Tratamiento;

            novedad.Estado = EstadoNovedad.Cerrada;
            novedad.MotivoCierre = motivo.Trim();
            novedad.FechaCierre = DateTime.Now;
            novedad.UsuarioCierreId = usuarioId;

            // El traslado cierra cuando se le acaban las pendientes, no con la
            // primera que se resuelva: uno que llego corto Y con una lata rota
            // tiene dos desenlaces que esperar.
            var otrasAbiertas = await _transferencias.ContarNovedadesAbiertasAsync(
                novedad.TransferenciaId, novedadId, ct);

            var estadoAnterior = transferencia.Estado;
            var cierraTraslado = otrasAbiertas == 0
                              && estadoAnterior == EstadoTransferencia.RecibidaParcial;

            if (cierraTraslado)
            {
                transferencia.Estado = EstadoTransferencia.Cerrada;
            }

            await _transferencias.GuardarCambiosAsync(ct);

            await _auditoria.RegistrarEventoAsync(
                Modulo, "CerrarNovedadTransferencia", usuarioId,
                $"novedad={novedadId} | transferencia={novedad.TransferenciaId} | " +
                $"tipo={novedad.Tipo} | tratamiento={tratamiento} | estadoNovedad=Abierta->Cerrada | " +
                $"otrasAbiertas={otrasAbiertas} | " +
                $"estadoTraslado={(cierraTraslado ? $"{estadoAnterior}->Cerrada" : $"{estadoAnterior} (sin cambios)")} | " +
                $"motivo={motivo.Trim()}",
                ct);

            await _transferencias.GuardarCambiosAsync(ct);

            return ResultadoNovedad.Cerrada(novedadId, cierraTraslado);
        }, cancellationToken);
    }

    // =========================================================================
    // INFORME DE CUMPLIMIENTO LOGISTICO
    // =========================================================================

    public async Task<ReporteCumplimientoDto> ObtenerReporteCumplimientoAsync(
        DateTime? desde = null,
        DateTime? hasta = null,
        int? sucursalId = null,
        CancellationToken cancellationToken = default)
    {
        var traslados = await _transferencias.ObtenerParaReporteAsync(
            desde, hasta, sucursalId, cancellationToken);

        // POR SEDE SE AGRUPA POR EL ORIGEN, no por el destino.
        //
        // El origen es quien responde: decide si atiende la peticion, decide
        // cuanto manda y elige la transportadora. El destino solo cuenta lo que
        // le llega. Agrupar por destino calificaria a una sede por el trabajo
        // de otra, que es exactamente lo que un informe de cumplimiento no debe
        // hacer.
        var porSucursal = traslados
            .GroupBy(t => (t.SucursalOrigenId, Nombre: t.SucursalOrigen?.Nombre ?? string.Empty))
            .Select(g => Resumir(
                $"sede-{g.Key.SucursalOrigenId}",
                g.Key.Nombre,
                g.Key.SucursalOrigenId,
                null,
                g))
            .OrderByDescending(g => g.Solicitados)
            .ThenBy(g => g.Etiqueta, StringComparer.CurrentCulture)
            .ToList();

        var porRuta = traslados
            .GroupBy(t => (
                t.SucursalOrigenId,
                t.SucursalDestinoId,
                Origen: t.SucursalOrigen?.Nombre ?? string.Empty,
                Destino: t.SucursalDestino?.Nombre ?? string.Empty))
            .Select(g => Resumir(
                $"ruta-{g.Key.SucursalOrigenId}-{g.Key.SucursalDestinoId}",
                // La flecha, y no un guion: la ruta tiene sentido y hay que
                // poder leer de un vistazo cual sede manda y cual recibe.
                $"{g.Key.Origen} → {g.Key.Destino}",
                g.Key.SucursalOrigenId,
                g.Key.SucursalDestinoId,
                g))
            .OrderByDescending(g => g.Solicitados)
            .ThenBy(g => g.Etiqueta, StringComparer.CurrentCulture)
            .ToList();

        return new ReporteCumplimientoDto(
            desde,
            hasta,
            Resumir("total", "Toda la red", null, null, traslados),
            porSucursal,
            porRuta);
    }

    /// <summary>
    /// Cuenta un grupo de traslados.
    ///
    /// TODO SON CONTEOS Y NINGUNO ES UN VOLUMEN. Cada traslado lleva su producto
    /// en su unidad -litros, galones, canecas- asi que sumar cantidades entre
    /// traslados distintos daria una cifra con unidades mezcladas. Lo comparable
    /// es cuantos llegaron completos y cuantos a tiempo.
    /// </summary>
    private static CumplimientoGrupoDto Resumir(
        string clave,
        string etiqueta,
        int? origenId,
        int? destinoId,
        IEnumerable<Transferencia> grupo)
    {
        var lista = grupo as IList<Transferencia> ?? grupo.ToList();

        var solicitados = lista.Count;
        var rechazados = lista.Count(t => t.Estado == EstadoTransferencia.Rechazada);
        var cancelados = lista.Count(t => t.Estado == EstadoTransferencia.Cancelada);

        // Despachado = salio del origen. Se mira la FECHA y no el estado porque
        // el estado sigue avanzando -EnTransito, Completada, Cerrada- y todos
        // esos ya salieron.
        var despachados = lista.Count(t => t.FechaDespacho is not null);

        var recibidosLista = lista.Where(t => t.FechaRecepcion is not null).ToList();
        var recibidos = recibidosLista.Count;

        // Completo = llego todo LO QUE SE DESPACHO. Lo que el origen no mando
        // no es culpa del transporte, y se cuenta aparte en AjustadosEnOrigen.
        var completos = recibidosLista.Count(t =>
            (t.CantidadRecibida ?? 0m) >= (t.CantidadDespachada ?? t.CantidadSolicitada ?? 0m));

        // A tiempo se compara por DIA: la fecha estimada se guarda a medianoche
        // y la recepcion con su hora, asi que comparar instantes marcaria como
        // tarde todo lo que llega el mismo dia despues de las 00:00.
        //
        // Sin fecha estimada NO se cuenta ni a tiempo ni tarde: son los
        // traslados anteriores a que fuera obligatoria, y meterlos en cualquiera
        // de los dos lados falsearia el porcentaje.
        var conPlazo = recibidosLista.Where(t => t.FechaEstimadaLlegada is not null).ToList();
        var aTiempo = conPlazo.Count(t =>
            t.FechaRecepcion!.Value.Date <= t.FechaEstimadaLlegada!.Value.Date);
        var tarde = conPlazo.Count - aTiempo;

        var ajustados = lista.Count(t =>
            t.CantidadDespachada is decimal d
            && t.CantidadSolicitada is decimal s
            && d < s);

        var enCurso = lista.Count(t =>
            t.Estado is EstadoTransferencia.Solicitada
                     or EstadoTransferencia.EnTransito
                     or EstadoTransferencia.RecibidaParcial);

        var conNovedad = lista.Count(t => t.Novedades.Count > 0);
        var novedadesAbiertas = lista.Sum(t =>
            t.Novedades.Count(n => n.Estado == EstadoNovedad.Abierta));

        var transitos = recibidosLista
            .Where(t => t.FechaDespacho is not null)
            .Select(t => (decimal)(t.FechaRecepcion!.Value - t.FechaDespacho!.Value).TotalDays)
            .ToList();

        // La base de la atencion excluye los cancelados: los retiro quien los
        // pidio, asi que contarlos como peticiones no atendidas le bajaria la
        // nota a una bodega por algo que no decidio.
        var atendibles = solicitados - cancelados;

        return new CumplimientoGrupoDto(
            clave,
            etiqueta,
            origenId,
            destinoId,
            solicitados,
            despachados,
            rechazados,
            cancelados,
            enCurso,
            recibidos,
            completos,
            recibidos - completos,
            aTiempo,
            tarde,
            ajustados,
            conNovedad,
            novedadesAbiertas,
            transitos.Count == 0
                ? null
                : Math.Round(transitos.Sum() / transitos.Count, 1, MidpointRounding.AwayFromZero),
            Porcentaje(despachados, atendibles),
            Porcentaje(completos, recibidos),
            Porcentaje(aTiempo, conPlazo.Count));
    }

    /// <summary>
    /// Un porcentaje con un decimal, o <c>null</c> cuando no hay base.
    ///
    /// Nulo y no cero: "ningun traslado todavia" y "ninguno cumplio" son cosas
    /// distintas, y pintar un 0 % en una sede que aun no ha despachado nada la
    /// acusaria de algo que no hizo.
    /// </summary>
    private static decimal? Porcentaje(int parte, int total) =>
        total <= 0
            ? null
            : Math.Round(parte * 100m / total, 1, MidpointRounding.AwayFromZero);

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
