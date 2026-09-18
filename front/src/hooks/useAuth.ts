import { useContext } from 'react';
import { AuthContext } from '../context/AuthContext';
import type { AuthContextValue } from '../context/AuthContext';

/**
 * Acceso a la sesion.
 *
 * Lanza si se usa fuera de `AuthProvider`. Es deliberado: la alternativa
 * -devolver un contexto vacio- daria un `estaAutenticado: false` que se lee
 * exactamente igual que "no ha iniciado sesion", y el sintoma seria una pantalla
 * que manda a /login sin motivo, lejos del componente mal colocado.
 */
export function useAuth(): AuthContextValue {
  const contexto = useContext(AuthContext);

  if (contexto === undefined) {
    throw new Error('useAuth se uso fuera de <AuthProvider>.');
  }

  return contexto;
}
