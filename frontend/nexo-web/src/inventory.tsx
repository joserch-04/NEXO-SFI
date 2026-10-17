import { useEffect, useRef, useState, type FormEvent, type ReactNode } from 'react';
import { api, errorText } from './api';
import type { Master, Movement, Product, Stock } from './catalog';

type View = 'existencias' | 'movimientos' | 'alertas';
type MoveType = 'ENTRADA' | 'SALIDA' | 'AJUSTE' | 'TRASLADO';
interface Draft { productId: string; locationId: string; destinationId: string; quantity: string; reason: string }
const emptyDraft = (): Draft => ({ productId: '', locationId: '', destinationId: '', quantity: '', reason: '' });
const labels: Record<MoveType, string> = { ENTRADA: 'Entrada', SALIDA: 'Salida', AJUSTE: 'Ajuste', TRASLADO: 'Traslado' };
const paths: Record<MoveType, string> = { ENTRADA: 'entradas', SALIDA: 'salidas', AJUSTE: 'ajustes', TRASLADO: 'traslados' };
const allowed = (p: string) => api.session?.user.permissions.includes(p) ?? false;

// El dialog nativo contiene el foco, permite Escape y devuelve el foco al disparador.
// Permanece montado al cerrarse para conservar el borrador y su clave de reintento.
function SidePanel({ open, title, onClose, busy = false, children }: { open: boolean; title: string; onClose: () => void; busy?: boolean; children: ReactNode }) {
  const dialog = useRef<HTMLDialogElement>(null);
  useEffect(() => { const node = dialog.current!; if (open && !node.open) node.showModal(); else if (!open && node.open) node.close(); }, [open]);
  return <dialog ref={dialog} className="inventory-panel" aria-label={title}
    onCancel={e => { e.preventDefault(); if (!busy) onClose(); }}
    onClick={e => { if (e.target === e.currentTarget && !busy) { const r = e.currentTarget.getBoundingClientRect(); if (e.clientX < r.left || e.clientX > r.right || e.clientY < r.top || e.clientY > r.bottom) onClose(); } }}>
    <div className="inventory-panel-head"><h2>{title}</h2><button type="button" className="secondary" aria-label={'Cerrar ' + title} disabled={busy} onClick={onClose}>Cerrar ×</button></div>
    <div className="inventory-panel-body">{children}</div>
  </dialog>;
}

export function Inventory() {
  const [view, setView] = useState<View>('existencias');
  const [stocks, setStocks] = useState<Stock[]>([]), [low, setLow] = useState<Stock[]>([]), [movements, setMovements] = useState<Movement[]>([]), [locations, setLocations] = useState<Master[]>([]);
  const [search, setSearch] = useState(''), [locationId, setLocationId] = useState(''), [stockState, setStockState] = useState('');
  const [loading, setLoading] = useState(true), [historyLoading, setHistoryLoading] = useState(false), [message, setMessage] = useState(''), [historyError, setHistoryError] = useState('');
  const [revision, setRevision] = useState(0), [page, setPage] = useState(1), [detail, setDetail] = useState<Stock | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false), [draft, setDraft] = useState<Draft>(emptyDraft), [key, setKey] = useState(() => crypto.randomUUID());
  const [type, setType] = useState<MoveType>(allowed('inventario.entradas') ? 'ENTRADA' : 'SALIDA');
  const [busy, setBusy] = useState(false), [moveError, setMoveError] = useState(''), [productSearch, setProductSearch] = useState(''), [products, setProducts] = useState<Product[]>([]), [productsLoading, setProductsLoading] = useState(false);
  const [selectedProduct, setSelectedProduct] = useState<{ id: number; sku: string; name: string } | null>(null);
  const [dates, setDates] = useState(() => { const from = new Date(); from.setDate(from.getDate() - 30); return { from: from.toISOString().slice(0, 10), to: new Date().toISOString().slice(0, 10), type: '', productId: '' }; });
  const canCatalog = allowed('catalogo.leer');
  const moveTypes = (Object.keys(labels) as MoveType[]).filter(t => allowed(t === 'ENTRADA' ? 'inventario.entradas' : 'inventario.ajustar'));
  const canMove = canCatalog && moveTypes.length > 0;

  useEffect(() => {
    let live = true; setLoading(true); setMessage('');
    Promise.all([api.request<Stock[]>('/inventario/existencias'), api.request<Stock[]>('/inventario/stock-bajo'), canCatalog ? api.request<Master[]>('/ubicaciones') : Promise.resolve([])])
      .then(([s, l, u]) => { if (live) { setStocks(s); setLow(l); setLocations(u); } })
      .catch(e => { if (live) setMessage(errorText(e)); }).finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [revision, canCatalog]);

  useEffect(() => {
    if (!drawerOpen || !canCatalog) return;
    let live = true; setProductsLoading(true);
    const timer = window.setTimeout(() => {
      api.request<Product[]>('/productos?active=true&pageSize=25&search=' + encodeURIComponent(productSearch))
        .then(p => { if (live) setProducts(p.filter(x => x.trackInventory)); })
        .catch(e => { if (live) setMoveError(errorText(e)); }).finally(() => { if (live) setProductsLoading(false); });
    }, 200);
    return () => { live = false; window.clearTimeout(timer); };
  }, [productSearch, drawerOpen, revision, canCatalog]);

  useEffect(() => {
    if (view !== 'movimientos') return;
    let live = true; setHistoryLoading(true); setHistoryError('');
    const end = new Date(dates.to + 'T00:00:00Z'); end.setUTCDate(end.getUTCDate() + 1);
    const q = new URLSearchParams({ from: dates.from + 'T00:00:00Z', to: end.toISOString(), page: String(page), pageSize: '25' });
    if (dates.type) q.set('type', dates.type); if (dates.productId) q.set('productId', dates.productId);
    api.request<Movement[]>('/inventario/movimientos?' + q).then(m => { if (live) setMovements(m); })
      .catch(e => { if (live) setHistoryError(errorText(e)); }).finally(() => { if (live) setHistoryLoading(false); });
    return () => { live = false; };
  }, [view, dates, page, revision]);

  function openMovement(stock?: Stock) {
    if (stock) {
      // Una acción explícita sobre otro producto inicia una operación nueva.
      setDraft({ ...emptyDraft(), productId: String(stock.productId), locationId: stock.locationId ? String(stock.locationId) : '' });
      setSelectedProduct({ id: stock.productId, sku: stock.sku, name: stock.name }); setProductSearch(''); setKey(crypto.randomUUID()); setMoveError('');
    }
    setDetail(null); setDrawerOpen(true);
  }
  function newMovement() { setDraft(emptyDraft()); setSelectedProduct(null); setProductSearch(''); setKey(crypto.randomUUID()); setMoveError(''); }
  async function move(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); setBusy(true); setMoveError('');
    const value = { key, type, productId: Number(draft.productId), locationId: Number(draft.locationId), destinationId: type === 'TRASLADO' ? Number(draft.destinationId) : null, quantity: Number(draft.quantity), reason: draft.reason };
    try {
      const result = await api.request<{ id: number }>('/inventario/' + paths[type], { method: 'POST', body: JSON.stringify(value) });
      newMovement(); setDrawerOpen(false); setPage(1); setRevision(x => x + 1);
      // El mensaje queda fuera del panel y sobrevive a la actualización del listado.
      setSuccess('Operación #' + result.id + ' registrada.');
    } catch (err) { setMoveError(errorText(err)); } finally { setBusy(false); }
  }
  const [success, setSuccess] = useState('');
  const matching = (s: Stock) => (s.name + ' ' + s.sku).toLocaleLowerCase().includes(search.trim().toLocaleLowerCase());
  const lowIds = new Set(low.map(s => s.productId));
  const shown = stocks.filter(s => matching(s) && (!locationId || s.locationId === Number(locationId)) && (!stockState || (stockState === 'inactive' ? !s.active : s.active && (stockState === 'empty' ? s.quantity === 0 : lowIds.has(s.productId)))));
  const uniqueProducts = [...new Map(stocks.map(s => [s.productId, s])).values()];
  const availableLocations = canCatalog ? locations.map(l => ({ id: l.id, name: l.code + ' · ' + l.name })) : [...new Map(stocks.filter(s => s.locationId).map(s => [s.locationId, { id: s.locationId, name: s.location }])).values()];
  const detailStocks = detail ? stocks.filter(s => s.productId === detail.productId) : [];
  const detailTotal = detailStocks.reduce((total, s) => total + s.quantity, 0);
  const detailStatus = !detail?.active ? 'Inactivo' : detailTotal === 0 ? 'Agotado' : detailTotal <= (detail?.minimumStock ?? 0) ? 'Stock bajo' : 'Disponible';
  const productOptions = selectedProduct && !products.some(p => p.id === selectedProduct.id) ? [selectedProduct, ...products] : products;

  return <div className="inventory-workbench">
    <div className="inventory-heading"><div><h1>Inventario</h1><p>Consulta existencias y registra movimientos de la tienda.</p></div><div className="inventory-actions"><button type="button" className="secondary" disabled={loading || historyLoading || busy} onClick={() => { setSuccess(''); setRevision(x => x + 1); }}>Actualizar</button>{canMove && <button type="button" onClick={() => openMovement()}>+ Registrar movimiento</button>}</div></div>
    {message && <p role="alert" className="notice">{message}</p>}{success && <p role="status" className="inventory-success">{success}</p>}
    <section className="inventory-surface">
      <div className="inventory-tabs" aria-label="Vistas de inventario">
        {([['existencias', 'Existencias'], ['movimientos', 'Movimientos'], ['alertas', 'Alertas']] as const).map(([id, title]) => <button key={id} type="button" aria-pressed={view === id} aria-controls="inventory-content" onClick={() => setView(id)}>{title}{id === 'alertas' && <span className={low.length ? 'inventory-count has-alerts' : 'inventory-count'}>{loading ? '…' : low.length}</span>}</button>)}
      </div>
      <div id="inventory-content" className="inventory-content" aria-busy={view === 'movimientos' ? historyLoading : loading}>
        {view === 'existencias' && <>
          <div className="inventory-toolbar"><label>Buscar producto<input value={search} onChange={e => setSearch(e.target.value)} placeholder="Nombre o SKU" maxLength={180}/></label><label>Ubicación<select value={locationId} onChange={e => setLocationId(e.target.value)}><option value="">Todas las ubicaciones</option>{availableLocations.map(l => <option key={l.id} value={l.id}>{l.name}</option>)}</select></label><label>Estado<select value={stockState} onChange={e => setStockState(e.target.value)}><option value="">Todos los estados</option><option value="low">Stock bajo total</option><option value="empty">Agotado en ubicación</option><option value="inactive">Producto inactivo</option></select></label>{(search || locationId || stockState) && <button type="button" className="secondary" onClick={() => { setSearch(''); setLocationId(''); setStockState(''); }}>Limpiar</button>}</div>
          <div className="inventory-list-caption"><h2>Existencias por ubicación</h2><span>{shown.length} registros</span></div>
          <div className="table-scroll"><table><thead><tr><th>Producto</th><th>Ubicación</th><th>Cantidad</th><th>Estado del producto</th><th><span className="sr-only">Acciones</span></th></tr></thead><tbody>{shown.map(s => <tr key={s.productId + ':' + s.locationId}><td><strong>{s.name}</strong><small className="block">{s.sku}</small></td><td>{s.location}</td><td className="inventory-quantity">{s.quantity}</td><td><span className={'inventory-status ' + (!s.active ? 'is-muted' : lowIds.has(s.productId) ? 'is-warning' : '')}>{!s.active ? 'Inactivo' : lowIds.has(s.productId) ? 'Stock bajo total' : 'Disponible'}</span></td><td><button className="secondary" onClick={() => setDetail(s)}>Ver detalle</button></td></tr>)}</tbody></table></div>
          {!shown.length && <p className="inventory-empty">{loading ? 'Cargando existencias…' : 'No hay existencias con estos filtros.'}</p>}
        </>}
        {view === 'alertas' && <>
          <div className="inventory-list-caption"><h2>Productos que requieren atención</h2><span>{low.length} alertas</span></div><p className="inventory-hint">El mínimo se compara con el total de la tienda. Se excluyen productos inactivos y sin control de inventario.</p>
          <div className="table-scroll"><table><thead><tr><th>Producto</th><th>Total disponible</th><th>Mínimo</th><th>Alerta</th><th><span className="sr-only">Acciones</span></th></tr></thead><tbody>{low.map(s => <tr key={s.productId}><td><strong>{s.name}</strong><small className="block">{s.sku}</small></td><td className="inventory-quantity">{s.quantity}</td><td>{s.minimumStock}</td><td><span className={'inventory-status ' + (s.quantity === 0 ? 'is-danger' : 'is-warning')}>{s.quantity === 0 ? 'Agotado' : 'Stock bajo'}</span></td><td><button className="secondary" onClick={() => setDetail(s)}>Ver detalle</button></td></tr>)}</tbody></table></div>
          {!low.length && <p className="inventory-empty">{loading ? 'Cargando alertas…' : 'No hay productos agotados ni con stock bajo.'}</p>}
        </>}
        {view === 'movimientos' && <>
          <div className="inventory-list-caption"><h2>Historial de movimientos</h2><span>{dates.from} — {dates.to} · UTC</span></div>
          <details className="inventory-filter-disclosure"><summary>Filtros de movimientos{(dates.type || dates.productId) && ' · filtros activos'}</summary><form className="inventory-toolbar" onSubmit={e => { e.preventDefault(); const d = new FormData(e.currentTarget); setDates({ from: String(d.get('from')), to: String(d.get('to')), type: String(d.get('type')), productId: String(d.get('productId')) }); setPage(1); }}><label>Desde (UTC)<input name="from" type="date" required defaultValue={dates.from}/></label><label>Hasta (UTC, inclusive)<input name="to" type="date" required defaultValue={dates.to}/></label><label>Tipo<select name="type" defaultValue={dates.type}><option value="">Todos</option>{(Object.keys(labels) as MoveType[]).map(t => <option key={t} value={t}>{labels[t]}</option>)}</select></label><label>Producto<select name="productId" defaultValue={dates.productId}><option value="">Todos</option>{uniqueProducts.map(p => <option key={p.productId} value={p.productId}>{p.sku} · {p.name}</option>)}</select></label><button disabled={historyLoading}>Aplicar filtros</button></form></details>
          {historyError && <p role="alert" className="notice">{historyError}</p>}
          <div className="table-scroll"><table><thead><tr><th>Operación / fecha</th><th>Producto / ubicación</th><th>Tipo</th><th>Cantidad</th><th>Responsable / motivo</th></tr></thead><tbody>{!historyError && movements.map(m => <tr key={m.id}><td>#{m.operationId}<small className="block">{new Date(m.createdAt.endsWith('Z') ? m.createdAt : m.createdAt + 'Z').toLocaleString('es-NI')}</small></td><td>{m.product}<small className="block">{m.location}</small></td><td>{labels[m.type as MoveType] ?? m.type}</td><td className="inventory-quantity">{m.delta > 0 ? '+' : ''}{m.delta}</td><td>{m.username}<small className="block">{m.reason}</small></td></tr>)}</tbody></table></div>
          {historyLoading && <p role="status" className="inventory-empty">Cargando movimientos…</p>}{!historyLoading && !historyError && !movements.length && <p className="inventory-empty">No hay movimientos en este período.</p>}
          <div className="pagination"><button className="secondary" disabled={page === 1 || historyLoading} onClick={() => setPage(p => p - 1)}>Anterior</button><span>Página {page}</span><button className="secondary" disabled={movements.length < 25 || historyLoading || !!historyError} onClick={() => setPage(p => p + 1)}>Siguiente</button></div>
        </>}
      </div>
    </section>
    <SidePanel open={drawerOpen} title="Registrar movimiento" busy={busy} onClose={() => setDrawerOpen(false)}>
      <form onSubmit={move}><fieldset disabled={busy}>
        <label>Tipo de movimiento<select value={type} onChange={e => { setType(e.target.value as MoveType); setKey(crypto.randomUUID()); setMoveError(''); }}>{moveTypes.map(t => <option key={t} value={t}>{labels[t]}</option>)}</select></label>
        <label>Buscar producto<input value={productSearch} onChange={e => setProductSearch(e.target.value)} placeholder="Nombre, SKU o código de barras" maxLength={180}/></label>
        <label>Producto<select required value={draft.productId} onChange={e => { setDraft(d => ({ ...d, productId: e.target.value })); setSelectedProduct(products.find(p => p.id === Number(e.target.value)) ?? null); }}><option value="">Seleccionar producto</option>{productOptions.map(p => <option key={p.id} value={p.id}>{p.sku} · {p.name}</option>)}</select></label><small>{productsLoading ? 'Buscando…' : 'Hasta 25 coincidencias. Escribe para acotar la búsqueda.'}</small>
        <label>{type === 'TRASLADO' ? 'Ubicación de origen' : 'Ubicación'}<select required value={draft.locationId} onChange={e => setDraft(d => ({ ...d, locationId: e.target.value }))}><option value="">Seleccionar ubicación</option>{locations.filter(l => l.active).map(l => <option key={l.id} value={l.id}>{l.code} · {l.name}</option>)}</select></label>
        {type === 'TRASLADO' && <label>Ubicación de destino<select required value={draft.destinationId} onChange={e => setDraft(d => ({ ...d, destinationId: e.target.value }))}><option value="">Seleccionar destino</option>{locations.filter(l => l.active && l.id !== Number(draft.locationId)).map(l => <option key={l.id} value={l.id}>{l.code} · {l.name}</option>)}</select></label>}
        <label>{type === 'AJUSTE' ? 'Diferencia (+ ingreso / − retiro)' : 'Cantidad'}<input required type="number" step="0.001" min={type === 'AJUSTE' ? '-999999999999' : '0.001'} max="999999999999" value={draft.quantity} onChange={e => setDraft(d => ({ ...d, quantity: e.target.value }))}/></label>
        <label>Motivo<input required maxLength={500} value={draft.reason} onChange={e => setDraft(d => ({ ...d, reason: e.target.value }))}/></label>
        <p className="inventory-hint">{type === 'AJUSTE' ? 'El ajuste aplica una diferencia al saldo actual.' : type === 'TRASLADO' ? 'El traslado conserva el total de la tienda.' : 'El servidor valida y actualiza el saldo de la ubicación.'} Ante una respuesta perdida, reintenta con los mismos datos.</p>
        {moveError && <p role="alert" className="notice">{moveError}</p>}
        <div className="inventory-panel-actions"><button disabled={busy}>{busy ? 'Registrando…' : 'Guardar movimiento'}</button><button type="button" className="secondary" onClick={() => setDrawerOpen(false)}>Cerrar y conservar borrador</button></div>
        <button type="button" className="inventory-reset" onClick={newMovement}>Iniciar otra operación</button>
      </fieldset></form>
    </SidePanel>
    <SidePanel open={detail !== null} title="Detalle de existencias" onClose={() => setDetail(null)}>
      {detail && <><h3 className="inventory-detail-name">{detail.name}</h3><p className="inventory-hint">{detail.sku}</p><dl className="inventory-detail-summary"><div><dt>Total disponible</dt><dd>{detailTotal}</dd></div><div><dt>Mínimo en tienda</dt><dd>{detail.minimumStock}</dd></div><div><dt>Estado</dt><dd>{detailStatus}</dd></div></dl><h3>Distribución por ubicación</h3><table><thead><tr><th>Ubicación</th><th>Cantidad</th></tr></thead><tbody>{detailStocks.map(s => <tr key={s.locationId}><td>{s.location}</td><td className="inventory-quantity">{s.quantity}</td></tr>)}</tbody></table>{canMove && detail.active && <button className="inventory-detail-move" onClick={() => openMovement(detail)}>Registrar movimiento de este producto</button>}</>}
    </SidePanel>
  </div>;
}
