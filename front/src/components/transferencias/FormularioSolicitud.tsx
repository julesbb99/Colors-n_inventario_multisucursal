import { useState } from 'react';
import { Modal } from '../ui/Modal';
import { Boton } from '../ui/Boton';
import { Alerta } from '../ui/Alerta';
import { Spinner } from '../ui/Spinner';
import { CampoNumero, CampoSelect } from '../ui/Campos';
import { useSede } from '../../hooks/useSede';
import { useCatalogos } from '../../hooks/useCatalogos';
import { useEnvio } from '../../hooks/useEnvio';
import { solicitarTransferencia } from '../../services/transferencias';
import { URGENCIA } from '../../models/transferencias';
import type { ValorUrgencia } from '../../models/transferencias';

const OPCIONES_URGENCIA = [
  { valor: String(URGENCIA.baja), texto: 'Baja' },
  { valor: String(URGENCIA.media), texto: 'Media' },
  { valor: String(URGENCIA.alta), texto: 'Alta' },
];

interface FormularioSolicitudProps {
  onCerrar: () => void;
  onSolicitada: () => void;
}

/**
 * Solicitar un traslado.
 *
 * LO PIDE EL DESTINO, así que la sede de destino es la del usuario y el origen
 * es la otra. Para un gerente u operador el destino va fijo en su sede: pedirle
 * mercancía para una bodega que no es la suya devolvería 403.
 */
export function FormularioSolicitud({ onCerrar, onSolicitada }: FormularioSolicitudProps) {
  const { sucursales, sedeActiva, puedeCambiarSede } = useSede();
  const { opcionesProducto, opcionesUnidad, unidadBaseDe } = useCatalogos();
  const { enviando, error, esPermisos, enviar } = useEnvio();

  const [productoId, setProductoId] = useState('');
  const [origenId, setOrigenId] = useState('');
  const [destinoId, setDestinoId] = useState(sedeActiva === null ? '' : String(sedeActiva));
  const [cantidad, setCantidad] = useState('');
  const [unidadId, setUnidadId] = useState('');
  const [urgencia, setUrgencia] = useState(String(URGENCIA.media));
  const [validacion, setValidacion] = useState<string | null>(null);

  function elegirProducto(valor: string) {
    setProductoId(valor);
    const unidad = unidadBaseDe(Number(valor));
    setUnidadId(unidad === null ? '' : String(unidad));
  }

  const opcionesSede = sucursales.map((s) => ({
    valor: String(s.id),
    texto: `${s.nombre} — ${s.ciudad}`,
  }));

  function alGuardar() {
    setValidacion(null);

    if (!productoId || !origenId || !destinoId || !unidadId) {
      setValidacion('Completa producto, sedes y unidad.');
      return;
    }
    if (origenId === destinoId) {
      // La base lo impide con un CHECK, pero un 500 desde el servidor no
      // explica nada; mejor decirlo aquí.
      setValidacion('El origen y el destino no pueden ser la misma sede.');
      return;
    }
    const valorCantidad = Number(cantidad);
    if (!Number.isFinite(valorCantidad) || valorCantidad <= 0) {
      setValidacion('La cantidad debe ser mayor que cero.');
      return;
    }

    void enviar(async () => {
      await solicitarTransferencia({
        productoId: Number(productoId),
        sucursalOrigenId: Number(origenId),
        sucursalDestinoId: Number(destinoId),
        cantidad: valorCantidad,
        unidadId: Number(unidadId),
        // Número, no texto: la API no registra JsonStringEnumConverter.
        urgencia: Number(urgencia) as ValorUrgencia,
      });
      onSolicitada();
      onCerrar();
    });
  }

  return (
    <Modal
      titulo="Solicitar traslado"
      descripcion="Nace 'Solicitada' y todavía no mueve stock. El descuento ocurre cuando el origen despacha."
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
                <Spinner etiqueta="Solicitando" />
                Solicitando…
              </>
            ) : (
              'Solicitar'
            )}
          </Boton>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        {error ? <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta> : null}
        {validacion ? <Alerta tipo="info">{validacion}</Alerta> : null}

        <CampoSelect
          etiqueta="Producto"
          valor={productoId}
          onCambio={elegirProducto}
          opciones={opcionesProducto}
          placeholder="Selecciona un producto"
          requerido
          disabled={enviando}
        />

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <CampoSelect
            etiqueta="Sede origen"
            valor={origenId}
            onCambio={setOrigenId}
            opciones={opcionesSede}
            placeholder="¿De dónde sale?"
            requerido
            disabled={enviando}
            ayuda="La que tiene la mercancía."
          />
          <CampoSelect
            etiqueta="Sede destino"
            valor={destinoId}
            onCambio={setDestinoId}
            opciones={opcionesSede}
            placeholder="¿A dónde llega?"
            requerido
            disabled={enviando || !puedeCambiarSede}
            ayuda={puedeCambiarSede ? 'La que necesita la mercancía.' : 'Tu sede: eres quien pide.'}
          />
        </div>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
          <CampoNumero
            etiqueta="Cantidad"
            valor={cantidad}
            onCambio={setCantidad}
            requerido
            disabled={enviando}
          />
          <CampoSelect
            etiqueta="Unidad"
            valor={unidadId}
            onCambio={setUnidadId}
            opciones={opcionesUnidad}
            placeholder="Elegir"
            requerido
            disabled={enviando}
          />
          <CampoSelect
            etiqueta="Urgencia"
            valor={urgencia}
            onCambio={setUrgencia}
            opciones={OPCIONES_URGENCIA}
            disabled={enviando}
          />
        </div>
      </div>
    </Modal>
  );
}
