import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import type { Rol } from '../models/auth';

interface ProtectedRouteProps {
  /**
   * Roles admitidos. Omitido, basta con tener sesion.
   *
   * Es un filtro de NAVEGACION, no de seguridad: solo evita mostrar una pantalla
   * cuyas peticiones la API va a rechazar de todos modos con 403. El control real
   * vive en el servidor, que no confia en nada que decida el navegador.
   */
  allowedRoles?: readonly Rol[];
}

export function ProtectedRoute({ allowedRoles }: ProtectedRouteProps) {
  const { estaAutenticado, rol } = useAuth();
  const ubicacion = useLocation();

  if (!estaAutenticado) {
    // Se guarda a donde iba para devolverlo ahi despues de entrar, en vez de
    // dejarlo siempre en el panel. `replace` evita que el boton de atras vuelva
    // a una pantalla que ya no puede ver.
    return <Navigate to="/login" replace state={{ desde: ubicacion.pathname }} />;
  }

  if (allowedRoles && (rol === null || !allowedRoles.includes(rol))) {
    return <Navigate to="/" replace />;
  }

  return <Outlet />;
}
