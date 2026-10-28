import { Outlet, Navigate, useLocation } from 'react-router-dom';
import { InventoryOperationsWorkspace, permitted } from './inventory-operations';
import { InventoryCounts } from './inventory-counts';
// Se conservan borradores y claves de reintento al consultar el historial o ubicaciones.
// Permanecen únicamente en memoria y se destruyen al salir de Inventario o cerrar sesión.
export function InventoryWorkspace() {
  const path = useLocation().pathname;
  const operations = path.startsWith('/inventario/operaciones');
  const counts = path.endsWith('/conteos');
  const canOperate = permitted('catalogo.leer') && (permitted('inventario.entradas') || permitted('inventario.ajustar'));
  return <div className="inventory-module">{canOperate && <div hidden={!operations}><InventoryOperationsWorkspace/></div>}<div hidden={!counts}><InventoryCounts active={counts}/></div><Outlet/></div>;
}
export function InventoryOperationsIndex() {
  return <Navigate to={permitted('inventario.entradas') ? 'entradas' : 'salidas'} replace/>;
}
