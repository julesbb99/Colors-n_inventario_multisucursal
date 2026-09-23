import { useState } from 'react';
import { Modal } from '../ui/Modal';
import { Boton } from '../ui/Boton';
import { Alerta } from '../ui/Alerta';
import { Spinner, CargandoPanel } from '../ui/Spinner';
import { aNumero, CampoArea, CampoNumero } from '../ui/Campos';
import { useConsulta } from '../../hooks/useConsulta';
import { useEnvio } from '../../hooks/useEnvio';
import { obtenerOrdenCompra, recibirOrdenCompra } from '../../services/compras';
import type { LineaRecepcionDto, OrdenCompraDto } from '../../models/compras';
import { formatearVolumen } from '../../utils/formato';

interface BorradorLinea {
  cantidad: string;
  numeroLote: string;
  fechaVencimiento: string;
}

const BORRADOR_VACIO: BorradorLinea = { cantidad: '', numeroLote: '', fechaVencimiento: '' };

interface FormularioRecepcionProps {
  orden: OrdenCompraDto;
  onCerrar: () => void;
  onRecibida: () => void;
}

/**
 * Registrar la llegada de mercancía de una orden.
 *
 * ESTE es el paso que mueve stock: suma al saldo de la sede, crea o alimenta el
 * lote y deja fila en el libro mayor. La orden por sí sola no movía nada.
 *
 * Solo se listan las líneas PENDIENTES. Una orden admite recepciones parciales,
 * y ofrecer una línea ya completa solo invita a recibirla dos veces.
 *
 * LA ORDEN SE VUELVE A PEDIR POR ID, aunque llegue una por parámetro. La que
 * llega viene del LISTADO, y el listado no trae las líneas -serían cientos de
 * filas que nadie mira-, así que su `detalles` está vacío. Sin esta consulta el
 * formulario no encontraba ninguna línea pendiente y anunciaba que la orden ya
 * había llegado completa, sin dejar revisar nada.
 */
export function FormularioRecepcion({ orden, onCerrar, onRecibida }: FormularioRecepcionProps) {
  const { enviando, error, esPermisos, enviar } = useEnvio();

  const {
    datos: completa,
    cargando,
    error: errorCarga,
  } = useConsulta(() => obtenerOrdenCompra(orden.id), [orden.id], orden);

  // Mientras carga, `completa` es todavía la del listado y no tiene líneas. Se
  // distingue con `cargando` para no volver a anunciar "llegó completa" durante
  // el instante en que aún no han llegado.
  const pendientes = cargando ? [] : completa.detalles.filter((detalle) => !detalle.completa);

  // Sin sembrar: cada campo cae a cadena vacía si no hay borrador para esa
  // línea. Sembrarlo al montar no serviría, porque las líneas llegan después.
  const [borradores, setBorradores] = useState<Record<number, BorradorLinea>>({});
  const [observaciones, setObservaciones] = useState('');

  function actualizar(detalleId: number, cambios: Partial<BorradorLinea>) {
    setBorradores((previos) => {
      // Las líneas llegan después del primer render, así que puede no haber
      // borrador todavía para esta.
      const base = previos[detalleId] ?? BORRADOR_VACIO;
      return { ...previos, [detalleId]: { ...base, ...cambios } };
    });
  }

  /**
   * Cómo queda una línea con lo que se lleva escrito.
   *
   * Se calcula al vuelo y se muestra debajo del campo para que la decisión
   * -completa o parcial- se vea ANTES de confirmar, y no se descubra después en
   * el estado de la orden.
   */
  function comoQueda(pendiente: number, texto: string) {
    // Vacío no es un error: significa «llegó todo lo que faltaba».
    const recibe = texto.trim() === '' ? pendiente : aNumero(texto);

    if (recibe === null || recibe < 0) {
      return { tono: 'malo' as const, mensaje: 'Cantidad no válida.' };
    }
    if (recibe > pendiente) {
      // La API lo rechaza en vez de recortarlo: recibir de más suele significar
      // que se digitó la línea equivocada, y recortar en silencio lo ocultaría.
      return {
        tono: 'malo' as const,
        mensaje: `Son más de los ${formatearVolumen(pendiente, null)} pendientes. El servidor lo va a rechazar.`,
      };
    }
    if (recibe === 0) {
      return { tono: 'neutro' as const, mensaje: 'No se recibe nada de esta línea.' };
    }
    if (recibe === pendiente) {
      return { tono: 'bueno' as const, mensaje: 'Esta línea queda COMPLETA.' };
    }
    return {
      tono: 'parcial' as const,
      // Por `formatearVolumen` y no por resta cruda: 3 − 0,7 da
      // 2.3000000000000003 en coma flotante, y eso es lo que se leería.
      mensaje: `Queda PARCIAL: faltarían ${formatearVolumen(pendiente - recibe, null)}.`,
    };
  }

  const TONOS = {
    bueno: 'text-emerald-700',
    parcial: 'text-amber-700',
    neutro: 'text-slate-500',
    malo: 'text-red-700',
  } as const;

  /**
   * Las líneas a las que les falta el lote o la fecha.
   *
   * Se calcula en el render y no solo al enviar: es lo que pinta el aviso de
   * arriba y desactiva el botón, para que no se llegue a pulsar y se descubra
   * después. La API valida lo mismo, pero contesta de línea en línea —se para
   * en la primera—, y aquí se pueden señalar todas de una vez.
   */
  const incompletas = pendientes.filter((detalle) => {
    const borrador = borradores[detalle.id] ?? BORRADOR_VACIO;
    return borrador.numeroLote.trim() === '' || borrador.fechaVencimiento === '';
  });

  function alGuardar() {
    if (incompletas.length > 0) {
      return;
    }

    const lineas: LineaRecepcionDto[] = pendientes.map((detalle) => {
      const borrador = borradores[detalle.id] ?? BORRADOR_VACIO;

      return {
        detalleId: detalle.id,
        // Vacío significa "llegó todo lo que faltaba de esta línea". Es lo que
        // espera la API, y evita tener que teclear la cifra exacta en el caso
        // normal, que es el de la entrega completa. `aNumero('')` ya da null.
        cantidad: aNumero(borrador.cantidad),
        // Estos dos sí van siempre: `incompletas` garantiza que no están vacíos.
        numeroLote: borrador.numeroLote.trim(),
        fechaVencimiento: borrador.fechaVencimiento,
      };
    });

    void enviar(async () => {
      await recibirOrdenCompra(orden.id, { ordenCompraId: orden.id, lineas,
        observaciones: observaciones.trim() === '' ? null : observaciones.trim() });
      onRecibida();
      onCerrar();
    });
  }

  return (
    <Modal
      titulo={`Recibir orden ${orden.id}`}
      descripcion={`${orden.proveedorNombre} · ${orden.sucursalNombre}. Este paso sí mueve el stock.`}
      ancho="xl"
      ocupado={enviando}
      onCerrar={onCerrar}
      pie={
        <>
          <Boton variante="secundaria" onClick={onCerrar} disabled={enviando}>
            Cancelar
          </Boton>
          <Boton
            onClick={alGuardar}
            disabled={
              enviando || cargando || pendientes.length === 0 || incompletas.length > 0
            }
          >
            {enviando ? (
              <>
                <Spinner etiqueta="Registrando" />
                Registrando…
              </>
            ) : (
              'Confirmar recepción'
            )}
          </Boton>
        </>
      }
    >
      <div className="flex flex-col gap-5">
        {error ? <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta> : null}
        {errorCarga ? <Alerta tipo="error">{errorCarga}</Alerta> : null}

        {cargando ? (
          <CargandoPanel texto="Cargando las líneas de la orden…" />
        ) : pendientes.length === 0 ? (
          <Alerta tipo="info">
            Esta orden ya no tiene líneas pendientes: llegó completa.
          </Alerta>
        ) : (
          <>
            <p className="text-sm text-slate-500">
              Revisa línea por línea y escribe cuánto llegó de verdad. Si dejas la cantidad vacía
              se recibe todo lo que falta de esa línea. El número de lote y la caducidad se leen
              del envase y son obligatorios: el número crea el lote o suma al que ya exista con
              ese número en la sede, y la fecha es la que ordena la cola FEFO.
            </p>

            {/*
              Aparece solo cuando falta algo, y NOMBRA los productos. Un aviso
              genérico obligaría a recorrer las líneas una por una buscando cuál
              quedó a medias, que es justo lo que hace falta cuando la orden trae
              seis productos.
            */}
            {incompletas.length > 0 ? (
              <Alerta tipo="info">
                Falta el lote o la caducidad de{' '}
                <span className="font-semibold">
                  {incompletas.map((d) => d.productoNombre).join(', ')}
                </span>
                . Los dos vienen impresos en el envase.
              </Alerta>
            ) : null}

            <div className="flex flex-col gap-3">
              {pendientes.map((detalle) => (
                <div
                  key={detalle.id}
                  className="rounded-xl border border-slate-200 bg-slate-50 p-3"
                >
                  <div className="mb-3 flex flex-wrap items-baseline gap-x-3 gap-y-1">
                    <span className="font-semibold text-slate-900">{detalle.productoNombre}</span>
                    <span className="text-sm text-slate-500">
                      pendiente{' '}
                      <span className="font-semibold tabular-nums text-amber-700">
                        {formatearVolumen(detalle.cantidadPendiente, detalle.unidadSimbolo)}
                      </span>{' '}
                      de {formatearVolumen(detalle.cantidad, detalle.unidadSimbolo)}
                    </span>
                  </div>

                  <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                    <div className="flex flex-col gap-1.5">
                      <CampoNumero
                        etiqueta="Cantidad recibida"
                        valor={borradores[detalle.id]?.cantidad ?? ''}
                        onCambio={(v) => actualizar(detalle.id, { cantidad: v })}
                        disabled={enviando}
                        placeholder={`Todo (${formatearVolumen(detalle.cantidadPendiente, null)})`}
                      />
                      {(() => {
                        const estado = comoQueda(
                          detalle.cantidadPendiente,
                          borradores[detalle.id]?.cantidad ?? '',
                        );
                        return (
                          <span className={`text-xs font-medium ${TONOS[estado.tono]}`}>
                            {estado.mensaje}
                          </span>
                        );
                      })()}
                    </div>

                    <div className="flex flex-col gap-1.5">
                      <label
                        htmlFor={`lote-${detalle.id}`}
                        className="text-sm font-semibold text-slate-700"
                      >
                        Número de lote
                        <span className="ml-1 text-terracota-600">*</span>
                      </label>
                      <input
                        id={`lote-${detalle.id}`}
                        type="text"
                        disabled={enviando}
                        value={borradores[detalle.id]?.numeroLote ?? ''}
                        onChange={(e) => actualizar(detalle.id, { numeroLote: e.target.value })}
                        placeholder="El del envase"
                        // El borde en ámbar mientras falta: señala la casilla
                        // concreta, que es lo que el aviso de arriba no puede
                        // hacer cuando la orden trae varias líneas.
                        className={`h-11 w-full rounded-lg border bg-white px-3 text-sm text-slate-900 outline-none focus:border-petroleo-500 ${
                          (borradores[detalle.id]?.numeroLote ?? '').trim() === ''
                            ? 'border-amber-400'
                            : 'border-slate-300'
                        }`}
                      />
                    </div>

                    <div className="flex flex-col gap-1.5">
                      <label
                        htmlFor={`vence-${detalle.id}`}
                        className="text-sm font-semibold text-slate-700"
                      >
                        Vencimiento
                        <span className="ml-1 text-terracota-600">*</span>
                      </label>
                      <input
                        id={`vence-${detalle.id}`}
                        type="date"
                        disabled={enviando}
                        value={borradores[detalle.id]?.fechaVencimiento ?? ''}
                        onChange={(e) =>
                          actualizar(detalle.id, { fechaVencimiento: e.target.value })
                        }
                        className={`h-11 w-full rounded-lg border bg-white px-3 text-sm tabular-nums text-slate-900 outline-none focus:border-petroleo-500 ${
                          (borradores[detalle.id]?.fechaVencimiento ?? '') === ''
                            ? 'border-amber-400'
                            : 'border-slate-300'
                        }`}
                      />
                    </div>
                  </div>
                </div>
              ))}
            </div>

            <CampoArea
              etiqueta="Observaciones"
              valor={observaciones}
              onCambio={setObservaciones}
              disabled={enviando}
              placeholder="Opcional"
            />
          </>
        )}
      </div>
    </Modal>
  );
}
