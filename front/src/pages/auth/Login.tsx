import { useState } from 'react';
import type { FormEvent } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { LogIn } from 'lucide-react';
import { useAuth } from '../../hooks/useAuth';
import { esApiError, mensajeDeError } from '../../models/api';
import { Alerta } from '../../components/ui/Alerta';
import { Spinner } from '../../components/ui/Spinner';

interface EstadoNavegacion {
  desde?: string;
}

export function Login() {
  const { estaAutenticado, login } = useAuth();
  const navegar = useNavigate();
  const ubicacion = useLocation();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [esLimite, setEsLimite] = useState(false);

  // Quien ya tiene sesion no deberia ver este formulario: si vuelve a /login a
  // mano, se le devuelve al panel.
  if (estaAutenticado) {
    return <Navigate to="/" replace />;
  }

  async function alEnviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault();

    // Doble guarda del envio: el boton se deshabilita, pero un Enter repetido
    // puede disparar el submit antes de que React repinte, y cada intento de mas
    // consume uno de los cinco por minuto que permite la API.
    if (enviando) {
      return;
    }

    setEnviando(true);
    setError(null);
    setEsLimite(false);

    try {
      await login({ email: email.trim(), password });

      const estado = ubicacion.state as EstadoNavegacion | null;
      navegar(estado?.desde ?? '/', { replace: true });
    } catch (fallo: unknown) {
      setError(mensajeDeError(fallo));
      setEsLimite(esApiError(fallo) && fallo.estado === 429);
    } finally {
      setEnviando(false);
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-100 px-4 py-10">
      <div className="w-full max-w-md">
        <div className="mb-7 flex items-center justify-center gap-3">
          <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-colorsin-700 text-lg font-black text-white">
            C
          </div>
          <span className="text-2xl font-bold tracking-tight text-slate-900">Colorsin</span>
        </div>

        <div className="rounded-2xl border border-slate-200 bg-white p-7 shadow-sm">
          <h1 className="text-xl font-bold text-slate-900">Iniciar sesion</h1>
          <p className="mt-1 text-sm text-slate-500">
            Entra con el correo con el que estas registrado.
          </p>

          <form onSubmit={alEnviar} className="mt-6 flex flex-col gap-4" noValidate>
            <div className="flex flex-col gap-1.5">
              <label htmlFor="email" className="text-sm font-semibold text-slate-700">
                Correo
              </label>
              <input
                id="email"
                name="email"
                type="email"
                autoComplete="username"
                required
                value={email}
                onChange={(evento) => setEmail(evento.target.value)}
                placeholder="nombre@colorsin.com.co"
                className="h-12 rounded-lg border border-slate-300 px-3.5 text-sm text-slate-900 outline-none transition placeholder:text-slate-400 focus:border-colorsin-500 focus:ring-2 focus:ring-colorsin-200"
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <label htmlFor="password" className="text-sm font-semibold text-slate-700">
                Contrasena
              </label>
              <input
                id="password"
                name="password"
                type="password"
                autoComplete="current-password"
                required
                value={password}
                onChange={(evento) => setPassword(evento.target.value)}
                className="h-12 rounded-lg border border-slate-300 px-3.5 text-sm text-slate-900 outline-none transition focus:border-colorsin-500 focus:ring-2 focus:ring-colorsin-200"
              />
            </div>

            {error ? <Alerta tipo={esLimite ? 'permisos' : 'error'}>{error}</Alerta> : null}

            <button
              type="submit"
              disabled={enviando}
              className="mt-1 flex h-12 items-center justify-center gap-2.5 rounded-lg bg-colorsin-700 text-sm font-semibold text-white transition hover:bg-colorsin-800 disabled:cursor-not-allowed disabled:bg-slate-400"
            >
              {enviando ? (
                <>
                  <Spinner etiqueta="Entrando" />
                  Entrando…
                </>
              ) : (
                <>
                  <LogIn size={18} aria-hidden="true" />
                  Entrar
                </>
              )}
            </button>
          </form>
        </div>

        <p className="mt-5 text-center text-xs leading-relaxed text-slate-500">
          Tras cinco intentos fallidos en un minuto la API bloquea el acceso desde tu conexion
          durante un rato.
        </p>
      </div>
    </div>
  );
}
