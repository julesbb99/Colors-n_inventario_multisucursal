import { useState } from 'react';
import { Plus } from 'lucide-react';
import { useAuth } from '../../hooks/useAuth';
import { useSede } from '../../hooks/useSede';
import { useConsulta } from '../../hooks/useConsulta';
import { obtenerUsuarios } from '../../services/comun';
import type { Usuario } from '../../models/auth';
import { DataTable } from '../../components/ui/DataTable';
import type { ColumnaTabla } from '../../components/ui/DataTable';
import { StatusBadge } from '../../components/ui/StatusBadge';
import type { EstadoBadge } from '../../components/ui/StatusBadge';
import { Alerta } from '../../components/ui/Alerta';
import { Boton } from '../../components/ui/Boton';
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

const COLUMNAS: ColumnaTabla<Usuario>[] = [
  {
    id: 'nombre',
    header: 'Nombre',
    render: (u) => <span className="font-medium text-slate-900">{u.nombre}</span>,
  },
  { id: 'email', header: 'Correo', accessor: 'email' },
  {
    id: 'sede',
    header: 'Sede',
    render: (u) =>
      // El Administrador General no pertenece a ninguna sede, y eso significa
      // TODAS, no ninguna: un guion se leería al revés.
      u.sucursalNombre === null ? (
        <span className="text-slate-500">Toda la red</span>
      ) : (
        <span className="text-slate-700">{u.sucursalNombre}</span>
      ),
  },
  {
    id: 'rol',
    header: 'Rol',
    align: 'centro',
    render: (u) => <StatusBadge estado={tonoDeRol(u.rol)}>{u.rol}</StatusBadge>,
  },
];

export function Usuarios() {
  const { esSupervision, esAdminGeneral } = useAuth();
  const { sedeActiva, nombreSedeActiva } = useSede();
  const [abierto, setAbierto] = useState(false);

  const {
    datos: usuarios,
    cargando,
    error,
    esPermisos,
    recargar,
  } = useConsulta(() => obtenerUsuarios(sedeActiva), [sedeActiva], SIN_DATOS);

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-center gap-3">
        <p className="min-w-0 flex-1 text-sm text-slate-500">
          {nombreSedeActiva} ·{' '}
          {esAdminGeneral
            ? 'puedes dar de alta gerentes y operadores en cualquier sede'
            : 'puedes dar de alta operadores de tu sede'}
        </p>

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

      {error ? (
        <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta>
      ) : (
        <DataTable
          columnas={COLUMNAS}
          data={usuarios}
          claveFila={(u) => u.id}
          cargando={cargando}
          estadoVacio="No hay usuarios registrados para esta sede."
        />
      )}
    </div>
  );
}
