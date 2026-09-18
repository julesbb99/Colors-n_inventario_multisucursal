import { AuthProvider } from './context/AuthContext';
import { AppRouter } from './router/AppRouter';

/**
 * La sesion envuelve al enrutador, no al reves: `ProtectedRoute` consulta
 * `useAuth` en cada navegacion, asi que el proveedor tiene que estar por encima.
 *
 * El proveedor de sede NO va aqui sino dentro de `MainLayout`, porque necesita
 * token para pedir el catalogo de sucursales.
 */
export default function App() {
  return (
    <AuthProvider>
      <AppRouter />
    </AuthProvider>
  );
}
