import { useEffect, useState, useSyncExternalStore, type FormEvent } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Routes, Route, Navigate, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { api, errorText } from './api';
import { Users, Roles } from './administration';
import './styles.css';
import { Products, CatalogMasters, Inventory } from './catalog';
import { BusinessProfileProvider, BusinessSettings, useBusinessProfile } from './business-settings';
import { SessionGuard } from './session-guard';
import { SalesWorkspace } from './sales-workspace';
import { Cash } from './cash';
import { Sales } from './sales';
import { SidebarNavigation } from './navigation';

function Notice({ text }: { text: string }) { return text ? <p role="alert" className="notice">{text}</p> : null; }
function Protected({ permission, role }: { permission?: string; role?: string }) {
  const session = useSyncExternalStore(api.subscribe, api.snapshot);
  const location = useLocation();
  if (!session) return <Navigate to="/login" state={{ from: location.pathname }} replace />;
  if ((permission && !session.user.permissions.includes(permission)) || (role && !session.user.roles.includes(role))) return <section className="card"><h1>Acceso restringido</h1><p>Tu cuenta no tiene permiso para consultar esta sección.</p></section>;
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
  return <main className="login"><section className="intro"><span className="brand">N<span>EXO</span></span><p className="eyebrow">CONTROL QUE CONECTA</p><h1>Tu tienda,<br/>en un solo lugar.</h1><p>Una base clara para administrar tu operación y trabajar con confianza.</p><small>Inventario · Facturación · Gestión</small></section><section className="login-form card"><p className="eyebrow">BIENVENIDO A NEXO</p><h2>Iniciar sesión</h2><p>Terminal compartida: inicia sesión con tu propia cuenta. Al recargar o volver a abrir NEXO deberás ingresar nuevamente.</p><form onSubmit={submit}><label>Usuario<input name="username" autoComplete="username" required maxLength={80} autoFocus /></label><label>Contraseña<input name="password" type="password" autoComplete="current-password" required maxLength={128}/></label><Notice text={error}/><button disabled={busy}>{busy ? 'Ingresando…' : 'Entrar a NEXO →'}</button></form><small>Acceso seguro a través de la red de la tienda.</small></section></main>;
}
function Workspace() {
  const { profile, error: businessError, reload } = useBusinessProfile();
  const session = useSyncExternalStore(api.subscribe, api.snapshot)!;
  const [error, setError] = useState(''), [busy, setBusy] = useState(false);
  async function logout() { setBusy(true); setError(''); try { await api.logout(); } catch (e) { setError(errorText(e)); } finally { setBusy(false); } }
  return <div className="shell"><SessionGuard/><aside className="system-sidebar"><NavLink to="/" className="business-sidebar-name">{profile?.iconDataUrl && <img className="business-sidebar-icon" src={profile.iconDataUrl} alt=""/>}<span>{profile?.name ?? 'Mi tienda'}</span></NavLink><p className="business-sidebar-powered">Gestionado con <strong>NEXO</strong></p><SidebarNavigation user={session.user}/><div className="sidebar-bottom"><small>{session.user.roles.join(' · ') || 'Sin rol asignado'}</small><p>{session.user.username}</p><button className="secondary" onClick={logout} disabled={busy}>{busy ? 'Cerrando…' : 'Cerrar sesión / Cambiar usuario'}</button><Notice text={error}/></div></aside><div className="workspace"><header><span className="business-header-name">{profile?.iconDataUrl && <img className="business-header-icon" src={profile.iconDataUrl} alt=""/>}{profile?.name ?? 'Mi tienda'}</span><span className="badge">Sesión activa · {session.user.username}</span></header><main id="contenido">{businessError && <div className="notice" role="alert">No se pudo cargar la identidad del negocio. <button className="secondary" onClick={reload}>Reintentar</button></div>}<Outlet/></main><footer>{profile?.name ?? 'Mi tienda'} · Gestionado con NEXO</footer></div></div>;
}
function Layout() {
  const session = useSyncExternalStore(api.subscribe, api.snapshot)!;
  return <BusinessProfileProvider key={session.user.id}><Workspace/></BusinessProfileProvider>;
}
function Home() {
  const session = useSyncExternalStore(api.subscribe, api.snapshot)!;
  const { profile } = useBusinessProfile();
  const canConfigure = session.user.roles.includes('Administrador') && session.user.permissions.includes('configuracion.administrar');
  return <><p className="eyebrow">TU NEGOCIO · TU ESPACIO</p>{profile?.iconDataUrl && <img className="business-home-icon" src={profile.iconDataUrl} alt="Icono del negocio"/>}<h1 className="business-home-name">{profile?.name ?? 'Mi tienda'}</h1>{profile?.tagline && <p className="business-home-tagline">{profile.tagline}</p>}<p className="lead">Bienvenido, {session.user.name || session.user.username}.</p>
    <section className="card"><h2>Accesos de trabajo</h2><div className="business-quick-links">{session.user.permissions.includes('catalogo.leer') && <NavLink to="/productos" className="secondary link-button">Consultar productos →</NavLink>}{session.user.permissions.includes('inventario.leer') && <NavLink to="/inventario" className="secondary link-button">Abrir inventario →</NavLink>}{canConfigure && <NavLink to="/configuracion" className="secondary link-button">Personalizar mi negocio →</NavLink>}<NavLink to="/cuenta" className="secondary link-button">Mi cuenta →</NavLink></div></section><p className="business-home-powered">Tu operación, organizada con <strong>NEXO</strong>.</p></>;
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
  return <BrowserRouter><Routes><Route path="/login" element={<Login/>}/><Route element={<Protected/>}><Route element={<Layout/>}><Route index element={<Home/>}/><Route path="cuenta" element={<Account/>}/><Route path="pos" element={<SalesWorkspace/>}><Route element={<Protected permission="ventas.registrar"/>}><Route index element={null}/></Route><Route element={<Protected permission="cajas.leer"/>}><Route path="caja" element={<Cash/>}/></Route><Route element={<Protected permission="ventas.leer"/>}><Route path="ventas" element={<Sales/>}/></Route></Route><Route element={<Protected permission="configuracion.administrar" role="Administrador"/>}><Route path="configuracion" element={<BusinessSettings/>}/></Route><Route element={<Protected permission="usuarios.leer"/>}><Route path="usuarios" element={<Users/>}/></Route><Route element={<Protected permission="roles.leer"/>}><Route path="roles" element={<Roles/>}/></Route><Route element={<Protected permission="catalogo.leer"/>}><Route path="productos" element={<Products/>}/><Route path="categorias" element={<CatalogMasters kind="categorias"/>}/><Route path="subcategorias" element={<CatalogMasters kind="subcategorias"/>}/><Route path="ubicaciones" element={<CatalogMasters kind="ubicaciones"/>}/></Route><Route element={<Protected permission="proveedores.leer"/>}><Route path="proveedores" element={<CatalogMasters kind="proveedores"/>}/></Route><Route element={<Protected permission="inventario.leer"/>}><Route path="inventario" element={<Inventory/>}/></Route><Route path="*" element={<section className="card"><h1>Página no encontrada</h1><NavLink to="/">Volver al inicio</NavLink></section>}/></Route></Route></Routes></BrowserRouter>;
}
createRoot(document.getElementById('root')!).render(<App/>);
