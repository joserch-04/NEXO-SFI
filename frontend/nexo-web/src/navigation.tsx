import { NavLink } from 'react-router-dom';
import { useState } from 'react';
import type { Identity } from './api';

export function SidebarNavigation({ user }: { user: Identity }) {
  const [expanded, setExpanded] = useState(false);
  const has = (permission: string) => user.permissions.includes(permission);
  const sales = has('ventas.registrar') || has('cajas.leer') || has('ventas.leer');
  const configure = user.roles.includes('Administrador') && has('configuracion.administrar');
  return <><button type="button" className="mobile-navigation-toggle" aria-expanded={expanded} aria-controls="principal-navigation" onClick={() => setExpanded(value => !value)}>{expanded ? 'Cerrar menú' : 'Menú del sistema'}<span aria-hidden="true">{expanded ? '−' : '+'}</span></button><nav id="principal-navigation" className={'system-navigation' + (expanded ? ' is-expanded' : '')} aria-label="Principal" onClick={event => { if ((event.target as HTMLElement).closest('a')) setExpanded(false); }}>
    <section className="nav-section" aria-labelledby="nav-general"><h2 id="nav-general">General</h2><NavLink to="/" end>Inicio</NavLink></section>
    {sales && <section className="nav-section" aria-labelledby="nav-sales"><h2 id="nav-sales">Punto de venta</h2>
      {has('ventas.registrar') && <NavLink to="/pos" end>Punto de venta</NavLink>}
      <div className="nav-children">{has('cajas.leer') && <NavLink to="/pos/caja">Caja y turnos</NavLink>}{has('ventas.leer') && <NavLink to="/pos/ventas">Ventas y comprobantes</NavLink>}</div>
    </section>}
    {(has('catalogo.leer') || has('proveedores.leer')) && <section className="nav-section" aria-labelledby="nav-catalog"><h2 id="nav-catalog">Catálogo</h2>
      {has('catalogo.leer') && <><NavLink to="/productos">Productos</NavLink><NavLink to="/categorias">Categorías</NavLink><NavLink to="/subcategorias">Subcategorías</NavLink></>}
      {has('proveedores.leer') && <NavLink to="/proveedores">Proveedores</NavLink>}
    </section>}
    {(has('inventario.leer') || has('catalogo.leer')) && <section className="nav-section" aria-labelledby="nav-inventory"><h2 id="nav-inventory">Inventario</h2>
      {has('inventario.leer') && <NavLink to="/inventario">Existencias y movimientos</NavLink>}{has('catalogo.leer') && <NavLink to="/ubicaciones">Ubicaciones</NavLink>}
    </section>}
    {(has('usuarios.leer') || has('roles.leer') || configure) && <section className="nav-section" aria-labelledby="nav-administration"><h2 id="nav-administration">Administración</h2>
      {has('usuarios.leer') && <NavLink to="/usuarios">Usuarios</NavLink>}{has('roles.leer') && <NavLink to="/roles">Roles y permisos</NavLink>}{configure && <NavLink to="/configuracion">Configuración</NavLink>}
    </section>}
    <section className="nav-section" aria-labelledby="nav-personal"><h2 id="nav-personal">Personal</h2><NavLink to="/cuenta">Mi cuenta</NavLink></section>
  </nav></>;
}
