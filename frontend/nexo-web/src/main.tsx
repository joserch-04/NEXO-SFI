import { useEffect, useState, useSyncExternalStore, type FormEvent } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Routes, Route, Navigate, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { api, ApiError, errorText } from './api';
import { Users, Roles } from './administration';
import './styles.css';

function Notice({ text }: { text: string }) { return text ? <p role="alert" className="notice">{text}</p> : null; }
function Protected({ permission }: { permission?: string }) {
  const session = useSyncExternalStore(api.subscribe, api.snapshot);
  const location = useLocation();
  if (!session) return <Navigate to="/login" state={{ from: location.pathname }} replace />;
  if (permission && !session.user.permissions.includes(permission)) return <section className="card"><h1>Acceso restringido</h1><p>Tu cuenta no tiene permiso para consultar esta sección.</p></section>;
  return <Outlet />;
}
function Login() {
  const session = useSyncExternalStore(api.subscribe, api.snapshot);
  const [busy, setBusy] = useState(false), [error, setError] = useState('');
  const location = useLocation();
  const from = location.state?.from;
  if (session) return <Navigate to={typeof from === 'string' && from.startsWith('/') && !from.startsWith('//') && from !== '/login' ? from : '/'} replace />;
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); const form = e.currentTarget; const data = new FormData(form); setBusy(true); setError('');
    try { await api.login(String(data.get('username')).trim(), String(data.get('password'))); form.reset(); }
    catch (err) { setError(errorText(err)); } finally { setBusy(false); }
  }
  return <main className="login"><section className="intro"><span className="brand">N<span>EXO</span></span><p className="eyebrow">CONTROL QUE CONECTA</p><h1>Tu tienda,<br/>en un solo lugar.</h1><p>Una base clara para administrar tu operación y trabajar con confianza.</p><small>Inventario · Facturación · Gestión</small></section><section className="login-form card"><p className="eyebrow">BIENVENIDO A NEXO</p><h2>Iniciar sesión</h2><p>Ingresa con la cuenta asignada por tu administrador.</p><form onSubmit={submit}><label>Usuario<input name="username" autoComplete="username" required maxLength={80} autoFocus /></label><label>Contraseña<input name="password" type="password" autoComplete="current-password" required maxLength={128}/></label><Notice text={error}/><button disabled={busy}>{busy ? 'Ingresando…' : 'Entrar a NEXO →'}</button></form><small>Acceso seguro a través de la red de la tienda.</small></section></main>;
}
function Layout() {
  const session = useSyncExternalStore(api.subscribe, api.snapshot)!;
  const [error, setError] = useState(''), [busy, setBusy] = useState(false);
  async function logout() { setBusy(true); setError(''); try { await api.logout(); } catch (e) { setError(errorText(e)); } finally { setBusy(false); } }
  return <div className="shell"><aside><a href="/" className="brand">N<span>EXO</span></a><p className="eyebrow">ESPACIO DE TRABAJO</p><nav aria-label="Principal"><NavLink to="/" end>◫ Inicio</NavLink>{session.user.permissions.includes('usuarios.leer') && <NavLink to="/usuarios">♙ Usuarios</NavLink>}{session.user.permissions.includes('roles.leer') && <NavLink to="/roles">♧ Roles y permisos</NavLink>}<NavLink to="/cuenta">⚙ Mi cuenta</NavLink></nav><div className="sidebar-bottom"><small>{session.user.roles.join(' · ') || 'Sin rol asignado'}</small><p>{session.user.username}</p><button className="secondary" onClick={logout} disabled={busy}>{busy ? 'Cerrando…' : 'Cerrar sesión'}</button><Notice text={error}/></div></aside><div className="workspace"><header><span>Gestión de tienda</span><span className="badge">Sesión activa · {session.user.username}</span></header><main id="contenido"><Outlet/></main><footer>NEXO · Sistema de inventario y facturación</footer></div></div>;
}
function Home() {
  const session = useSyncExternalStore(api.subscribe, api.snapshot)!;
  return <><p className="eyebrow">VISTA GENERAL</p><h1>Todo comienza con una buena conexión.</h1><p className="lead">Bienvenido, {session.user.name || session.user.username}. Este es tu espacio de trabajo.</p><section className="hero"><span className="badge">Frontend base · Fase 5</span><h2>Una base lista para crecer contigo.</h2><p>Consulta tu acceso y administra la seguridad de tu cuenta desde un mismo lugar.</p><NavLink className="link-button" to="/cuenta">Ver mi cuenta →</NavLink></section><div className="grid md:grid-cols-3 gap-5 mt-6"><article className="card"><p className="eyebrow">01 · IDENTIDAD</p><h2>Tu sesión</h2><p>Acceso autenticado y renovación segura.</p></article><article className="card"><p className="eyebrow">02 · ACCESO</p><h2>{session.user.roles.length} roles asignados</h2><p>{session.user.roles.join(', ') || 'Sin roles activos'}</p></article><article className="card"><p className="eyebrow">03 · PRÓXIMAS ETAPAS</p><h2>Catálogo y operación</h2><p>Inventario, ventas y reportes se incorporarán en las siguientes fases.</p></article></div></>;
}
function Account() {
  const session = useSyncExternalStore(api.subscribe, api.snapshot)!;
  const [error, setError] = useState(''), [busy, setBusy] = useState(false);
  const navigate = useNavigate();
  async function change(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); const form = e.currentTarget, data = new FormData(form);
    const newPassword = String(data.get('newPassword'));
    if (newPassword !== data.get('confirm')) { setError('Las contraseñas nuevas no coinciden.'); return; }
    if (!/[\p{L}]/u.test(newPassword) || !/\p{Nd}/u.test(newPassword)) { setError('Incluye al menos una letra y un número.'); return; }
    setBusy(true); setError('');
    try { await api.request(`/usuarios/${session.user.id}/cambio-contrasena`, { method: 'POST', body: JSON.stringify({ currentPassword: data.get('currentPassword'), newPassword }) }); form.reset(); api.clear(); navigate('/login', { replace: true }); }
    catch (err) { setError(errorText(err)); } finally { setBusy(false); }
  }
  return <><p className="eyebrow">SEGURIDAD PERSONAL</p><h1>Mi cuenta</h1><div className="grid lg:grid-cols-2 gap-6"><section className="card"><h2>{session.user.name || session.user.username}</h2><p>@{session.user.username}</p><h3>Roles</h3><p>{session.user.roles.join(', ') || 'Sin roles'}</p><h3>Permisos de la sesión</h3><ul>{session.user.permissions.map(p => <li key={p}>{p}</li>)}</ul><p className="mt-4 text-sm">El servidor comprueba los permisos vigentes en cada solicitud.</p></section>{session.user.roles.includes('Administrador') && session.user.permissions.includes('usuarios.administrar') ? <section className="card"><h2>Cambiar contraseña</h2><p>Al guardar se cerrarán todas tus sesiones. Vuelve a ingresar con la nueva contraseña.</p><form onSubmit={change}><label>Contraseña actual<input name="currentPassword" type="password" autoComplete="current-password" required maxLength={128}/></label><label>Nueva contraseña<input name="newPassword" type="password" autoComplete="new-password" required minLength={12} maxLength={128} aria-describedby="password-help"/></label><small id="password-help">Entre 12 y 128 caracteres, con letras y números.</small><label>Confirmar nueva contraseña<input name="confirm" type="password" autoComplete="new-password" required minLength={12} maxLength={128}/></label><Notice text={error}/><button disabled={busy}>{busy ? 'Guardando…' : 'Guardar contraseña'}</button></form></section> : <section className="card"><h2>Contraseña administrada</h2><p>Para cambiar tu contraseña, solicita autorización al administrador. Solo él puede realizar el cambio.</p></section>}</div></>;
}
function App() {
  const [ready, setReady] = useState(false), [error, setError] = useState('');
  async function restore() { setError(''); try { await api.refresh(); setReady(true); } catch (e) { if (e instanceof ApiError && e.status === 401) setReady(true); else setError(errorText(e)); } }
  useEffect(() => { void restore(); }, []);
  if (!ready) return <main className="startup card"><h1>NEXO</h1>{error ? <><Notice text={error}/><button onClick={restore}>Reintentar conexión</button><button className="secondary" onClick={() => setReady(true)}>Ir al inicio de sesión</button></> : <p role="status">Comprobando sesión…</p>}</main>;
  return <BrowserRouter><Routes><Route path="/login" element={<Login/>}/><Route element={<Protected/>}><Route element={<Layout/>}><Route index element={<Home/>}/><Route path="cuenta" element={<Account/>}/><Route element={<Protected permission="usuarios.leer"/>}><Route path="usuarios" element={<Users/>}/></Route><Route element={<Protected permission="roles.leer"/>}><Route path="roles" element={<Roles/>}/></Route><Route path="*" element={<section className="card"><h1>Página no encontrada</h1><NavLink to="/">Volver al inicio</NavLink></section>}/></Route></Route></Routes></BrowserRouter>;
}
createRoot(document.getElementById('root')!).render(<App/>);
