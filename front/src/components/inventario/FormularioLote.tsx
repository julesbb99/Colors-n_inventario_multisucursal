import { useState } from 'react';
import { Modal } from '../ui/Modal';
import { Boton } from '../ui/Boton';
import { Alerta } from '../ui/Alerta';
import { Spinner } from '../ui/Spinner';
import { CampoSelect, CampoTexto } from '../ui/Campos';
import { CampoSede } from '../ui/CampoSede';
import { useSede } from '../../hooks/useSede';
import { useCatalogos } from '../../hooks/useCatalogos';
import { useEnvio } from '../../hooks/useEnvio';
import { crearLote } from '../../services/inventario';

interface FormularioLoteProps {
  onCerrar: () => void;
  onCreado: () => void;
}

/**
 * Abrir un lote.
 *
 * NO TIENE CAMPO DE CANTIDAD, y es la decisión de diseño que hay que respetar:
 * el saldo vive a la vez en el consolidado de la sede y en el desglose por lote.
 * Cargar cantidad aquí la sumaría solo al desglose, sin fila en el libro mayor
 * que diga de dónde salió. El lote nace vacío y la mercancía entra después.
 */
export function FormularioLote({ onCerrar, onCreado }: FormularioLoteProps) {
  const { sedeActiva } = useSede();
  const { opcionesProducto } = useCatalogos();
  const { enviando, error, esPermisos, enviar } = useEnvio();

  const [productoId, setProductoId] = useState('');
  const [sucursalId, setSucursalId] = useState(sedeActiva === null ? '' : String(sedeActiva));
  const [numeroLote, setNumeroLote] = useState('');
  const [fechaVencimiento, setFechaVencimiento] = useState('');
  const [validacion, setValidacion] = useState<string | null>(null);

  function alGuardar() {
    setValidacion(null);

    if (!productoId || !sucursalId) {
      setValidacion('Elige el producto y la sede.');
      return;
    }
    if (numeroLote.trim() === '') {
      setValidacion('El número de lote es obligatorio: es lo que permite rastrear la mercancía.');
      return;
    }

    void enviar(async () => {
      await crearLote({
        productoId: Number(productoId),
        sucursalId: Number(sucursalId),
        numeroLote: numeroLote.trim(),
        fechaVencimiento: fechaVencimiento === '' ? null : fechaVencimiento,
      });
      onCreado();
      onCerrar();
    });
  }

  return (
    <Modal
      titulo="Nuevo lote"
      descripcion="Ficha de trazabilidad hasta el fabricante. Solo supervisión."
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
                <Spinner etiqueta="Creando" />
                Creando…
              </>
            ) : (
              'Crear lote'
            )}
          </Boton>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        {error ? <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta> : null}
        {validacion ? <Alerta tipo="info">{validacion}</Alerta> : null}

        <Alerta tipo="info">
          El lote se crea <strong>vacío</strong>. La mercancía entra después con un movimiento de
          ingreso indicando el lote, o con una recepción de compra.
        </Alerta>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <CampoSelect
            etiqueta="Producto"
            valor={productoId}
            onCambio={setProductoId}
            opciones={opcionesProducto}
            placeholder="Selecciona un producto"
            requerido
            disabled={enviando}
          />
          <CampoSede valor={sucursalId} onCambio={setSucursalId} disabled={enviando} />
        </div>

        <CampoTexto
          etiqueta="Número de lote"
          valor={numeroLote}
          onCambio={setNumeroLote}
          requerido
          disabled={enviando}
          placeholder="PEI-2026-095"
          ayuda="Único dentro de la pareja producto y sede. Si vuelve a llegar el mismo lote, no se abre otro: se le suma cantidad."
        />

        <CampoTexto
          etiqueta="Fecha de vencimiento"
          tipo="date"
          valor={fechaVencimiento}
          onCambio={setFechaVencimiento}
          disabled={enviando}
          ayuda="Vacía si el producto no caduca: va al final de la cola FEFO."
        />
      </div>
    </Modal>
  );
}
