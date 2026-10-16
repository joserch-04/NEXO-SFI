import { useEffect, useState, useSyncExternalStore, type FormEvent } from 'react';
import { api, errorText, type User } from './api';

interface Role { id: number; name: string; active: boolean }
interface Permission { id: number; code: string; description: string }
const json = (method: string, body: unknown): RequestInit => ({ method, body: JSON.stringify(body) });
const useIdentity = () => useSyncExternalStore(api.subscribe, api.snapshot)!.user;
function Alert({ error }: { error: string }) { return error ? <p role="alert" className="notice">{error}</p> : null; }
function Check({ label, checked, onChange, disabled = false }: { label: string; checked: boolean; onChange: () => void; disabled?: boolean }) {
  return <label className="flex items-center gap-3"><input className="w-4 h-4 m-0" type="checkbox" checked={checked} onChange={onChange} disabled={disabled}/>{label}</label>;
}

export function Users() {
  const identity = useIdentity();
  const manage = identity.permissions.includes('usuarios.administrar');
  const assign = identity.permissions.includes('roles.asignar') && identity.permissions.includes('roles.leer') && identity.roles.includes('Administrador');
  const [users, setUsers] = useState<User[]>([]);
  const [page, setPage] = useState(1), [reload, setReload] = useState(0);
  const [loading, setLoading] = useState(true), [error, setError] = useState(''), [message, setMessage] = useState('');
  const [editor, setEditor] = useState<User | 'new' | null>(null);
  const [roleUser, setRoleUser] = useState<User | null>(null);
  const [passwordUser, setPasswordUser] = useState<User | null>(null);
  useEffect(() => {
    let live = true; setLoading(true); setError('');
    api.request<User[]>(`/usuarios?page=${page}&pageSize=25`).then(value => { if (live) setUsers(value); })
      .catch(e => { if (live) setError(errorText(e)); }).finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [page, reload]);
  function updated(text: string) { setMessage(text); setReload(x => x + 1); }
  return <>
    <p className="eyebrow">ADMINISTRACIÓN</p><div className="flex justify-between gap-4 flex-wrap"><h1>Usuarios</h1>{manage && <button onClick={() => { setEditor('new'); setRoleUser(null); setPasswordUser(null); setMessage(''); }}>Crear usuario</button>}</div>
    <p className="lead">Crea cuentas para cajeros y personal de consulta. Los roles determinan sus permisos.</p>
    {message && <p role="status" className="card mb-5">{message}</p>}
    {editor && <UserEditor key={editor === 'new' ? 'new' : editor.id} user={editor === 'new' ? null : editor} onCancel={() => setEditor(null)} onSaved={(saved, created) => {
      setEditor(null); updated(created ? 'Cuenta creada. Asigna sus roles para completar el acceso.' : 'Cuenta actualizada. Sus sesiones anteriores fueron revocadas.');
      if (created && assign) setRoleUser(saved);
    }}/>}
    {roleUser && <UserRoles key={roleUser.id} user={roleUser} onCancel={() => setRoleUser(null)} onSaved={() => { setRoleUser(null); updated('Roles asignados. El usuario debe iniciar sesión nuevamente.'); }}/>}
    {passwordUser && <AdminPassword key={passwordUser.id} user={passwordUser} onClose={() => setPasswordUser(null)} onSaved={() => { setPasswordUser(null); updated('Contraseña cambiada por el administrador. Las sesiones del usuario fueron cerradas.'); }}/>}
    <section className="card"><Alert error={error}/>{error ? <button onClick={() => setReload(x => x + 1)}>Reintentar</button> : loading ? <p role="status">Cargando usuarios…</p> : users.length === 0 ? <p>No hay usuarios en esta página.</p> :
      <div className="overflow-x-auto"><table><thead><tr><th>Nombre</th><th>Usuario</th><th>Estado</th><th>Acciones</th></tr></thead><tbody>{users.map(user => <tr key={user.id}>
        <td>{user.name}</td><td>{user.username}</td><td><span className="badge">{user.active ? 'Activo' : 'Inactivo'}</span></td><td><div className="flex gap-2 flex-wrap">
          {manage && <button className="secondary" onClick={() => { setEditor(user); setRoleUser(null); setPasswordUser(null); }}>Editar</button>}
          {assign && user.id !== identity.id && <button className="secondary" onClick={() => { setRoleUser(user); setEditor(null); setPasswordUser(null); }}>Roles</button>}
          {manage && identity.roles.includes('Administrador') && user.id !== identity.id && <button className="secondary" onClick={() => { setPasswordUser(user); setEditor(null); setRoleUser(null); }}>Cambiar contraseña</button>}
          {!manage && !assign && <span>Solo lectura</span>}
        </div></td>
      </tr>)}</tbody></table></div>}
      <div className="pagination"><button className="secondary" disabled={loading || page === 1} onClick={() => setPage(p => p - 1)}>Anterior</button><span>Página {page}</span><button className="secondary" disabled={loading || !!error || users.length < 25} onClick={() => setPage(p => p + 1)}>Siguiente</button></div>
    </section>
  </>;
}

function UserEditor({ user, onCancel, onSaved }: { user: User | null; onCancel: () => void; onSaved: (user: User, created: boolean) => void }) {
  const identity = useIdentity();
  const [active, setActive] = useState(user?.active ?? true), [busy, setBusy] = useState(false), [error, setError] = useState('');
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const form = event.currentTarget, data = new FormData(form);
    const password = String(data.get('password') ?? '');
    if (!user && (!/\p{L}/u.test(password) || !/\p{Nd}/u.test(password))) { setError('La contraseña debe incluir letras y números.'); return; }
    const self = user?.id === identity.id;
    if (user && !window.confirm(self ? 'Guardar los cambios cerrará tu sesión. ¿Continuar?' : 'Se actualizará la cuenta y se cerrarán sus sesiones. ¿Continuar?')) return;
    setBusy(true); setError('');
    try {
      const payload = { username: String(data.get('username')).trim(), name: String(data.get('name')).trim(), active, ...(!user ? { password } : {}) };
      const saved = await api.request<User>(user ? `/usuarios/${user.id}` : '/usuarios', json(user ? 'PUT' : 'POST', payload));
      form.reset(); if (self) api.clear(); else onSaved(saved, !user);
    } catch (e) { setError(errorText(e)); } finally { setBusy(false); }
  }
  return <section className="card mb-6"><h2>{user ? `Editar a ${user.username}` : '1. Crear cuenta'}</h2><form onSubmit={submit}><fieldset disabled={busy}>
    <div className="grid md:grid-cols-2 gap-x-6"><label>Nombre completo<input name="name" defaultValue={user?.name} required maxLength={160}/></label><label>Usuario<input name="username" defaultValue={user?.username} required pattern="[a-zA-Z0-9._\-]{3,80}" minLength={3} maxLength={80} autoComplete="off"/><small>De 3 a 80 letras, números, punto, guion o guion bajo.</small></label></div>
    {!user && <label>Contraseña inicial<input name="password" type="password" autoComplete="new-password" required minLength={12} maxLength={128}/><small>Entre 12 y 128 caracteres, con letras y números. Los cambios posteriores los realiza el administrador.</small></label>}
    <Check label="Cuenta activa" checked={active} onChange={() => setActive(x => !x)}/>
    <Alert error={error}/><div className="flex gap-3"><button>{busy ? 'Guardando…' : user ? 'Guardar cambios' : 'Crear cuenta y continuar'}</button><button className="secondary" type="button" onClick={onCancel}>Cancelar</button></div>
    </fieldset></form></section>;
}

function AdminPassword({ user, onClose, onSaved }: { user: User; onClose: () => void; onSaved: () => void }) {
  const [busy, setBusy] = useState(false), [error, setError] = useState('');
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const form = event.currentTarget, data = new FormData(form);
    const password = String(data.get('newPassword'));
    if (password !== data.get('confirm')) { setError('Las contraseñas no coinciden.'); return; }
    if (!/\p{L}/u.test(password) || !/\p{Nd}/u.test(password)) { setError('Incluye letras y números.'); return; }
    if (!window.confirm(`¿Cambiar la contraseña de ${user.username}? Se cerrarán todas sus sesiones.`)) return;
    setBusy(true); setError('');
    try { await api.request(`/usuarios/${user.id}/cambio-contrasena`, json('POST', { currentPassword: null, newPassword: password })); form.reset(); onSaved(); }
    catch (e) { setError(errorText(e)); } finally { setBusy(false); }
  }
  return <section className="card mb-6"><h2>Cambiar contraseña de {user.username}</h2><p>El administrador autoriza y realiza este cambio. No necesitas la contraseña anterior del usuario.</p><form onSubmit={submit}><fieldset disabled={busy}>
    <label>Nueva contraseña<input name="newPassword" type="password" autoComplete="new-password" required minLength={12} maxLength={128}/><small>Entre 12 y 128 caracteres, con letras y números.</small></label>
    <label>Confirmar contraseña<input name="confirm" type="password" autoComplete="new-password" required minLength={12} maxLength={128}/></label><Alert error={error}/>
    <div className="flex gap-3"><button>{busy ? 'Guardando…' : 'Confirmar cambio'}</button><button type="button" className="secondary" onClick={onClose}>Cancelar</button></div>
  </fieldset></form></section>;
}

function UserRoles({ user, onCancel, onSaved }: { user: User; onCancel: () => void; onSaved: () => void }) {
  const [roles, setRoles] = useState<Role[]>([]), [ids, setIds] = useState<number[]>([]);
  const [loading, setLoading] = useState(true), [busy, setBusy] = useState(false), [error, setError] = useState(''), [reload, setReload] = useState(0);
  useEffect(() => {
    let live = true; setLoading(true); setError('');
    Promise.all([api.request<Role[]>('/roles'), api.request<string[]>(`/usuarios/${user.id}/roles`)]).then(([all, names]) => {
      if (live) { setRoles(all.filter(r => r.active)); setIds(all.filter(r => r.active && names.includes(r.name)).map(r => r.id)); }
    }).catch(e => { if (live) setError(errorText(e)); }).finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [user.id, reload]);
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!window.confirm(`¿Guardar los roles de ${user.username}? Se cerrarán sus sesiones.${ids.length === 0 ? ' La cuenta quedará sin permisos.' : ''}`)) return;
    setBusy(true); setError('');
    try { await api.request(`/usuarios/${user.id}/roles`, json('PUT', { ids })); onSaved(); }
    catch (e) { setError(errorText(e)); } finally { setBusy(false); }
  }
  return <section className="card mb-6"><h2>2. Roles de {user.username}</h2><p>Vendedor/Cajero para caja y ventas; Consulta para lectura. Los permisos de todos los roles asignados se suman.</p>
    <Alert error={error}/>{loading ? <p role="status">Cargando roles…</p> : roles.length === 0 ? <button onClick={() => setReload(x => x + 1)}>Reintentar carga</button> : <form onSubmit={submit}><fieldset disabled={busy}>{roles.map(role => <Check key={role.id} label={role.name} checked={ids.includes(role.id)} onChange={() => setIds(old => old.includes(role.id) ? old.filter(id => id !== role.id) : [...old, role.id])}/>)}<div className="flex gap-3"><button>{busy ? 'Guardando…' : 'Guardar roles'}</button><button type="button" className="secondary" onClick={onCancel}>Cerrar</button></div></fieldset></form>}
  </section>;
}

export function Roles() {
  const identity = useIdentity();
  const editable = identity.roles.includes('Administrador') && identity.permissions.includes('roles.administrar');
  const [roles, setRoles] = useState<Role[]>([]), [permissions, setPermissions] = useState<Permission[]>([]), [roleId, setRoleId] = useState<number | null>(null);
  const [ids, setIds] = useState<number[]>([]), [loading, setLoading] = useState(true), [detailLoading, setDetailLoading] = useState(false), [busy, setBusy] = useState(false);
  const [error, setError] = useState(''), [detailError, setDetailError] = useState(''), [message, setMessage] = useState(''), [reload, setReload] = useState(0);
  useEffect(() => {
    let live = true; setLoading(true); setError('');
    Promise.all([api.request<Role[]>('/roles'), api.request<Permission[]>('/permisos')]).then(([r, p]) => { if (live) { setRoles(r); setPermissions(p); setRoleId(r[0]?.id ?? null); } })
      .catch(e => { if (live) setError(errorText(e)); }).finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [reload]);
  useEffect(() => {
    if (roleId === null) return;
    let live = true; setDetailLoading(true); setDetailError(''); setMessage(''); setIds([]);
    api.request<number[]>(`/roles/${roleId}/permisos`).then(value => { if (live) setIds(value); })
      .catch(e => { if (live) setDetailError(errorText(e)); }).finally(() => { if (live) setDetailLoading(false); });
    return () => { live = false; };
  }, [roleId, reload]);
  const role = roles.find(r => r.id === roleId);
  const canSave = editable && role?.name !== 'Administrador';
  async function submit(event: FormEvent) {
    event.preventDefault(); if (!role || !canSave || !window.confirm(`¿Actualizar los permisos de ${role.name}? El cambio afectará a todos sus usuarios.`)) return;
    setBusy(true); setDetailError(''); setMessage('');
    try { await api.request(`/roles/${role.id}/permisos`, json('PUT', { ids })); setMessage('Permisos guardados. Se aplican en la siguiente solicitud de cada usuario.'); }
    catch (e) { setDetailError(errorText(e)); } finally { setBusy(false); }
  }
  return <><p className="eyebrow">CONTROL DE ACCESO</p><h1>Roles y permisos</h1><p className="lead">Define qué puede hacer cada rol. Los usuarios reciben estos permisos al asignarles sus roles.</p>
    <section className="card"><Alert error={error}/>{error ? <button onClick={() => setReload(x => x + 1)}>Reintentar</button> : loading ? <p role="status">Cargando catálogo…</p> : <>
      <label>Rol<select className="block border rounded-lg p-3 mt-2 w-full" value={roleId ?? ''} disabled={busy} onChange={e => { setDetailLoading(true); setRoleId(Number(e.target.value)); }}>{roles.map(r => <option key={r.id} value={r.id}>{r.name}{r.active ? '' : ' (inactivo)'}</option>)}</select></label>
      {role?.name === 'Administrador' && <p className="mt-4">El rol Administrador está protegido y sus permisos no se pueden modificar.</p>}
      <Alert error={detailError}/>{message && <p role="status" className="my-4">{message}</p>}
      {detailLoading ? <p role="status">Cargando permisos del rol…</p> : detailError ? <button onClick={() => setReload(x => x + 1)}>Recargar permisos</button> : <form onSubmit={submit}><fieldset disabled={busy || !canSave}><div className="grid md:grid-cols-2 gap-x-8">{permissions.map(p => <Check key={p.id} label={`${p.description} (${p.code})`} checked={ids.includes(p.id)} onChange={() => setIds(old => old.includes(p.id) ? old.filter(id => id !== p.id) : [...old, p.id])}/>)}</div>{canSave && <button>{busy ? 'Guardando…' : 'Guardar permisos'}</button>}</fieldset></form>}
    </>}</section></>;
}
