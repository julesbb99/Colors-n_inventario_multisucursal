import { useState } from 'react';
import { Modal } from '../ui/Modal';
import { Boton } from '../ui/Boton';
import { Alerta } from '../ui/Alerta';
import { Spinner, CargandoPanel } from '../ui/Spinner';
import { CampoArea } from '../ui/Campos';
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
    const recibe = texto.trim() === '' ? pendiente : Number(texto);

    if (!Number.isFinite(recibe) || recibe < 0) {
      return { tono: 'malo' as const, mensaje: 'Cantidad no válida.' };
    }
    if (recibe > pendiente) {
      // La API lo rechaza en vez de recortarlo: recibir de más suele significar
      // que se digitó la línea equivocada, y recortar en silencio lo ocultaría.
      return {
        tono: 'malo' as const,
        mensaje: `Son más de los ${pendiente} pendientes. El servidor lo va a rechazar.`,
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
      mensaje: `Queda PARCIAL: faltarían ${pendiente - recibe}.`,
    };
  }

  const TONOS = {
    bueno: 'text-emerald-700',
    parcial: 'text-amber-700',
    neutro: 'text-slate-500',
    malo: 'text-red-700',
  } as const;

  function alGuardar() {
    const lineas: LineaRecepcionDto[] = pendientes.map((detalle) => {
      const borrador = borradores[detalle.id] ?? BORRADOR_VACIO;

      return {
        detalleId: detalle.id,
        // Vacío significa "llegó todo lo que faltaba de esta línea". Es lo que
        // espera la API, y evita tener que teclear la cifra exacta en el caso
        // normal, que es el de la entrega completa.
        cantidad: borrador.cantidad === '' ? null : Number(borrador.cantidad),
        numeroLote: borrador.numeroLote.trim() === '' ? null : borrador.numeroLote.trim(),
        fechaVencimiento: borrador.fechaVencimiento === '' ? null : borrador.fechaVencimiento,
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
          <Boton onClick={alGuardar} disabled={enviando || cargando || pendientes.length === 0}>
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
              se recibe todo lo que falta de esa línea. El número de lote crea uno nuevo o suma al
              que ya exista con ese número en la sede.
            </p>

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
                      <label
                        htmlFor={`cant-${detalle.id}`}
                        className="text-sm font-semibold text-slate-700"
                      >
                        Cantidad recibida
                      </label>
                      <input
                        id={`cant-${detalle.id}`}
                        type="number"
                        inputMode="decimal"
                        min={0}
                        step="any"
                        disabled={enviando}
                        value={borradores[detalle.id]?.cantidad ?? ''}
                        onChange={(e) => actualizar(detalle.id, { cantidad: e.target.value })}
                        placeholder={`Todo (${detalle.cantidadPendiente})`}
                        className="h-11 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm tabular-nums text-slate-900 outline-none focus:border-petroleo-500"
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
                      </label>
                      <input
                        id={`lote-${detalle.id}`}
                        type="text"
                        disabled={enviando}
                        value={borradores[detalle.id]?.numeroLote ?? ''}
                        onChange={(e) => actualizar(detalle.id, { numeroLote: e.target.value })}
                        placeholder="Opcional"
                        className="h-11 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 outline-none focus:border-petroleo-500"
                      />
                    </div>

                    <div className="flex flex-col gap-1.5">
                      <label
                        htmlFor={`vence-${detalle.id}`}
                        className="text-sm font-semibold text-slate-700"
                      >
                        Vencimiento
                      </label>
                      <input
                        id={`vence-${detalle.id}`}
                        type="date"
                        disabled={enviando}
                        value={borradores[detalle.id]?.fechaVencimiento ?? ''}
                        onChange={(e) =>
                          actualizar(detalle.id, { fechaVencimiento: e.target.value })
                        }
                        className="h-11 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 outline-none focus:border-petroleo-500"
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
