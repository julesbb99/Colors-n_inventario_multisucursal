import { createContext, useCallback, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { useAuth } from '../hooks/useAuth';
import { obtenerSucursales } from '../services/comun';
import { mensajeDeError } from '../models/api';
import type { SucursalDto } from '../models/comun';

export interface SedeContextValue {
  sucursales: SucursalDto[];
  /** Sede por la que se filtra. `null` = sin filtro, o sea toda la red. */
  sedeActiva: number | null;
  nombreSedeActiva: string;
  /** Solo el Administrador General puede conmutar de sede. */
  puedeCambiarSede: boolean;
  cambiarSede: (sucursalId: number | null) => void;
  cargando: boolean;
  error: string | null;
}

export const SedeContext = createContext<SedeContextValue | undefined>(undefined);

/**
 * La sede por la que se esta mirando el sistema.
 *
 * LA REGLA MULTI-SEDE, que espeja la de `IUsuarioContexto` en la API:
 *
 *   AdminGeneral      elige cualquier sede, o "Todas" para ver la red completa.
 *   Gerente/Operador  queda fijo en la sede de SU token; el selector va
 *                     deshabilitado.
 *
 * ESTO NO ES UN CONTROL DE SEGURIDAD, es comodidad de la interfaz. Quien de
 * verdad impide ver otra sede es la API, que responde 403 si el `sucursalId` de
 * la consulta no coincide con el del token. Deshabilitar el selector evita que
 * alguien se tope con ese 403 sin entender por que, nada mas: un usuario que
 * edite la peticion a mano sigue chocando contra el servidor.
 */
export function SedeProvider({ children }: { children: ReactNode }) {
  const { estaAutenticado, esAdminGeneral, sucursalId } = useAuth();

  const [sucursales, setSucursales] = useState<SucursalDto[]>([]);
  const [cargando, setCargando] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // El Administrador General arranca viendo la red completa; los demas, su sede.
  const [sedeElegida, setSedeElegida] = useState<number | null>(null);

  useEffect(() => {
    if (!estaAutenticado) {
      setSucursales([]);
      setError(null);
      return;
    }

    let vigente = true;
    setCargando(true);
    setError(null);

    obtenerSucursales()
      .then((datos) => {
        if (vigente) {
          setSucursales(datos);
        }
      })
      .catch((fallo: unknown) => {
        if (vigente) {
          setError(mensajeDeError(fallo));
        }
      })
      .finally(() => {
        if (vigente) {
          setCargando(false);
        }
      });

    // Si la sesion cambia mientras la peticion viaja, lo que llegue tarde ya no
    // corresponde a este usuario y no debe escribirse en el estado.
    return () => {
      vigente = false;
    };
  }, [estaAutenticado]);

  const cambiarSede = useCallback(
    (nueva: number | null) => {
      if (!esAdminGeneral) {
        return;
      }
      setSedeElegida(nueva);
    },
    [esAdminGeneral],
  );

  // Para un gerente o un operador la sede efectiva es SIEMPRE la de su token,
  // independientemente de lo que tenga `sedeElegida`. Se calcula aqui y no al
  // guardar para que cambiar de usuario no arrastre la seleccion del anterior.
  const sedeActiva = esAdminGeneral ? sedeElegida : sucursalId;

  const nombreSedeActiva = useMemo(() => {
    if (sedeActiva === null) {
      return 'Todas las sedes';
    }
    const sede = sucursales.find((s) => s.id === sedeActiva);
    return sede ? sede.nombre : `Sede ${sedeActiva}`;
  }, [sedeActiva, sucursales]);

  const valor = useMemo<SedeContextValue>(
    () => ({
      sucursales,
      sedeActiva,
      nombreSedeActiva,
      puedeCambiarSede: esAdminGeneral,
      cambiarSede,
      cargando,
      error,
    }),
    [sucursales, sedeActiva, nombreSedeActiva, esAdminGeneral, cambiarSede, cargando, error],
  );

  return <SedeContext.Provider value={valor}>{children}</SedeContext.Provider>;
}
