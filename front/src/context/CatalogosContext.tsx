import { createContext, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { useAuth } from '../hooks/useAuth';
import { obtenerUnidadesMedida } from '../services/comun';
import { obtenerProductos } from '../services/inventario';
import type { UnidadMedidaDto } from '../models/comun';
import type { ProductoDto } from '../models/inventario';
import type { OpcionSelect } from '../components/ui/Campos';

export interface CatalogosContextValue {
  productos: ProductoDto[];
  unidades: UnidadMedidaDto[];
  /** Listas ya preparadas para un `<select>`. */
  opcionesProducto: OpcionSelect[];
  opcionesUnidad: OpcionSelect[];
  cargando: boolean;
  /** La unidad base de un producto: es la que conviene preseleccionar. */
  unidadBaseDe: (productoId: number) => number | null;
  /** El producto completo, para leer su precio de venta sin volver a la API. */
  productoPorId: (productoId: number) => ProductoDto | null;
  /**
   * Vuelve a pedir los catálogos.
   *
   * Existe por el precio de venta: es el único dato de aquí que se edita dentro
   * de la aplicación, y sin esto el formulario de venta seguiría rellenando con
   * el precio viejo hasta recargar la página.
   */
  recargar: () => void;
}

export const CatalogosContext = createContext<CatalogosContextValue | undefined>(undefined);

/**
 * Productos y unidades de medida, cargados UNA vez para toda la sesión.
 *
 * Son catálogos de la red, no cambian entre sedes y los necesitan cinco
 * formularios distintos. Pedirlos en cada uno significaría dos consultas cada
 * vez que alguien abre un modal, para datos que no se mueven.
 *
 * Los catálogos de un solo formulario -clientes, proveedores, transportadoras-
 * NO están aquí: los pide quien los usa, y así no se descargan hasta que hacen
 * falta.
 */
export function CatalogosProvider({ children }: { children: ReactNode }) {
  const { estaAutenticado } = useAuth();

  const [productos, setProductos] = useState<ProductoDto[]>([]);
  const [unidades, setUnidades] = useState<UnidadMedidaDto[]>([]);
  const [cargando, setCargando] = useState(false);
  // Un contador y no un booleano: pedir dos recargas seguidas con un booleano
  // solo dispararía la primera.
  const [version, setVersion] = useState(0);

  useEffect(() => {
    if (!estaAutenticado) {
      setProductos([]);
      setUnidades([]);
      return;
    }

    let vigente = true;
    setCargando(true);

    Promise.all([obtenerProductos(), obtenerUnidadesMedida()])
      .then(([listaProductos, listaUnidades]) => {
        if (vigente) {
          setProductos(listaProductos);
          setUnidades(listaUnidades);
        }
      })
      // Un catálogo que no carga deja los selectores vacíos, y eso ya se ve en
      // pantalla. No se guarda el error: el formulario que lo necesite informará
      // al intentar enviar, que es donde la persona puede hacer algo.
      .catch(() => undefined)
      .finally(() => {
        if (vigente) {
          setCargando(false);
        }
      });

    return () => {
      vigente = false;
    };
  }, [estaAutenticado, version]);

  const valor = useMemo<CatalogosContextValue>(() => {
    const porId = new Map(productos.map((p) => [p.id, p]));

    return {
      productos,
      unidades,
      opcionesProducto: productos.map((p) => ({
        valor: String(p.id),
        texto: p.categoria ? `${p.nombre} — ${p.categoria}` : p.nombre,
      })),
      opcionesUnidad: unidades.map((u) => ({
        valor: String(u.id),
        texto: `${u.nombre} (${u.simbolo})`,
      })),
      cargando,
      unidadBaseDe: (productoId) => porId.get(productoId)?.unidadBaseId ?? null,
      productoPorId: (productoId) => porId.get(productoId) ?? null,
      recargar: () => setVersion((n) => n + 1),
    };
  }, [productos, unidades, cargando]);

  return <CatalogosContext.Provider value={valor}>{children}</CatalogosContext.Provider>;
}
