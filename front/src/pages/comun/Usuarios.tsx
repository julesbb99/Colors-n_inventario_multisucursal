import { useMemo, useState } from 'react';
import { Plus, ShieldOff, ShieldCheck } from 'lucide-react';
import { useAuth } from '../../hooks/useAuth';
import { useSede } from '../../hooks/useSede';
import { useConsulta } from '../../hooks/useConsulta';
import { useEnvio } from '../../hooks/useEnvio';
import { deshabilitarUsuario, habilitarUsuario, obtenerUsuarios } from '../../services/comun';
import { ROLES } from '../../models/auth';
import type { Usuario } from '../../models/auth';
import { DataTable } from '../../components/ui/DataTable';
import type { ColumnaTabla } from '../../components/ui/DataTable';
import { StatusBadge } from '../../components/ui/StatusBadge';
import type { EstadoBadge } from '../../components/ui/StatusBadge';
import { Alerta } from '../../components/ui/Alerta';
import { Boton } from '../../components/ui/Boton';
import { Modal } from '../../components/ui/Modal';
import { Spinner } from '../../components/ui/Spinner';
import { FormularioUsuario } from '../../components/comun/FormularioUsuario';

const SIN_DATOS: Usuario[] = [];

/**
 * El tono por rol.
 *
 * `usuario.rol` trae el TEXTO DE LA BASE ('Gerente de Sucursal'), no la grafía
 * del claim. Es correcto usarlo aquí porque esto solo pinta; para decidir
 * permisos está el rol del token.
 */
function tonoDeRol(rol: string): EstadoBadge {
  if (rol === 'Administrador General') {
    return 'critico';
  }
  if (rol === 'Gerente de Sucursal') {
    return 'advertencia';
  }
  return 'neutral';
}

/** Quién está mirando. Decide a quién se le ofrece el botón de deshabilitar. */
interface QuienMira {
  usuarioId: number | null;
  sedePropia: number | null;
  esAdminGeneral: boolean;
}

/**
 * Si quien mira puede cambiar el estado de ese perfil.
 *
 * ES UNA COPIA DE LA REGLA DEL SERVIDOR, y hay que decirlo: la que manda vive
 * en `ReglasGestionUsuario` y la API responde 403 igual. Esto solo evita
 * ofrecer un botón que va a fallar, que es peor que no ofrecerlo: parece que la
 * acción es tuya y no lo es.
 *
 *   Administrador General  gerentes y operadores, cualquier sede
 *   Gerente de Sucursal    operadores, SOLO de su sede
 *
 * Y las dos reglas que no son de jerarquía: nadie sobre sí mismo —te deja fuera
 * del sistema en ese clic— y nadie sobre un Administrador General.
 */
function puedeGestionar(usuario: Usuario, quien: QuienMira): boolean {
  if (quien.usuarioId !== null && usuario.id === quien.usuarioId) {
    return false;
  }
  if (usuario.rol === 'Administrador General') {
    return false;
  }
  if (quien.esAdminGeneral) {
    return true;
  }
  // A partir de aquí, gerente: solo operadores de su propia sede.
  return usuario.rol === 'Operador' && usuario.sucursalId === quien.sedePropia;
}

function construirColumnas(
  quien: QuienMira,
  onGestionar: (usuario: Usuario) => void,
): ColumnaTabla<Usuario>[] {
  return [
    {
      id: 'nombre',
      header: 'Nombre',
      render: (u) => (
        <span
          className={u.activo ? 'font-medium text-slate-900' : 'font-medium text-slate-400'}
        >
          {u.nombre}
        </span>
      ),
    },
    {
      id: 'email',
      header: 'Correo',
      render: (u) => (
        <span className={u.activo ? 'text-slate-700' : 'text-slate-400'}>{u.email}</span>
      ),
    },
    {
      id: 'sede',
      header: 'Sede',
      render: (u) =>
        // El Administrador General no pertenece a ninguna sede, y eso significa
        // TODAS, no ninguna: un guion se leería al revés.
        u.sucursalNombre === null ? (
          <span className="text-slate-500">Toda la red</span>
        ) : (
          <span className={u.activo ? 'text-slate-700' : 'text-slate-400'}>
            {u.sucursalNombre}
          </span>
        ),
    },
    {
      id: 'rol',
      header: 'Rol',
      align: 'centro',
      render: (u) => <StatusBadge estado={tonoDeRol(u.rol)}>{u.rol}</StatusBadge>,
    },
    {
      id: 'estado',
      header: 'Estado',
      align: 'centro',
      render: (u) =>
        u.activo ? (
          <StatusBadge estado="activo">Habilitado</StatusBadge>
        ) : (
          <StatusBadge estado="inactivo">Deshabilitado</StatusBadge>
        ),
    },
    {
      id: 'acciones',
      header: '',
      align: 'derecha',
      ancho: 'w-36',
      render: (u) => {
        if (!puedeGestionar(u, quien)) {
          return <span className="text-slate-400">—</span>;
        }

        return (
          <button
            type="button"
            onClick={() => onGestionar(u)}
            className={`inline-flex h-9 shrink-0 items-center gap-1.5 whitespace-nowrap rounded-lg border px-2.5 text-xs font-semibold transition ${
              u.activo
                ? 'border-slate-300 bg-white text-slate-700 hover:border-terracota-400 hover:text-terracota-700'
                : 'border-emerald-300 bg-white text-emerald-700 hover:bg-emerald-50'
            }`}
          >
            {u.activo ? (
              <>
                <ShieldOff size={14} aria-hidden="true" />
                Deshabilitar
              </>
            ) : (
              <>
                <ShieldCheck size={14} aria-hidden="true" />
                Habilitar
              </>
            )}
          </button>
        );
      },
    },
  ];
}

export function Usuarios() {
  const { sesion, esSupervision, esAdminGeneral } = useAuth();
  const { sedeActiva, nombreSedeActiva } = useSede();
  const [abierto, setAbierto] = useState(false);
  const [incluirInactivos, setIncluirInactivos] = useState(true);
  /** Perfil sobre el que se va a confirmar el cambio de estado. */
  const [porGestionar, setPorGestionar] = useState<Usuario | null>(null);

  const { enviando, error: errorEnvio, esPermisos: envioEsPermisos, enviar } = useEnvio();

  const {
    datos: usuarios,
    cargando,
    error,
    esPermisos,
    recargar,
  } = useConsulta(
    () => obtenerUsuarios(sedeActiva, incluirInactivos),
    [sedeActiva, incluirInactivos],
    SIN_DATOS,
  );

  const quien = useMemo<QuienMira>(
    () => ({
      usuarioId: sesion?.usuarioId ?? null,
      sedePropia: sesion?.sucursalId ?? null,
      esAdminGeneral,
    }),
    [sesion?.usuarioId, sesion?.sucursalId, esAdminGeneral],
  );

  const columnas = useMemo(
    () => construirColumnas(quien, setPorGestionar),
    [quien],
  );

  const deshabilitados = usuarios.filter((u) => !u.activo).length;

  function confirmar() {
    if (porGestionar === null) {
      return;
    }
    const objetivo = porGestionar;

    void enviar(async () => {
      if (objetivo.activo) {
        await deshabilitarUsuario(objetivo.id);
      } else {
        await habilitarUsuario(objetivo.id);
      }
      recargar();
      setPorGestionar(null);
    });
  }

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-center gap-3">
        <p className="min-w-0 flex-1 text-sm text-slate-500">
          {nombreSedeActiva} ·{' '}
          {esAdminGeneral
            ? 'puedes dar de alta y deshabilitar gerentes y operadores en cualquier sede'
            : 'puedes dar de alta y deshabilitar operadores de tu sede'}
        </p>

        {/* El filtro arranca ENCENDIDO, al revés que en existencias o
            proveedores. Allí lo inactivo es producto retirado del catálogo y
            estorba; aquí es una persona a la que alguien le cerró la cuenta, y
            esconderla por omisión haría que nadie recordara reactivarla. */}
        <label className="flex h-11 cursor-pointer items-center gap-2 text-sm text-slate-600">
          <input
            type="checkbox"
            checked={incluirInactivos}
            onChange={(evento) => setIncluirInactivos(evento.target.checked)}
            className="h-4 w-4 cursor-pointer rounded border-slate-300 text-terracota-600 focus:ring-terracota-500"
          />
          Ver deshabilitados
          {deshabilitados > 0 ? (
            <span className="rounded-full bg-slate-200 px-1.5 py-0.5 text-[11px] font-bold tabular-nums text-slate-600">
              {deshabilitados}
            </span>
          ) : null}
        </label>

        {esSupervision ? (
          <Boton onClick={() => setAbierto(true)}>
            <Plus size={17} aria-hidden="true" />
            Nuevo usuario
          </Boton>
        ) : null}
      </div>

      {abierto ? (
        <FormularioUsuario onCerrar={() => setAbierto(false)} onCreado={recargar} />
      ) : null}

      {/*
        SE CONFIRMA, y no se hace al primer clic. Deshabilitar a alguien le
        cierra el acceso en el acto y no hay nada en la fila que avise de lo que
        implica. El texto dice también lo que NO pasa: no se borra nada.
      */}
      {porGestionar !== null ? (
        <Modal
          titulo={
            porGestionar.activo
              ? `Deshabilitar a ${porGestionar.nombre}`
              : `Habilitar a ${porGestionar.nombre}`
          }
          ancho="md"
          ocupado={enviando}
          onCerrar={() => setPorGestionar(null)}
          pie={
            <>
              <Boton
                variante="secundaria"
                onClick={() => setPorGestionar(null)}
                disabled={enviando}
              >
                Cancelar
              </Boton>
              <Boton onClick={confirmar} disabled={enviando}>
                {enviando ? (
                  <>
                    <Spinner etiqueta="Enviando" />
                    Enviando…
                  </>
                ) : porGestionar.activo ? (
                  'Deshabilitar'
                ) : (
                  'Habilitar'
                )}
              </Boton>
            </>
          }
        >
          <div className="flex flex-col gap-4">
            {errorEnvio ? (
              <Alerta tipo={envioEsPermisos ? 'permisos' : 'error'}>{errorEnvio}</Alerta>
            ) : null}

            <div className="rounded-xl bg-slate-50 px-4 py-3 text-sm text-slate-600">
              <span className="font-semibold text-slate-900">{porGestionar.nombre}</span> ·{' '}
              {porGestionar.rol}
              {porGestionar.sucursalNombre === null
                ? ''
                : ` · ${porGestionar.sucursalNombre}`}
              <span className="mt-0.5 block text-xs">{porGestionar.email}</span>
            </div>

            {porGestionar.activo ? (
              <>
                <p className="text-sm text-slate-700">
                  Dejará de poder iniciar sesión.{' '}
                  <strong className="font-semibold">No se borra nada</strong>: sus ventas, sus
                  movimientos y su rastro en la bitácora siguen ahí, porque esas filas no pueden
                  quedarse sin responsable. Se puede volver a habilitar cuando haga falta.
                </p>
                {/*
                  Hay que decirlo. Quien cierra una cuenta espera que el efecto
                  sea inmediato, y con la sesión ya abierta no lo es: el token
                  vive hasta una hora. Si la baja es urgente, esto es lo que hay
                  que saber.
                */}
                <Alerta tipo="info">
                  Si esa persona tiene la sesión abierta ahora mismo, seguirá dentro hasta que su
                  token caduque —hasta una hora—. Para cortar el acceso en el acto hay que
                  cambiarle la contraseña, y eso todavía no se puede hacer desde la aplicación.
                </Alerta>
              </>
            ) : (
              <p className="text-sm text-slate-700">
                Volverá a poder iniciar sesión con{' '}
                <strong className="font-semibold">la misma contraseña</strong> que tenía: el
                perfil no la perdió al deshabilitarse.
              </p>
            )}
          </div>
        </Modal>
      ) : null}

      {error ? (
        <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta>
      ) : (
        <DataTable
          columnas={columnas}
          data={usuarios}
          claveFila={(u) => u.id}
          cargando={cargando}
          estadoVacio="No hay usuarios registrados para esta sede."
        />
      )}

      <p className="text-xs text-slate-500">
        Un perfil deshabilitado no se borra ni se puede borrar: seis tablas lo citan como
        responsable —ventas, movimientos, traslados, órdenes de compra, novedades y la bitácora—
        y ninguna puede quedarse sin él. Nadie puede deshabilitar su propia cuenta
        {quien.esAdminGeneral || sesion?.rol === ROLES.gerenteSucursal
          ? ', ni la de un Administrador General.'
          : '.'}
      </p>
    </div>
  );
}
