import { useState } from 'react';
import { Modal } from '../ui/Modal';
import { Boton } from '../ui/Boton';
import { Alerta } from '../ui/Alerta';
import { Spinner } from '../ui/Spinner';
import { CampoArea, CampoNumero, CampoSelect, CampoTexto } from '../ui/Campos';
import { useAuth } from '../../hooks/useAuth';
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
import { NOVEDADES_CRITICAS, TIPO_NOVEDAD } from '../../models/transferencias';
import type { TransferenciaDto, ValorTipoNovedad } from '../../models/transferencias';
import type { TransportadoraDto } from '../../models/transferencias';
import { ROLES_SUPERVISION } from '../../models/auth';
import { formatearVolumen } from '../../utils/formato';

export type AccionTraslado = 'despacho' | 'recepcion' | 'novedad' | 'rechazo' | 'cancelacion';

const SIN_TRANSPORTADORAS: TransportadoraDto[] = [];

const TITULOS: Record<AccionTraslado, string> = {
  despacho: 'Despachar traslado',
  recepcion: 'Recibir traslado',
  novedad: 'Reportar novedad',
  rechazo: 'Rechazar traslado',
  cancelacion: 'Cancelar traslado',
};

const DESCRIPCIONES: Record<AccionTraslado, string> = {
  despacho: 'Lo hace la sede origen. Descuenta stock por FEFO y el traslado pasa a En tránsito.',
  recepcion: 'Lo hace la sede destino. Sin cantidad, llega todo lo despachado.',
  novedad: 'No mueve stock: deja constancia de lo que pasó en el camino.',
  rechazo: 'Solo el origen y solo desde Solicitada. La mercancía no sale.',
  cancelacion: 'Cualquiera de las dos sedes, solo desde Solicitada.',
};

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
  const { rol } = useAuth();
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
  const [validacion, setValidacion] = useState<string | null>(null);

  const esSupervision = rol !== null && ROLES_SUPERVISION.includes(rol);
  const novedadEsCritica = NOVEDADES_CRITICAS.includes(Number(tipoNovedad) as ValorTipoNovedad);

  function alGuardar() {
    setValidacion(null);

    void enviar(async () => {
      const observaciones = texto.trim() === '' ? null : texto.trim();

      if (accion === 'despacho') {
        if (!transportadoraId || guia.trim() === '') {
          setValidacion('La transportadora y la guía son obligatorias.');
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
              onCambio={setTransportadoraId}
              opciones={transportadoras.map((t) => ({
                valor: String(t.id),
                texto: `${t.nombre} — ${t.tipoServicio}`,
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
                disabled={enviando}
                ayuda="Opcional."
              />
            </div>
          </>
        ) : null}

        {accion === 'recepcion' ? (
          <CampoNumero
            etiqueta="Cantidad recibida"
            valor={cantidadRecibida}
            onCambio={setCantidadRecibida}
            disabled={enviando}
            ayuda="Vacía recibe todo lo despachado. Menos de lo enviado deja el traslado como Recibida parcial."
          />
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
                { valor: String(TIPO_NOVEDAD.faltante), texto: 'Faltante (crítica)' },
                { valor: String(TIPO_NOVEDAD.averia), texto: 'Avería (crítica)' },
              ]}
            />

            {/*
              Aviso, no bloqueo. Quien decide es la API, que responde 403 a un
              operador que intente registrar una crítica; adelantarlo aquí evita
              que escriba el reporte entero para descubrirlo al enviar.
            */}
            {novedadEsCritica && !esSupervision ? (
              <Alerta tipo="permisos">
                Faltante y avería solo las registra supervisión. Pídele a tu gerente que la
                reporte, o deja constancia como retraso y coméntalo.
              </Alerta>
            ) : null}

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
