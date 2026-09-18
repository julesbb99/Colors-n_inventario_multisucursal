import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { ProtectedRoute } from './ProtectedRoute';
import { MainLayout } from '../components/layout/MainLayout';
import { Login } from '../pages/auth/Login';
import { Dashboard } from '../pages/dashboard/Dashboard';
import { Existencias } from '../pages/inventario/Existencias';

/**
 * Rutas de la aplicacion.
 *
 * Las protegidas cuelgan de `ProtectedRoute` y dentro de `MainLayout`, en ese
 * orden: primero se comprueba la sesion y despues se pinta el marco. Al reves,
 * la barra lateral y el selector de sede aparecerian por un instante antes de
 * mandar a /login.
 */
export function AppRouter() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<Login />} />

        <Route element={<ProtectedRoute />}>
          <Route element={<MainLayout />}>
            <Route path="/" element={<Dashboard />} />
            <Route path="/inventario/existencias" element={<Existencias />} />
          </Route>
        </Route>

        {/* Cualquier otra ruta vuelve al panel; si no hay sesion, ProtectedRoute
            la desviara a /login desde ahi. */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
