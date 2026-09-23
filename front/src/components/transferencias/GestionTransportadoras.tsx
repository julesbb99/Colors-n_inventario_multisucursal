import { useState } from 'react';
import { Pencil, Plus, RotateCcw, Truck, XCircle } from 'lucide-react';
import { Modal } from '../ui/Modal';
import { Boton } from '../ui/Boton';
import { Alerta } from '../ui/Alerta';
import { Spinner } from '../ui/Spinner';
import { aNumero, CampoNumero, CampoSelect, CampoTexto } from '../ui/Campos';
import { StatusBadge } from '../ui/StatusBadge';
import { useConsulta } from '../../hooks/useConsulta';
import { useEnvio } from '../../hooks/useEnvio';
import {
  actualizarTransportadora,
  crearTransportadora,
  obtenerTransportadoras,
  reactivarTransportadora,
  retirarTransportadora,
} from '../../services/transferencias';
import type { GuardarTransportadoraDto, TransportadoraDto } from '../../models/transferencias';

const SIN_TRANSPORTADORAS: TransportadoraDto[] = [];

const TIPOS = [
  { valor: 'urgente', texto: 'Urgente' },
  { valor: 'estandar', texto: 'Estándar' },
];

interface GestionTransportadorasProps {
  onCerrar: () => void;
  /** Se llama tras guardar, para que el formulario de despacho recoja el cambio. */
  onGuardado: () => void;
}

/**
 * Catálogo de transportadoras. ADMINISTRACIÓN GENERAL Y GERENCIA DE SEDE.
 *
 * ES UNA DIFERENCIA DELIBERADA CON PROVEEDORES, que sí está reservado al
 * Administrador General: allí cuelgan los precios de compra, y retocar uno mueve
 * el criterio con el que compran las tres sedes. Una transportadora no lleva
 * precios; es un contacto de logística, y quien negocia el flete de su sede es
 * quien sabe con quién se trabaja.
 *
 * LOS DÍAS SON EL DATO QUE IMPORTA. «Urgente» y «estándar» son etiquetas que
 * sirven para agrupar en el selector; la fecha estimada de llegada la calcula el
 * despacho sumando estos días. Dos empresas urgentes pueden tardar 1 y 2.
 */
export function GestionTransportadoras({
  onCerrar,
  onGuardado,
}: GestionTransportadorasProps) {
  const { enviando, error, esPermisos, enviar } = useEnvio();

  // CON las retiradas: sin ellas la baja sería irreversible desde la interfaz,
  // porque no habría dónde pulsar para devolverlas. El selector del despacho
  // sigue pidiendo solo las activas.
  const {
    datos: transportadoras,
    cargando,
    recargar,
  } = useConsulta(() => obtenerTransportadoras(true), [], SIN_TRANSPORTADORAS);

  // `null` = nadie en edición. Un id = esa fila. `'nueva'` = el alta.
  const [editando, setEditando] = useState<number | 'nueva' | null>(null);

  const [nombre, setNombre] = useState('');
  const [tipoServicio, setTipoServicio] = useState('estandar');
  const [dias, setDias] = useState('3');
  const [validacion, setValidacion] = useState<string | null>(null);

  function abrirAlta() {
    setEditando('nueva');
    setNombre('');
    setTipoServicio('estandar');
    setDias('3');
    setValidacion(null);
  }

  function abrirEdicion(transportadora: TransportadoraDto) {
    setEditando(transportadora.id);
    setNombre(transportadora.nombre);
    setTipoServicio(transportadora.tipoServicio);
    setDias(String(transportadora.diasEntrega));
    setValidacion(null);
  }

  function alGuardar() {
    setValidacion(null);

    if (nombre.trim() === '') {
      setValidacion('La transportadora necesita nombre.');
      return;
    }

    const diasEntrega = aNumero(dias);
    if (diasEntrega === null || !Number.isInteger(diasEntrega) || diasEntrega < 1) {
      setValidacion(
        'El plazo debe ser un número entero de al menos 1 día. Un traslado que llega el mismo ' +
          'día que sale no necesita transportadora.',
      );
      return;
    }

    const peticion: GuardarTransportadoraDto = {
      nombre: nombre.trim(),
      tipoServicio: tipoServicio === 'urgente' ? 'urgente' : 'estandar',
      diasEntrega,
    };

    void enviar(async () => {
      if (editando === 'nueva') {
        await crearTransportadora(peticion);
      } else if (typeof editando === 'number') {
        await actualizarTransportadora(editando, peticion);
      }
      setEditando(null);
      recargar();
      onGuardado();
    });
  }

  function cambiarEstado(transportadora: TransportadoraDto) {
    setValidacion(null);

    void enviar(async () => {
      if (transportadora.activo) {
        await retirarTransportadora(transportadora.id);
      } else {
        await reactivarTransportadora(transportadora.id);
      }
      recargar();
      onGuardado();
    });
  }

  return (
    <Modal
      titulo="Transportadoras"
      descripcion="Son de toda la red: las tres sedes eligen de esta misma lista al despachar."
      ancho="xl"
      ocupado={enviando}
      onCerrar={onCerrar}
      pie={
        <>
          <span className="mr-auto text-xs text-slate-500">
            Los días son lo que calcula la fecha de llegada al despachar.
          </span>
          <Boton variante="secundaria" onClick={onCerrar} disabled={enviando}>
            Cerrar
          </Boton>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        {error ? <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta> : null}
        {validacion ? <Alerta tipo="info">{validacion}</Alerta> : null}

        <div className="flex items-center gap-3">
          <h3 className="text-sm font-bold text-slate-800">
            {cargando
              ? 'Cargando…'
              : (() => {
                  const activas = transportadoras.filter((t) => t.activo).length;
                  const retiradas = transportadoras.length - activas;
                  return retiradas === 0
                    ? `${activas} en el catálogo`
                    : `${activas} en el catálogo · ${retiradas} retirada${
                        retiradas === 1 ? '' : 's'
                      }`;
                })()}
          </h3>
          <div className="h-px flex-1 bg-slate-200" />
          <Boton
            variante="secundaria"
            onClick={abrirAlta}
            disabled={enviando || editando === 'nueva'}
            className="!h-9 !px-3 text-xs"
          >
            <Plus size={15} aria-hidden="true" />
            Nueva transportadora
          </Boton>
        </div>

        {/*
          El formulario se abre en el sitio, encima de la lista, en vez de en
          otro modal: dos modales apilados para tres campos esconden la lista
          contra la que se está comparando.
        */}
        {editando !== null ? (
          <div className="flex flex-col gap-3 rounded-xl border border-petroleo-200 bg-petroleo-50/50 p-4">
            <p className="text-sm font-semibold text-slate-800">
              {editando === 'nueva' ? 'Nueva transportadora' : 'Editando'}
            </p>

            <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
              <div className="sm:col-span-1">
                <CampoTexto
                  etiqueta="Nombre"
                  valor={nombre}
                  onCambio={setNombre}
                  requerido
                  disabled={enviando}
                  placeholder="Transportes del Valle S.A.S."
                />
              </div>
              <CampoSelect
                etiqueta="Tipo de servicio"
                valor={tipoServicio}
                onCambio={setTipoServicio}
                opciones={TIPOS}
                requerido
                disabled={enviando}
                ayuda="Solo agrupa en el selector."
              />
              <CampoNumero
                etiqueta="Días de entrega"
                valor={dias}
                onCambio={setDias}
                entero
                requerido
                disabled={enviando}
                ayuda="Entre sedes. Es lo que fija la fecha."
              />
            </div>

            <div className="flex justify-end gap-2">
              <Boton
                variante="secundaria"
                onClick={() => setEditando(null)}
                disabled={enviando}
                className="!h-9 !px-3 text-xs"
              >
                Cancelar
              </Boton>
              <Boton onClick={alGuardar} disabled={enviando} className="!h-9 !px-3 text-xs">
                {enviando ? (
                  <>
                    <Spinner etiqueta="Guardando" />
                    Guardando…
                  </>
                ) : (
                  'Guardar'
                )}
              </Boton>
            </div>
          </div>
        ) : null}

        <ul className="flex flex-col gap-2">
          {transportadoras.map((transportadora) => (
            <li
              key={transportadora.id}
              className={`flex flex-wrap items-center gap-3 rounded-lg px-3 py-2.5 ${
                transportadora.activo ? 'bg-slate-50' : 'bg-slate-100/70'
              }`}
            >
              <Truck
                size={17}
                className={`shrink-0 ${
                  transportadora.activo ? 'text-slate-400' : 'text-slate-300'
                }`}
                aria-hidden="true"
              />

              <span
                className={`min-w-0 flex-1 text-sm font-medium ${
                  transportadora.activo ? 'text-slate-900' : 'text-slate-400'
                }`}
              >
                {transportadora.nombre}
              </span>

              {transportadora.activo ? (
                <>
                  <StatusBadge
                    estado={transportadora.tipoServicio === 'urgente' ? 'advertencia' : 'neutral'}
                  >
                    {transportadora.tipoServicio === 'urgente' ? 'Urgente' : 'Estándar'}
                  </StatusBadge>

                  <span className="text-sm tabular-nums text-slate-600">
                    {transportadora.diasEntrega}{' '}
                    {transportadora.diasEntrega === 1 ? 'día' : 'días'}
                  </span>
                </>
              ) : (
                <StatusBadge estado="inactivo">Retirada</StatusBadge>
              )}

              {/*
                Editar solo tiene sentido en una activa: cambiarle el plazo a una
                retirada no cambia nada hasta que vuelva, y ofrecerlo sugiere que
                sigue en uso.
              */}
              {transportadora.activo ? (
                <button
                  type="button"
                  onClick={() => abrirEdicion(transportadora)}
                  disabled={enviando}
                  title="Cambiar nombre, tipo o plazo"
                  aria-label={`Editar ${transportadora.nombre}`}
                  className="rounded-md p-1.5 text-slate-500 transition hover:bg-slate-200 hover:text-slate-800 disabled:cursor-not-allowed disabled:opacity-40"
                >
                  <Pencil size={15} aria-hidden="true" />
                </button>
              ) : null}

              {/*
                Botón con palabra, no un bote de basura: el icono de basura
                promete un borrado que no ocurre. La fila se conserva y los
                traslados que llevó la siguen citando.
              */}
              <button
                type="button"
                onClick={() => cambiarEstado(transportadora)}
                disabled={enviando}
                title={
                  transportadora.activo
                    ? 'Deja de ofrecerse al despachar. No se borra: sus traslados la siguen citando.'
                    : 'Vuelve a ofrecerse al despachar, con su historia.'
                }
                aria-label={
                  transportadora.activo
                    ? `Retirar ${transportadora.nombre} del catálogo`
                    : `Devolver ${transportadora.nombre} al catálogo`
                }
                className={`inline-flex h-8 items-center gap-1.5 rounded-lg border px-2.5 text-xs font-semibold transition disabled:cursor-not-allowed disabled:opacity-40 ${
                  transportadora.activo
                    ? 'border-slate-300 bg-white text-slate-700 hover:border-terracota-400 hover:bg-terracota-50 hover:text-terracota-700'
                    : 'border-petroleo-300 bg-white text-petroleo-700 hover:bg-petroleo-50'
                }`}
              >
                {transportadora.activo ? (
                  <>
                    <XCircle size={14} aria-hidden="true" />
                    Retirar
                  </>
                ) : (
                  <>
                    <RotateCcw size={14} aria-hidden="true" />
                    Reactivar
                  </>
                )}
              </button>
            </li>
          ))}
        </ul>

        {/*
          Hay que decirlo: cambiar el plazo no reescribe la historia. Quien lo
          corrige espera que se arregle "todo", y los traslados en tránsito
          conservan la fecha con la que salieron.
        */}
        <p className="text-xs text-slate-500">
          Cambiar el plazo afecta a los despachos que vengan. Los traslados que ya salieron
          conservan la fecha estimada con la que se despacharon, porque cada uno guarda la suya.{' '}
          <strong className="font-semibold">Retirar no borra:</strong> la transportadora deja de
          ofrecerse al despachar, pero sus traslados la siguen citando con su guía. Se puede
          reactivar cuando vuelva a hacer falta.
        </p>
      </div>
    </Modal>
  );
}
