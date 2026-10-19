import { useCallback, useEffect, useState } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { api, errorText } from './api';
import { can, type Shift } from './sales-shared';
import { SalesContext } from './sales-context';
import { Pos } from './pos';

// Rutas independientes; conservar el cobro montado evita perder carrito y UUID
// al abrir caja o consultar una venta dentro de esta sección de trabajo.
export function SalesWorkspace() {
  const { pathname } = useLocation();
  const [shifts, setShifts] = useState<Shift[]>([]), [error, setError] = useState(''), [revision, setRevision] = useState(0);
  const reload = useCallback(() => setRevision(r => r + 1), []);
  useEffect(() => {
    let live = true; setError('');
    if (can('cajas.leer')) api.request<Shift[]>('/turnos').then(rows => { if (live) setShifts(rows); }).catch(e => { if (live) setError(errorText(e)); });
    return () => { live = false; };
  }, [revision, pathname]);
  const own = shifts.find(s => s.state === 'ABIERTO' && s.userId === api.session?.user.id);
  const isPos = pathname.replace(/\/$/, '') === '/pos';
  return <SalesContext.Provider value={{ shifts, own, revision, reload }}>
    {error && <p role="alert" className="notice">{error}</p>}
    {can('ventas.registrar') && <div hidden={!isPos}><Pos/></div>}
    <Outlet/>
  </SalesContext.Provider>;
}
