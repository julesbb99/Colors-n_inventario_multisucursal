import { useState } from 'react';
import { AlertTriangle } from 'lucide-react';
import { Modal } from '../ui/Modal';
import { Boton } from '../ui/Boton';
import { Alerta } from '../ui/Alerta';
import { Spinner } from '../ui/Spinner';
import { CampoArea, CampoNumero, CampoSelect, CampoTexto } from '../ui/Campos';
import { useConsulta } from '../../hooks/useConsulta';
import { useEnvio } from '../../hooks/useEnvio';
import {
  cancelarTransferencia,
  despacharTransferencia,
  obtenerTransportadoras,
  recibirTransferencia,
  rechazarTransferencia,
  registrarNovedad,
} from '../../services/transferencias';
import { TIPO_NOVEDAD } from '../../models/transferencias';
import type { TransferenciaDto, ValorTipoNovedad } from '../../models/transferencias';
import type { TransportadoraDto } from '../../models/transferencias';
import { formatearVolumen } from '../../utils/formato';

export type AccionTraslado = 'despacho' | 'recepcion' | 'novedad' | 'rechazo' | 'cancelacion';

const SIN_TRANSPORTADORAS: TransportadoraDto[] = [];

const TITULOS: Record<AccionTraslado, string> = {
  despacho: 'Despachar traslado',
  recepcion: 'Recibir traslado',
  // El botón que abre esto dice «Recibir», así que el título tiene que decir lo
  // mismo: un botón que promete una cosa y abre otra titulada distinto hace
  // dudar de si se pulsó lo que se quería.
  novedad: 'Recibir traslado',
  rechazo: 'Rechazar traslado',
  cancelacion: 'Cancelar traslado',
};

const DESCRIPCIONES: Record<AccionTraslado, string> = {
  despacho: 'Lo hace la sede origen. Descuenta stock por FEFO y el traslado pasa a En tránsito.',
  recepcion: 'Lo hace la sede destino. Sin cantidad, llega todo lo despachado.',
  novedad:
    'Ya entró lo que llegó y el traslado quedó corto. Aquí se deja constancia de qué pasó con ' +
    'el faltante. NO mueve stock: esa cantidad ya salió del inventario de la red.',
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
 * El texto que acompaña al faltante anotado como merma.
 *
 * Nombra a la transportadora a propósito: el faltante es, antes que una merma,
 * un reclamo contra quien lo transportó. Si se resuelve con ella, la mercancía
 * aparece y esto queda como el historial de lo que pasó; si no, esta anotación
 * ES la merma. Sin el nombre, dentro de un mes nadie sabe a quién reclamarle.
 */
function ConstruirNotaMerma(
  transportadora: string | null,
  observaciones: string | null,
): string {
  const partes = [
    transportadora === null
      ? 'Faltante en traslado, sin transportadora registrada.'
      : `Faltante en traslado con ${transportadora}. Reclamar antes de darlo por perdido.`,
  ];

  if (observaciones !== null) {
    partes.push(observaciones);
  }

  return partes.join(' ');
}

interface AccionesTrasladoProps {
  traslado: TransferenciaDto;
  accion: AccionTraslado;
  onCerrar: () => void;
  onHecho: () => void;
}

/**
 * Las cinco acciones sobre un traslado ya creado.
 *
 * Van en un componente y no en cinco porque comparten casi todo -el modal, el
 * estado de envío, el pie- y se diferencian en dos o tres campos. Cinco archivos
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

  const [transportadoraId, setTransportadoraId] = useState('');
  const [guia, setGuia] = useState('');
  const [fechaEstimada, setFechaEstimada] = useState('');
  const [cantidadRecibida, setCantidadRecibida] = useState('');
  const [tipoNovedad, setTipoNovedad] = useState(String(TIPO_NOVEDAD.retraso));
  const [cantidadAfectada, setCantidadAfectada] = useState('');
  const [texto, setTexto] = useState('');
  const [registrarMerma, setRegistrarMerma] = useState(true);
  const [validacion, setValidacion] = useState<string | null>(null);

  const transportadoraElegida =
    transportadoras.find((t) => String(t.id) === transportadoraId) ?? null;

  /**
   * Al elegir transportadora se CALCULA la fecha de llegada.
   *
   * Antes el campo estaba ahí, vacío y marcado como opcional, y lo normal era
   * dejarlo así: un traslado en tránsito sin fecha no se puede reclamar, porque
   * no hay a partir de cuándo decir que va tarde.
   *
   * Los días salen de la transportadora, no de una constante en esta pantalla:
   * «urgente» es una etiqueta comercial, no un plazo, y el día que una cambie el
   * suyo se corrige en la tabla y no aquí.
   *
   * SE PUEDE CORREGIR A MANO después. El cálculo es el punto de partida
   * razonable, no una imposición: si se pactó otra fecha con el transportador,
   * manda esa.
   */
  function elegirTransportadora(valor: string) {
    setTransportadoraId(valor);

    const elegida = transportadoras.find((t) => String(t.id) === valor);
    if (elegida) {
      setFechaEstimada(hoyMas(elegida.diasEntrega));
    }
  }

  /**
   * Cuánto falta por llegar según lo que se está escribiendo.
   *
   * Se calcula MIENTRAS SE TECLEA y no al enviar: el aviso de merma tiene que
   * aparecer en el momento en que quien descarga escribe una cifra menor, que es
   * cuando todavía puede contar otra vez.
   *
   * Nulo cuando el campo está vacío -sin cantidad se recibe todo- o cuando lo
   * escrito no es un número menor que lo solicitado.
   */
  const faltante = (() => {
    if (accion !== 'recepcion' || cantidadRecibida.trim() === '') {
      return null;
    }
    const recibida = Number(cantidadRecibida);
    const solicitada = traslado.cantidadSolicitada;
    if (!Number.isFinite(recibida) || solicitada === null || recibida >= solicitada) {
      return null;
    }
    // A 4 decimales, que es la escala de `transferencias.cantidad_solicitada`.
    return Math.round((solicitada - recibida) * 10000) / 10000;
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
              'va tarde. Elige la transportadora y se calcula sola.',
          );
          return;
        }
        await despacharTransferencia(traslado.id, {
          transferenciaId: traslado.id,
          transportadoraId: Number(transportadoraId),
          guia: guia.trim(),
          fechaEstimadaLlegada: fechaEstimada === '' ? null : fechaEstimada,
          observaciones,
        });
      } else if (accion === 'recepcion') {
        await recibirTransferencia(traslado.id, {
          transferenciaId: traslado.id,
          cantidadRecibida: cantidadRecibida === '' ? null : Number(cantidadRecibida),
          observaciones,
        });

        // LA MERMA VA DESPUES DE RECIBIR, y en dos llamadas, no en una.
        //
        // Son dos hechos distintos: cuánto entró al saldo -que lo decide la
        // recepción- y cuánto se perdió en el camino -que es el faltante-. Si
        // la recepción falla, no hay faltante que anotar; al revés, si falla la
        // anotación, la mercancía ya entró y eso no se deshace.
        //
        // NO MUEVE STOCK, y es deliberado: lo que faltó salió del origen al
        // despachar y nunca entró al destino, así que YA está descontado de la
        // red. Un movimiento de merma volvería a restarlo y el inventario
        // quedaría por debajo de lo que hay en los estantes.
        if (registrarMerma && faltante !== null) {
          await registrarNovedad(traslado.id, {
            transferenciaId: traslado.id,
            tipo: TIPO_NOVEDAD.faltante,
            cantidadAfectada: faltante,
            observaciones: ConstruirNotaMerma(traslado.transportadoraNombre, observaciones),
          });
        }
      } else if (accion === 'novedad') {
        await registrarNovedad(traslado.id, {
          transferenciaId: traslado.id,
          // Número, no texto: la API espera el valor ordinal del enum.
          tipo: Number(tipoNovedad) as ValorTipoNovedad,
          cantidadAfectada: cantidadAfectada === '' ? null : Number(cantidadAfectada),
          observaciones,
        });
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
              TITULOS[accion]
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
          {traslado.sucursalOrigenNombre} → {traslado.sucursalDestinoNombre} ·{' '}
          <span className="tabular-nums">
            {formatearVolumen(traslado.cantidadSolicitada, traslado.unidadSimbolo)}
          </span>
        </div>

        {accion === 'despacho' ? (
          <>
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
                      } de ${transportadoraElegida.nombre} · cámbiala si pactaste otra fecha`
                }
              />
            </div>
          </>
        ) : null}

        {accion === 'recepcion' ? (
          <>
            <CampoNumero
              etiqueta="Cantidad recibida"
              valor={cantidadRecibida}
              onCambio={setCantidadRecibida}
              disabled={enviando}
              ayuda="Vacía recibe todo lo despachado. Cuenta antes de escribir: esta cifra es la que entra al saldo."
            />

            {/*
              Solo cuando de verdad falta algo. Un recuadro permanente que
              pregunta por la merma en cada recepción se acaba ignorando, y
              entonces no avisa el día que importa.
            */}
            {faltante !== null ? (
              <div className="flex flex-col gap-2 rounded-xl border border-terracota-300 bg-terracota-50 px-4 py-3">
                <p className="flex items-center gap-2 text-sm font-semibold text-terracota-800">
                  <AlertTriangle size={16} aria-hidden="true" />
                  Faltan {formatearVolumen(faltante, traslado.unidadSimbolo)}
                </p>

                <label className="flex items-start gap-2 text-sm text-slate-700">
                  <input
                    type="checkbox"
                    checked={registrarMerma}
                    onChange={(evento) => setRegistrarMerma(evento.target.checked)}
                    disabled={enviando}
                    className="mt-0.5 h-4 w-4 shrink-0 accent-terracota-600"
                  />
                  <span>
                    Anotar el faltante como <strong className="font-semibold">merma</strong>
                    {traslado.transportadoraNombre === null
                      ? ''
                      : `, para reclamárselo a ${traslado.transportadoraNombre}`}
                    .
                  </span>
                </label>

                {/*
                  Hay que decir que NO vuelve a descontar. Quien lo lee espera
                  que "merma" baje el inventario, y aquí no: lo que faltó salió
                  del origen al despachar y nunca entró al destino, así que ya
                  está descontado de la red. Restarlo otra vez dejaría el sistema
                  con menos producto del que hay en los estantes.
                */}
                <p className="text-xs text-slate-600">
                  No vuelve a descontar stock: esa cantidad salió del origen al despachar y nunca
                  llegó, así que ya está fuera del inventario de la red. Queda anotada con la
                  transportadora para poder reclamar, y si no se recupera, es la merma.
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

            <CampoNumero
              etiqueta="Cantidad afectada"
              valor={cantidadAfectada}
              onCambio={setCantidadAfectada}
              disabled={enviando}
              ayuda="Opcional."
            />
          </>
        ) : null}

        <CampoArea
          etiqueta={accion === 'rechazo' || accion === 'cancelacion' ? 'Motivo' : 'Observaciones'}
          valor={texto}
          onCambio={setTexto}
          disabled={enviando}
          placeholder={accion === 'rechazo' ? 'Por qué no se atiende' : 'Opcional'}
        />
      </div>
    </Modal>
  );
}
