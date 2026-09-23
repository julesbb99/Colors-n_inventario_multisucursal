import { useState } from 'react';
import { AlertTriangle } from 'lucide-react';
import { Modal } from '../ui/Modal';
import { Boton } from '../ui/Boton';
import { Alerta } from '../ui/Alerta';
import { Spinner } from '../ui/Spinner';
import { aNumero, aTexto, CampoArea, CampoNumero, CampoSelect, CampoTexto } from '../ui/Campos';
import { useConsulta } from '../../hooks/useConsulta';
import { useEnvio } from '../../hooks/useEnvio';
import {
  cancelarTransferencia,
  cerrarNovedad,
  despacharTransferencia,
  obtenerTransportadoras,
  recibirTransferencia,
  rechazarTransferencia,
  registrarNovedad,
} from '../../services/transferencias';
import {
  ETIQUETA_TIPO_NOVEDAD,
  ETIQUETA_TRATAMIENTO,
  TIPO_NOVEDAD,
  TRATAMIENTO_NOVEDAD,
} from '../../models/transferencias';
import type {
  TransferenciaDto,
  TransportadoraDto,
  ValorTipoNovedad,
  ValorTratamientoNovedad,
} from '../../models/transferencias';
import { formatearFechaSolo, formatearVolumen } from '../../utils/formato';

export type AccionTraslado =
  | 'despacho'
  | 'recepcion'
  | 'novedad'
  | 'cierreNovedad'
  | 'rechazo'
  | 'cancelacion';

const SIN_TRANSPORTADORAS: TransportadoraDto[] = [];

const TITULOS: Record<AccionTraslado, string> = {
  despacho: 'Despachar traslado',
  recepcion: 'Recibir traslado',
  // Lo mismo que dice el botón que lo abre: uno que promete una cosa y abre
  // otra titulada distinto hace dudar de si se pulsó lo que se quería.
  novedad: 'Novedad de traslado',
  cierreNovedad: 'Cerrar novedad del traslado',
  rechazo: 'Rechazar traslado',
  cancelacion: 'Cancelar traslado',
};

/**
 * El texto del botón que confirma, que NO siempre es el título.
 *
 * En casi todas las acciones el título ya es un verbo y sirve tal cual. La
 * novedad no: se titula por lo que ES -«Novedad de traslado»- y el botón tiene
 * que decir lo que HACE, que es guardarla. «Novedad de traslado» en un botón no
 * dice si va a registrar algo o a abrir otra cosa.
 */
const ETIQUETA_ENVIO: Record<AccionTraslado, string> = {
  despacho: 'Despachar traslado',
  recepcion: 'Recibir traslado',
  novedad: 'Guardar novedad',
  cierreNovedad: 'Cerrar novedad',
  rechazo: 'Rechazar traslado',
  cancelacion: 'Cancelar traslado',
};

const DESCRIPCIONES: Record<AccionTraslado, string> = {
  despacho:
    'Lo hace la sede origen. Se puede ajustar la cantidad a lo que de verdad haya en el ' +
    'estante. Descuenta stock por FEFO y el traslado pasa a En tránsito.',
  recepcion:
    'Lo hace la sede destino, y no antes del día de llegada. Sin cantidad, llega todo lo ' +
    'despachado.',
  novedad:
    'Ya entró lo que llegó y el traslado quedó corto. Aquí se deja constancia y se decide qué ' +
    'se hace con el faltante. NO mueve stock: esa cantidad ya salió del inventario de la red.',
  cierreNovedad:
    'El desenlace de algo que quedó pendiente. No borra la novedad: le añade el porqué, la ' +
    'fecha y quién la cerró.',
  rechazo: 'Lo hace la sede origen, solo desde Solicitada. La mercancía no sale.',
  cancelacion: 'La retira quien la pidió, solo desde Solicitada.',
};

/**
 * Hoy más `dias`, en el formato `aaaa-mm-dd` que espera un `<input type="date">`.
 *
 * SE CONSTRUYE CON LOS COMPONENTES LOCALES, no con `toISOString()`. Ese método
 * convierte a UTC, y en Colombia (UTC−5) eso adelanta la fecha un día durante
 * toda la tarde: un despacho a las 7 p. m. del día 20 daría 21. La fecha de
 * llegada es un día del calendario de aquí, no un instante.
 */
function hoyMas(dias: number): string {
  const fecha = new Date();
  fecha.setDate(fecha.getDate() + dias);

  const mes = String(fecha.getMonth() + 1).padStart(2, '0');
  const dia = String(fecha.getDate()).padStart(2, '0');
  return `${fecha.getFullYear()}-${mes}-${dia}`;
}

/**
 * El texto que acompaña al faltante, según lo que se haya decidido hacer.
 *
 * Nombra a la transportadora a propósito: el faltante es, antes que una merma,
 * un reclamo contra quien lo transportó. Sin el nombre, dentro de un mes nadie
 * sabe a quién reclamarle.
 */
function notaDelFaltante(
  tratamiento: ValorTratamientoNovedad,
  transportadora: string | null,
  observaciones: string | null,
): string {
  const quien = transportadora === null ? 'la transportadora (sin registrar)' : transportadora;

  const cabecera =
    tratamiento === TRATAMIENTO_NOVEDAD.reenvio
      ? 'Faltante en tránsito. Se pide REENVÍO del origen; el traslado sigue pendiente hasta ' +
        'que llegue.'
      : tratamiento === TRATAMIENTO_NOVEDAD.reclamacion
        ? `Faltante en tránsito. Se RECLAMA a ${quien}; el traslado sigue pendiente hasta ` +
          'tener respuesta.'
        : `Faltante en tránsito con ${quien}. Se da por perdido: esto es la merma.`;

  return observaciones === null ? cabecera : `${cabecera} ${observaciones}`;
}

interface AccionesTrasladoProps {
  traslado: TransferenciaDto;
  accion: AccionTraslado;
  onCerrar: () => void;
  onHecho: () => void;
}

/**
 * Las acciones sobre un traslado ya creado.
 *
 * Van en un componente y no en seis porque comparten casi todo -el modal, el
 * estado de envío, el pie- y se diferencian en dos o tres campos. Seis archivos
 * casi idénticos es como terminan divergiendo en el manejo de errores.
 */
export function AccionesTraslado({
  traslado,
  accion,
  onCerrar,
  onHecho,
}: AccionesTrasladoProps) {
  const { enviando, error, esPermisos, enviar } = useEnvio();

  const { datos: transportadoras } = useConsulta(
    () => (accion === 'despacho' ? obtenerTransportadoras() : Promise.resolve(SIN_TRANSPORTADORAS)),
    [accion],
    SIN_TRANSPORTADORAS,
  );

  const solicitada = traslado.cantidadSolicitada;
  // Lo que salió de verdad. El `??` cubre los traslados despachados antes de
  // que la columna existiera, que salieron por lo pedido.
  const despachada = traslado.cantidadDespachada ?? solicitada;

  const abiertas = traslado.novedades.filter((n) => n.estado === 'Abierta');

  const [transportadoraId, setTransportadoraId] = useState('');
  const [guia, setGuia] = useState('');
  const [fechaEstimada, setFechaEstimada] = useState('');
  // Arranca en lo pedido, con coma decimal: lo normal es despachar todo, y el
  // ajuste es la excepción. Vacío obligaría a teclearlo en cada despacho.
  const [cantidadDespachada, setCantidadDespachada] = useState(aTexto(solicitada));
  const [cantidadRecibida, setCantidadRecibida] = useState('');
  const [tratamientoFaltante, setTratamientoFaltante] = useState('');
  const [tipoNovedad, setTipoNovedad] = useState(String(TIPO_NOVEDAD.retraso));
  const [tratamientoNovedad, setTratamientoNovedad] = useState(
    String(TRATAMIENTO_NOVEDAD.ninguno),
  );
  const [cantidadAfectada, setCantidadAfectada] = useState('');
  const [novedadElegida, setNovedadElegida] = useState(
    abiertas.length > 0 ? String(abiertas[0].id) : '',
  );
  const [texto, setTexto] = useState('');
  const [validacion, setValidacion] = useState<string | null>(null);

  const transportadoraElegida =
    transportadoras.find((t) => String(t.id) === transportadoraId) ?? null;

  /**
   * Al elegir transportadora se CALCULA la fecha de llegada.
   *
   * Los días salen de la transportadora, no de una constante en esta pantalla:
   * «urgente» es una etiqueta comercial, no un plazo, y el día que una cambie el
   * suyo se corrige en la tabla y no aquí.
   *
   * SE PUEDE CORREGIR A MANO después, y ahora eso pesa más que antes: esta
   * fecha es la que habilita el botón de recibir, así que ponerla muy lejos
   * deja al destino sin poder recibir mercancía que ya tiene delante.
   */
  function elegirTransportadora(valor: string) {
    setTransportadoraId(valor);

    const elegida = transportadoras.find((t) => String(t.id) === valor);
    if (elegida) {
      setFechaEstimada(hoyMas(elegida.diasEntrega));
    }
  }

  /**
   * Lo que el origen NO va a mandar, según lo que se está escribiendo.
   *
   * NO ES UNA PÉRDIDA y el aviso lo dice: esa mercancía no sale, se queda en el
   * estante del origen. Confundirla con el faltante de tránsito sería cargarle
   * a la transportadora algo que nunca subió al camión.
   */
  const sinDespachar = (() => {
    if (accion !== 'despacho' || solicitada === null) {
      return null;
    }
    const escrita = aNumero(cantidadDespachada);
    if (escrita === null || escrita >= solicitada) {
      return null;
    }
    return Math.round((solicitada - escrita) * 10000) / 10000;
  })();

  /**
   * Cuánto falta por llegar según lo que se está escribiendo.
   *
   * SE MIDE CONTRA LO DESPACHADO, no contra lo solicitado: si el origen ajustó
   * el envío a 3 de los 5 pedidos y llegan 3, no falta nada.
   *
   * Se calcula MIENTRAS SE TECLEA y no al enviar: el aviso tiene que aparecer
   * en el momento en que quien descarga escribe una cifra menor, que es cuando
   * todavía puede contar otra vez.
   */
  const faltante = (() => {
    if (accion !== 'recepcion' || despachada === null) {
      return null;
    }
    const recibida = aNumero(cantidadRecibida);
    if (recibida === null || recibida >= despachada) {
      return null;
    }
    return Math.round((despachada - recibida) * 10000) / 10000;
  })();

  function alGuardar() {
    setValidacion(null);

    void enviar(async () => {
      const observaciones = texto.trim() === '' ? null : texto.trim();

      if (accion === 'despacho') {
        if (!transportadoraId || guia.trim() === '') {
          setValidacion('La transportadora y la guía son obligatorias.');
          return;
        }
        // La fecha la pone el sistema al elegir transportadora, así que llegar
        // aquí vacía significa que alguien la borró a mano. La API la exige
        // igual; adelantarlo evita el viaje de ida y vuelta.
        if (fechaEstimada === '') {
          setValidacion(
            'La fecha estimada de llegada es obligatoria: es con lo que se sabe si el traslado ' +
              'va tarde, y es la que habilita la recepción. Elige la transportadora y se ' +
              'calcula sola.',
          );
          return;
        }

        const aDespachar = aNumero(cantidadDespachada);
        if (aDespachar === null || aDespachar <= 0) {
          setValidacion(
            'Indica cuánto se despacha. Si no se puede mandar nada, rechaza el traslado en vez ' +
              'de despachar cero: así queda dicho por qué.',
          );
          return;
        }
        if (solicitada !== null && aDespachar > solicitada) {
          setValidacion(
            `No se puede despachar más de lo pedido (${formatearVolumen(
              solicitada,
              traslado.unidadSimbolo,
            )}). El ajuste sirve para mandar menos.`,
          );
          return;
        }

        await despacharTransferencia(traslado.id, {
          transferenciaId: traslado.id,
          transportadoraId: Number(transportadoraId),
          guia: guia.trim(),
          fechaEstimadaLlegada: fechaEstimada === '' ? null : fechaEstimada,
          cantidadDespachada: aDespachar,
          observaciones,
        });
      } else if (accion === 'recepcion') {
        // EL TRATAMIENTO SE DECIDE AQUÍ, en el mismo paso en que se cuenta lo
        // que llegó. Preguntarlo después, en otra pantalla, es como el faltante
        // se queda sin decidir: quien descarga ya se fue.
        if (faltante !== null && tratamientoFaltante === '') {
          setValidacion(
            'Falta mercancía: hay que decir qué se hace con ella. Reenvío y reclamación dejan ' +
              'el traslado pendiente; asumirla lo cierra como merma.',
          );
          return;
        }

        await recibirTransferencia(traslado.id, {
          transferenciaId: traslado.id,
          cantidadRecibida: aNumero(cantidadRecibida),
          observaciones,
        });

        // LA NOVEDAD VA DESPUÉS DE RECIBIR, y en dos llamadas, no en una.
        //
        // Son dos hechos distintos: cuánto entró al saldo -que lo decide la
        // recepción- y qué se hace con lo que faltó. Si la recepción falla, no
        // hay faltante que anotar; al revés, si falla la anotación, la
        // mercancía ya entró y eso no se deshace.
        //
        // NO MUEVE STOCK, y es deliberado: lo que faltó salió del origen al
        // despachar y nunca entró al destino, así que YA está descontado de la
        // red. Un movimiento de merma volvería a restarlo y el inventario
        // quedaría por debajo de lo que hay en los estantes.
        if (faltante !== null) {
          const tratamiento = Number(tratamientoFaltante) as ValorTratamientoNovedad;

          await registrarNovedad(traslado.id, {
            transferenciaId: traslado.id,
            tipo: TIPO_NOVEDAD.faltante,
            cantidadAfectada: faltante,
            tratamiento,
            observaciones: notaDelFaltante(
              tratamiento,
              traslado.transportadoraNombre,
              observaciones,
            ),
          });
        }
      } else if (accion === 'novedad') {
        await registrarNovedad(traslado.id, {
          transferenciaId: traslado.id,
          // Número, no texto: la API espera el valor ordinal del enum.
          tipo: Number(tipoNovedad) as ValorTipoNovedad,
          // `aNumero` convierte la coma en punto: Number('0,5') es NaN.
          cantidadAfectada: aNumero(cantidadAfectada),
          tratamiento: Number(tratamientoNovedad) as ValorTratamientoNovedad,
          observaciones,
        });
      } else if (accion === 'cierreNovedad') {
        if (novedadElegida === '') {
          setValidacion('No hay ninguna novedad pendiente que cerrar en este traslado.');
          return;
        }
        if (observaciones === null) {
          setValidacion(
            'Hay que decir POR QUÉ se cierra: si llegó lo que faltaba, si lo respondió la ' +
              'transportadora o si se da por perdido. Es lo único que queda para revisarlo ' +
              'después.',
          );
          return;
        }
        await cerrarNovedad(traslado.id, Number(novedadElegida), { motivo: observaciones });
      } else if (accion === 'rechazo') {
        await rechazarTransferencia(traslado.id, { motivo: observaciones });
      } else {
        await cancelarTransferencia(traslado.id, { motivo: observaciones });
      }

      onHecho();
      onCerrar();
    });
  }

  return (
    <Modal
      titulo={`${TITULOS[accion]} ${traslado.id}`}
      descripcion={DESCRIPCIONES[accion]}
      ocupado={enviando}
      onCerrar={onCerrar}
      pie={
        <>
          <Boton variante="secundaria" onClick={onCerrar} disabled={enviando}>
            Cancelar
          </Boton>
          <Boton onClick={alGuardar} disabled={enviando}>
            {enviando ? (
              <>
                <Spinner etiqueta="Enviando" />
                Enviando…
              </>
            ) : (
              ETIQUETA_ENVIO[accion]
            )}
          </Boton>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        {error ? <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta> : null}
        {validacion ? <Alerta tipo="info">{validacion}</Alerta> : null}

        <div className="rounded-xl bg-slate-50 px-4 py-3 text-sm text-slate-600">
          <span className="font-semibold text-slate-900">{traslado.productoNombre}</span> ·{' '}
          {traslado.sucursalOrigenNombre} → {traslado.sucursalDestinoNombre}
          <span className="mt-1 block tabular-nums">
            Pedido: {formatearVolumen(solicitada, traslado.unidadSimbolo)}
            {/* Las dos cifras juntas cuando difieren: quien recibe tiene que
                saber contra qué está contando, y no es contra lo que se pidió. */}
            {traslado.cantidadDespachada !== null &&
            solicitada !== null &&
            traslado.cantidadDespachada < solicitada ? (
              <>
                {' · '}
                <span className="font-semibold text-terracota-700">
                  Despachado: {formatearVolumen(traslado.cantidadDespachada, traslado.unidadSimbolo)}
                </span>
              </>
            ) : null}
            {traslado.fechaEstimadaLlegada === null ? null : (
              <> · Llegada prevista: {formatearFechaSolo(traslado.fechaEstimadaLlegada)}</>
            )}
          </span>

          {/*
            LOS LOTES QUE VIAJAN. Aquí valen más que en la tabla: quien abre
            este panel tiene el camión delante, y lo que necesita es cotejar el
            número impreso en el envase contra lo que dice el papel. Con la
            cantidad por lote además se sabe cuánto debería traer cada uno.

            Vacío mientras está Solicitada -no ha salido nada- así que no se
            pinta el bloque.
          */}
          {traslado.lotes.length === 0 ? null : (
            <span className="mt-2 block border-t border-slate-200 pt-2">
              <span className="text-[11px] font-semibold uppercase tracking-wider text-slate-500">
                {traslado.lotes.length === 1 ? 'Lote que viaja' : 'Lotes que viajan'}
              </span>
              <span className="mt-1 flex flex-wrap gap-1.5">
                {traslado.lotes.map((l, i) => (
                  <span
                    key={l.loteId ?? `sin-lote-${i}`}
                    className={`inline-flex items-center gap-1.5 whitespace-nowrap rounded px-2 py-0.5 text-xs ${
                      l.numeroLote === null
                        ? 'bg-amber-50 text-amber-800 ring-1 ring-inset ring-amber-200'
                        : 'bg-white text-slate-700 ring-1 ring-inset ring-slate-200'
                    }`}
                  >
                    <span className="font-mono font-semibold">
                      {l.numeroLote ?? 'sin lote'}
                    </span>
                    {/* En unidad BASE del producto, no en la del traslado: así
                        lo guarda el libro mayor. Un traslado de 5 galones mueve
                        18,93 litros de lote. */}
                    <span className="tabular-nums text-slate-500">
                      {formatearVolumen(l.cantidadBase, traslado.unidadBaseSimbolo)}
                    </span>
                    {l.fechaVencimiento === null ? null : (
                      <span className="text-slate-400">
                        vence {formatearFechaSolo(l.fechaVencimiento)}
                      </span>
                    )}
                  </span>
                ))}
              </span>
              {/* El destino recrea los MISMOS números, y hay que decirlo: si no,
                  quien recibe podría pensar que tiene que inventar lotes
                  nuevos, y se perdería la trazabilidad del fabricante. */}
              <span className="mt-1 block text-xs text-slate-500">
                La sede destino los recrea con el mismo número y la misma
                caducidad: la trazabilidad del fabricante no se corta en el
                traslado.
              </span>
            </span>
          )}
        </div>

        {accion === 'despacho' ? (
          <>
            {/*
              EL AJUSTE VA PRIMERO, antes de la transportadora: es la pregunta
              que se hace mirando el estante -«¿cuánto tengo?»- y pasa antes que
              llamar al transportador.
            */}
            <CampoNumero
              etiqueta={`Cantidad a despachar (${traslado.unidadSimbolo})`}
              valor={cantidadDespachada}
              onCambio={setCantidadDespachada}
              requerido
              disabled={enviando}
              ayuda={`Pedido: ${formatearVolumen(solicitada, traslado.unidadSimbolo)}. Bájala si no hay todo; no se puede subir.`}
            />

            {sinDespachar !== null ? (
              <Alerta tipo="info">
                Se despacharán{' '}
                <strong className="font-semibold">
                  {formatearVolumen(sinDespachar, traslado.unidadSimbolo)} menos
                </strong>{' '}
                de lo pedido. Eso NO es un faltante ni una merma: esa mercancía no sale, se queda
                en {traslado.sucursalOrigenNombre}. Si {traslado.sucursalDestinoNombre} la sigue
                necesitando, hay que pedirla en otro traslado.
              </Alerta>
            ) : null}

            <CampoSelect
              etiqueta="Transportadora"
              valor={transportadoraId}
              onCambio={elegirTransportadora}
              opciones={transportadoras.map((t) => ({
                valor: String(t.id),
                texto: `${t.nombre} — ${t.tipoServicio}, ${t.diasEntrega} ${
                  t.diasEntrega === 1 ? 'día' : 'días'
                }`,
              }))}
              placeholder="Selecciona una"
              requerido
              disabled={enviando}
            />
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <CampoTexto
                etiqueta="Guía"
                valor={guia}
                onCambio={setGuia}
                requerido
                disabled={enviando}
                placeholder="Número de guía"
              />
              <CampoTexto
                etiqueta="Llegada estimada"
                tipo="date"
                valor={fechaEstimada}
                onCambio={setFechaEstimada}
                requerido
                disabled={enviando}
                ayuda={
                  transportadoraElegida === null
                    ? 'La calcula la transportadora que elijas.'
                    : // Punto medio y no punto final antes del nombre: muchas
                      // razones sociales acaban en punto -«Redetrans S.A.»- y
                      // encadenarlo con una frase daba «S.A..».
                      `${transportadoraElegida.diasEntrega} ${
                        transportadoraElegida.diasEntrega === 1 ? 'día' : 'días'
                      } de ${transportadoraElegida.nombre} · habilita la recepción ese día`
                }
              />
            </div>
          </>
        ) : null}

        {accion === 'recepcion' ? (
          <>
            <CampoNumero
              etiqueta={`Cantidad recibida (${traslado.unidadSimbolo})`}
              valor={cantidadRecibida}
              onCambio={setCantidadRecibida}
              disabled={enviando}
              placeholder={aTexto(despachada)}
              ayuda={`Vacía recibe todo lo despachado (${formatearVolumen(despachada, traslado.unidadSimbolo)}). Cuenta antes de escribir: esta cifra es la que entra al saldo.`}
            />

            {/*
              Solo cuando de verdad falta algo. Un recuadro permanente que
              pregunta por el faltante en cada recepción se acaba ignorando, y
              entonces no avisa el día que importa.
            */}
            {faltante !== null ? (
              <div className="flex flex-col gap-3 rounded-xl border border-terracota-300 bg-terracota-50 px-4 py-3">
                <p className="flex items-center gap-2 text-sm font-semibold text-terracota-800">
                  <AlertTriangle size={16} aria-hidden="true" />
                  Faltan {formatearVolumen(faltante, traslado.unidadSimbolo)} de los{' '}
                  {formatearVolumen(despachada, traslado.unidadSimbolo)} despachados
                </p>

                <CampoSelect
                  etiqueta="Qué se hace con el faltante"
                  valor={tratamientoFaltante}
                  onCambio={setTratamientoFaltante}
                  disabled={enviando}
                  requerido
                  placeholder="Elige el tratamiento"
                  opciones={[
                    {
                      valor: String(TRATAMIENTO_NOVEDAD.reclamacion),
                      texto: `Reclamar${
                        traslado.transportadoraNombre === null
                          ? ' a la transportadora'
                          : ` a ${traslado.transportadoraNombre}`
                      } — queda pendiente`,
                    },
                    {
                      valor: String(TRATAMIENTO_NOVEDAD.reenvio),
                      texto: `Pedir reenvío a ${traslado.sucursalOrigenNombre} — queda pendiente`,
                    },
                    {
                      valor: String(TRATAMIENTO_NOVEDAD.asumido),
                      texto: 'Darlo por perdido (merma) — cierra el traslado',
                    },
                  ]}
                  ayuda="Reenvío y reclamación dejan el traslado en «por recibir» hasta que se resuelva."
                />

                {/*
                  Hay que decir que NO vuelve a descontar. Quien lo lee espera
                  que "merma" baje el inventario, y aquí no: lo que faltó salió
                  del origen al despachar y nunca entró al destino, así que ya
                  está descontado de la red. Restarlo otra vez dejaría el sistema
                  con menos producto del que hay en los estantes.
                */}
                <p className="text-xs text-slate-600">
                  Ninguna de las tres vuelve a descontar stock: esa cantidad salió del origen al
                  despachar y nunca llegó, así que ya está fuera del inventario de la red. Queda
                  anotada con la transportadora y la guía para poder reclamar.
                </p>
              </div>
            ) : null}
          </>
        ) : null}

        {accion === 'novedad' ? (
          <>
            <CampoSelect
              etiqueta="Tipo"
              valor={tipoNovedad}
              onCambio={setTipoNovedad}
              disabled={enviando}
              opciones={[
                { valor: String(TIPO_NOVEDAD.retraso), texto: 'Retraso' },
                { valor: String(TIPO_NOVEDAD.sobrante), texto: 'Sobrante' },
                { valor: String(TIPO_NOVEDAD.faltante), texto: 'Faltante' },
                { valor: String(TIPO_NOVEDAD.averia), texto: 'Avería' },
              ]}
            />

            {/*
              Decimal con coma y sin signos: ver CampoNumero. Un «-1» aquí no
              significa nada, y con el campo numérico del navegador se podía
              escribir -y hasta «1e5»-.
            */}
            <CampoNumero
              etiqueta="Cantidad afectada"
              valor={cantidadAfectada}
              onCambio={setCantidadAfectada}
              disabled={enviando}
              placeholder="0,5"
              ayuda="Opcional. Solo números, con coma decimal."
            />

            <CampoSelect
              etiqueta="Tratamiento"
              valor={tratamientoNovedad}
              onCambio={setTratamientoNovedad}
              disabled={enviando}
              opciones={[
                {
                  valor: String(TRATAMIENTO_NOVEDAD.ninguno),
                  texto: 'Solo dejar constancia — no deja pendiente',
                },
                {
                  valor: String(TRATAMIENTO_NOVEDAD.reclamacion),
                  texto: 'Reclamación a la transportadora — queda pendiente',
                },
                {
                  valor: String(TRATAMIENTO_NOVEDAD.reenvio),
                  texto: 'Reenvío desde el origen — queda pendiente',
                },
                {
                  valor: String(TRATAMIENTO_NOVEDAD.asumido),
                  texto: 'Asumido como merma — cierra el traslado',
                },
              ]}
              ayuda="Las dos del medio mantienen el traslado en «por recibir» hasta que se cierren."
            />
          </>
        ) : null}

        {accion === 'cierreNovedad' ? (
          abiertas.length === 0 ? (
            <Alerta tipo="info">
              Este traslado no tiene novedades pendientes. Puede que otra persona ya la haya
              cerrado; recarga la lista para verlo.
            </Alerta>
          ) : (
            <>
              {/*
                El selector aparece siempre, incluso con una sola: con una, deja
                a la vista QUÉ se está cerrando -el tipo, la cantidad- en vez de
                pedir un motivo para algo que no se nombra.
              */}
              <CampoSelect
                etiqueta="Novedad pendiente"
                valor={novedadElegida}
                onCambio={setNovedadElegida}
                disabled={enviando || abiertas.length === 1}
                opciones={abiertas.map((n) => ({
                  valor: String(n.id),
                  texto: `${n.tipo === null ? 'Novedad' : ETIQUETA_TIPO_NOVEDAD[n.tipo]}${
                    n.cantidadAfectada === null
                      ? ''
                      : ` de ${formatearVolumen(n.cantidadAfectada, traslado.unidadSimbolo)}`
                  } — ${ETIQUETA_TRATAMIENTO[n.tratamiento]} · ${formatearFechaSolo(n.fecha)}`,
                }))}
              />

              <Alerta tipo="info">
                Cerrarla <strong className="font-semibold">no borra nada</strong>: el reporte
                original conserva su tipo, su cantidad, quién lo firmó y cuándo. Lo que se añade
                es el desenlace. Si es la última pendiente, el traslado sale de «por recibir».
              </Alerta>
            </>
          )
        ) : null}

        <CampoArea
          etiqueta={
            accion === 'rechazo' || accion === 'cancelacion'
              ? 'Motivo'
              : accion === 'cierreNovedad'
                ? 'Por qué se cierra'
                : 'Observaciones'
          }
          valor={texto}
          onCambio={setTexto}
          disabled={enviando}
          placeholder={
            accion === 'rechazo'
              ? 'Por qué no se atiende'
              : accion === 'cierreNovedad'
                ? 'Llegó el reenvío completo / la transportadora abonó el faltante / se da por perdido…'
                : 'Opcional'
          }
          ayuda={
            accion === 'cierreNovedad'
              ? 'Obligatorio. Es lo único que queda para entender, dentro de seis meses, qué pasó con esa mercancía.'
              : undefined
          }
        />
      </div>
    </Modal>
  );
}
