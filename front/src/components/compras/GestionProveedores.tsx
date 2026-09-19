import { useEffect, useState } from 'react';
import { Pencil, Plus, RotateCcw, Trash2 } from 'lucide-react';
import { Modal } from '../ui/Modal';
import { Boton } from '../ui/Boton';
import { Alerta } from '../ui/Alerta';
import { Spinner } from '../ui/Spinner';
import { CampoNumero, CampoTexto } from '../ui/Campos';
import { StatusBadge } from '../ui/StatusBadge';
import { useCatalogos } from '../../hooks/useCatalogos';
import { useConsulta } from '../../hooks/useConsulta';
import { useEnvio } from '../../hooks/useEnvio';
import {
  actualizarProveedor,
  crearProveedor,
  guardarPrecioReferencia,
  obtenerPrecioReferencia,
  obtenerProveedores,
  reactivarProveedor,
  retirarProveedor,
} from '../../services/compras';
import type { ProveedorDto } from '../../models/compras';
import { formatearCOP } from '../../utils/formato';

const SIN_PROVEEDORES: ProveedorDto[] = [];

interface GestionProveedoresProps {
  onCerrar: () => void;
}

/**
 * Catálogo de proveedores y su lista de precios. SOLO ADMINISTRADOR GENERAL.
 *
 * Vive aquí, dentro de Compras, y no en una pantalla propia porque es donde se
 * usa: quien mantiene los precios es quien mira las órdenes. El botón que abre
 * esto ya está condicionado al rol, y la API responde 403 a cualquier otro.
 *
 * RETIRAR NO BORRA. `ordenes_compra` referencia al proveedor con RESTRICT y
 * `producto_proveedor` con CASCADE, así que un borrado real fallaría con los
 * proveedores que tienen historia y se llevaría la lista de precios de los que
 * no. Se marca inactivo y deja de ofrecerse en órdenes nuevas.
 */
export function GestionProveedores({ onCerrar }: GestionProveedoresProps) {
  const { productos } = useCatalogos();
  const { enviando, error, esPermisos, enviar } = useEnvio();

  const {
    datos: proveedores,
    cargando,
    recargar,
  } = useConsulta(() => obtenerProveedores(true), [], SIN_PROVEEDORES);

  const [editando, setEditando] = useState<ProveedorDto | null>(null);
  const [creando, setCreando] = useState(false);
  const [precios, setPrecios] = useState<number | null>(null);

  const [nombre, setNombre] = useState('');
  const [contacto, setContacto] = useState('');
  const [telefono, setTelefono] = useState('');
  const [validacion, setValidacion] = useState<string | null>(null);

  function abrirCreacion() {
    setCreando(true);
    setEditando(null);
    setNombre('');
    setContacto('');
    setTelefono('');
    setValidacion(null);
  }

  function abrirEdicion(proveedor: ProveedorDto) {
    setEditando(proveedor);
    setCreando(false);
    setNombre(proveedor.nombre);
    setContacto(proveedor.contacto ?? '');
    setTelefono(proveedor.telefono);
    setValidacion(null);
  }

  function cerrarFormulario() {
    setCreando(false);
    setEditando(null);
    setValidacion(null);
  }

  function guardar() {
    setValidacion(null);

    if (nombre.trim() === '' || telefono.trim() === '') {
      setValidacion('El nombre y el teléfono son obligatorios.');
      return;
    }

    const cuerpo = {
      nombre: nombre.trim(),
      contacto: contacto.trim() === '' ? null : contacto.trim(),
      telefono: telefono.trim(),
    };

    void enviar(async () => {
      if (editando) {
        await actualizarProveedor(editando.id, cuerpo);
      } else {
        await crearProveedor(cuerpo);
      }
      recargar();
      cerrarFormulario();
    });
  }

  function cambiarEstado(proveedor: ProveedorDto) {
    void enviar(async () => {
      if (proveedor.activo) {
        await retirarProveedor(proveedor.id);
      } else {
        await reactivarProveedor(proveedor.id);
      }
      recargar();
    });
  }

  return (
    <Modal
      titulo="Proveedores"
      descripcion="Catálogo y lista de precios de toda la red. Solo el Administrador General puede cambiarlos."
      ancho="xl"
      ocupado={enviando}
      onCerrar={onCerrar}
      pie={
        <Boton variante="secundaria" onClick={onCerrar} disabled={enviando}>
          Cerrar
        </Boton>
      }
    >
      <div className="flex flex-col gap-4">
        {error ? <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta> : null}
        {validacion ? <Alerta tipo="info">{validacion}</Alerta> : null}

        {creando || editando ? (
          <div className="flex flex-col gap-3 rounded-xl border border-petroleo-200 bg-petroleo-50/50 p-4">
            <h3 className="text-sm font-bold text-slate-800">
              {editando ? `Editar ${editando.nombre}` : 'Nuevo proveedor'}
            </h3>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
              <CampoTexto
                etiqueta="Nombre"
                valor={nombre}
                onCambio={setNombre}
                requerido
                disabled={enviando}
                ayuda="Único en el catálogo."
              />
              <CampoTexto
                etiqueta="Contacto"
                valor={contacto}
                onCambio={setContacto}
                disabled={enviando}
                placeholder="Opcional"
              />
              <CampoTexto
                etiqueta="Teléfono"
                valor={telefono}
                onCambio={setTelefono}
                requerido
                disabled={enviando}
              />
            </div>
            <div className="flex justify-end gap-2">
              <Boton variante="secundaria" onClick={cerrarFormulario} disabled={enviando}>
                Cancelar
              </Boton>
              <Boton onClick={guardar} disabled={enviando}>
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
        ) : (
          <div className="flex justify-end">
            <Boton onClick={abrirCreacion} disabled={enviando}>
              <Plus size={17} aria-hidden="true" />
              Nuevo proveedor
            </Boton>
          </div>
        )}

        {cargando ? <p className="text-sm text-slate-500">Cargando proveedores…</p> : null}

        <ul className="flex flex-col gap-2">
          {proveedores.map((proveedor) => (
            <li
              key={proveedor.id}
              className="flex flex-col gap-3 rounded-xl border border-slate-200 bg-white p-3"
            >
              <div className="flex flex-wrap items-center gap-3">
                <span className="min-w-0 flex-1">
                  <span className="font-semibold text-slate-900">{proveedor.nombre}</span>
                  <span className="block text-xs text-slate-500">
                    {proveedor.contacto ? `${proveedor.contacto} · ` : ''}
                    {proveedor.telefono}
                    {proveedor.productosQueSurte !== null
                      ? ` · surte ${proveedor.productosQueSurte} producto(s)`
                      : ''}
                  </span>
                </span>

                {proveedor.activo ? null : <StatusBadge estado="inactivo">Retirado</StatusBadge>}

                <button
                  type="button"
                  onClick={() => setPrecios(precios === proveedor.id ? null : proveedor.id)}
                  disabled={enviando}
                  className="rounded-md border border-slate-300 px-2 py-1 text-xs font-semibold text-slate-700 transition hover:bg-slate-50 disabled:opacity-40"
                >
                  {precios === proveedor.id ? 'Ocultar precios' : 'Lista de precios'}
                </button>

                <button
                  type="button"
                  onClick={() => abrirEdicion(proveedor)}
                  disabled={enviando}
                  title="Editar"
                  aria-label={`Editar ${proveedor.nombre}`}
                  className="rounded-md p-1.5 text-slate-500 transition hover:bg-slate-100 hover:text-slate-800 disabled:opacity-40"
                >
                  <Pencil size={16} aria-hidden="true" />
                </button>

                <button
                  type="button"
                  onClick={() => cambiarEstado(proveedor)}
                  disabled={enviando}
                  title={
                    proveedor.activo
                      ? 'Retirar del catálogo. No borra: conserva órdenes y precios.'
                      : 'Volver a ofrecerlo en órdenes nuevas'
                  }
                  aria-label={
                    proveedor.activo
                      ? `Retirar ${proveedor.nombre}`
                      : `Reactivar ${proveedor.nombre}`
                  }
                  className="rounded-md p-1.5 text-slate-500 transition hover:bg-terracota-50 hover:text-terracota-700 disabled:opacity-40"
                >
                  {proveedor.activo ? (
                    <Trash2 size={16} aria-hidden="true" />
                  ) : (
                    <RotateCcw size={16} aria-hidden="true" />
                  )}
                </button>
              </div>

              {precios === proveedor.id ? (
                <ListaPrecios
                  proveedor={proveedor}
                  productos={productos}
                  onGuardado={recargar}
                />
              ) : null}
            </li>
          ))}
        </ul>
      </div>
    </Modal>
  );
}

interface ListaPreciosProps {
  proveedor: ProveedorDto;
  productos: { id: number; nombre: string; unidadBaseSimbolo: string | null }[];
  onGuardado: () => void;
}

/**
 * La lista de precios de un proveedor, un producto por fila.
 *
 * EL PRECIO VA POR UNIDAD BASE del producto, que es como lo guarda la tabla
 * `producto_proveedor` -no tiene columna de unidad-. Por eso cada fila muestra
 * el símbolo: sin él, "48.880" no dice si es por litro o por caneca.
 *
 * Dejar el campo vacío QUITA el producto de la lista. No toca ninguna orden
 * histórica: las líneas guardan su propio precio, no una referencia a esto.
 */
function ListaPrecios({ proveedor, productos, onGuardado }: ListaPreciosProps) {
  const { enviando, error, enviar } = useEnvio();
  const [valores, setValores] = useState<Record<number, string>>({});

  // Se consulta al desplegar y no al abrir el modal: son tantas peticiones como
  // productos, y solo hacen falta para el proveedor que se está mirando.
  //
  // En un efecto y no en el render: pedir datos mientras se pinta es un efecto
  // secundario en mitad del render, y React puede ejecutarlo dos veces o
  // descartarlo. El `vigente` descarta la respuesta si el panel se cierra o
  // cambia de proveedor mientras la petición viaja.
  useEffect(() => {
    let vigente = true;

    void Promise.all(
      productos.map((producto) =>
        obtenerPrecioReferencia(producto.id, proveedor.id)
          .then((dato) => [producto.id, dato.precioReferencia] as const)
          .catch(() => [producto.id, null] as const),
      ),
    ).then((filas) => {
      if (!vigente) {
        return;
      }
      const mapa: Record<number, string> = {};
      for (const [id, precio] of filas) {
        mapa[id] = precio === null ? '' : String(precio);
      }
      setValores(mapa);
    });

    return () => {
      vigente = false;
    };
  }, [proveedor.id, productos]);

  function guardar(productoId: number) {
    const texto = valores[productoId] ?? '';
    const precio = texto.trim() === '' ? null : Number(texto);

    if (precio !== null && (!Number.isFinite(precio) || precio < 0)) {
      return;
    }

    void enviar(async () => {
      await guardarPrecioReferencia(proveedor.id, productoId, { precioReferencia: precio });
      onGuardado();
    });
  }

  return (
    <div className="flex flex-col gap-2 rounded-lg bg-slate-50 p-3">
      {error ? <Alerta tipo="error">{error}</Alerta> : null}

      {productos.map((producto) => (
        <div key={producto.id} className="flex flex-wrap items-end gap-3">
          <span className="min-w-0 flex-1 text-sm text-slate-700">
            {producto.nombre}
            <span className="text-xs text-slate-400"> · por {producto.unidadBaseSimbolo}</span>
          </span>

          <div className="w-40">
            <CampoNumero
              etiqueta="Precio de lista"
              valor={valores[producto.id] ?? ''}
              onCambio={(v) => setValores((previos) => ({ ...previos, [producto.id]: v }))}
              min={0}
              disabled={enviando}
            />
          </div>

          <Boton
            variante="secundaria"
            onClick={() => guardar(producto.id)}
            disabled={enviando}
            className="!h-11 !px-3 text-xs"
          >
            Guardar
          </Boton>
        </div>
      ))}

      <p className="text-xs text-slate-500">
        Vacío quita el producto de la lista de este proveedor. Ejemplo de lectura:{' '}
        {formatearCOP(48880)} por litro son {formatearCOP(Math.round(48880 * 3.78541))} por galón.
      </p>
    </div>
  );
}
