import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { ProtectedRoute } from './ProtectedRoute';
import { MainLayout } from '../components/layout/MainLayout';
import { Login } from '../pages/auth/Login';
import { Dashboard } from '../pages/dashboard/Dashboard';
import { Existencias } from '../pages/inventario/Existencias';
import { LotesFefo } from '../pages/inventario/LotesFefo';
import { Traslados } from '../pages/transferencias/Traslados';
import { Compras } from '../pages/compras/Compras';
import { Ventas } from '../pages/ventas/Ventas';
import { Usuarios } from '../pages/comun/Usuarios';
import { ROLES_SUPERVISION } from '../models/auth';

/**
 * Rutas de la aplicación.
 *
 * Las protegidas cuelgan de `ProtectedRoute` y dentro de `MainLayout`, en ese
 * orden: primero se comprueba la sesión y después se pinta el marco. Al revés,
 * la barra lateral y el selector de sede aparecerían por un instante antes de
 * mandar a /login.
 *
 * NINGUNA RUTA LLEVA `allowedRoles` todavía, y es deliberado: las cinco
 * pantallas son de lectura, y leer lo permite cualquier rol. El aislamiento real
 * -qué sede ve cada quien- lo aplica la API, no el enrutador. Cuando se agreguen
 * las pantallas de escritura (crear orden, despachar traslado), esas sí se
 * acotan a `ROLES_SUPERVISION`.
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
            <Route path="/inventario/lotes" element={<LotesFefo />} />
            <Route path="/traslados" element={<Traslados />} />
            <Route path="/compras" element={<Compras />} />
            <Route path="/ventas" element={<Ventas />} />
          </Route>
        </Route>

        {/* Usuarios SÍ lleva `allowedRoles`: es la primera pantalla del sistema
            que un operador no debe siquiera abrir, porque lista correos y roles
            del personal. La API responde 403 igualmente; esto evita mostrarle
            una pantalla que solo le daría un error. */}
        <Route element={<ProtectedRoute allowedRoles={ROLES_SUPERVISION} />}>
          <Route element={<MainLayout />}>
            <Route path="/usuarios" element={<Usuarios />} />
          </Route>
        </Route>

        {/* Cualquier otra ruta vuelve al panel; si no hay sesión, ProtectedRoute
            la desviará a /login desde ahí. */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
