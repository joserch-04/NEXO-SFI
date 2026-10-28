import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { api, ApiError, errorText } from './api';
import type { Master, Product, Stock } from './catalog';
import { ProductImage } from './product-image';
import { can, money, round, quantityRound, unitPrice, type Shift } from './sales-shared';
import { useSalesWorkspace } from './sales-context';
import { ReceiptView } from './sales';

export function Pos() {
  const { own, reload } = useSalesWorkspace();
  const [receiptId, setReceiptId] = useState<number | null>(null);
  return <><div className="module-heading"><div><p className="eyebrow">VENTAS</p><h1>Punto de venta</h1><p className="lead">Buscá productos, prepará el carrito y registrá el cobro.</p></div><div className="module-actions">{can('cajas.leer') && <Link className="secondary link-button" to="/pos/caja">Gestionar caja</Link>}{can('ventas.leer') && <Link className="secondary link-button" to="/pos/ventas">Ver ventas</Link>}</div></div><Checkout shift={own} refresh={reload} receipt={setReceiptId}/>{receiptId !== null && <ReceiptView id={receiptId} own={own} close={() => setReceiptId(null)} refresh={reload}/>}</>;
}

function Checkout({ shift, refresh, receipt }: { shift?: Shift; refresh: () => void; receipt: (id: number) => void }) {
  const [products, setProducts] = useState<Product[]>([]), [categories, setCategories] = useState<Master[]>([]), [subs, setSubs] = useState<Master[]>([]), [stocks, setStocks] = useState<Stock[]>([]);
  const [search, setSearch] = useState(''), [category, setCategory] = useState(''), [sub, setSub] = useState(''), [page, setPage] = useState(1), [rate, setRate] = useState(0), [error, setError] = useState('');
  const [cart, setCart] = useState<{ product: Product; quantity: number; discount: number }[]>([]), [customer, setCustomer] = useState(''), [paid, setPaid] = useState({ EFECTIVO: '', TARJETA: '', TRANSFERENCIA: '' });
  const [step, setStep] = useState<'cart' | 'payment'>('cart');
  const [paymentMode, setPaymentMode] = useState('EFECTIVO');
  const paymentRef = useRef<HTMLDialogElement>(null);
  const [expanded, setExpanded] = useState(false);
  const orderRef = useRef<HTMLDialogElement>(null);
  useEffect(() => { const dialog = orderRef.current; if (expanded && cart.length) { if (!dialog?.open) dialog?.showModal(); } else { dialog?.close(); if (expanded) setExpanded(false); } }, [expanded, cart.length]);
  useEffect(() => { const dialog = paymentRef.current; if (step === 'payment') { if (!dialog?.open) dialog?.showModal(); } else dialog?.close(); }, [step]);
  useEffect(() => () => paymentRef.current?.close(), []);
  const [references, setReferences] = useState<Record<string, string>>({ TARJETA: '', TRANSFERENCIA: '' });
  const missingReference = Object.entries(paid).some(([method, value]) => method !== 'EFECTIVO' && Number(value) > 0 && !references[method]?.trim());
  const [busy, setBusy] = useState(false), [pending, setPending] = useState<object | null>(null), [stockRevision, setStockRevision] = useState(0);
  const searchRef = useRef<HTMLInputElement>(null);
  useEffect(() => { let live = true; Promise.all([api.request<Master[]>('/categorias'), api.request<Master[]>('/subcategorias'), api.request<{ rate: number }>('/pos/configuracion')]).then(([c, s, cfg]) => { if (live) { setCategories(c); setSubs(s); setRate(cfg.rate); } }).catch(e => { if (live) setError(errorText(e)); }); return () => { live = false; }; }, []);
  useEffect(() => { let live = true; api.request<Stock[]>('/inventario/existencias').then(s => { if (live) setStocks(s); }).catch(e => { if (live) setError(errorText(e)); }); return () => { live = false; }; }, [stockRevision]);
  useEffect(() => { let live = true; const timer = setTimeout(() => { const q = new URLSearchParams({ search, active: 'true', pageSize: '24', page: String(page) }); if (category) q.set('categoryId', category); if (sub) q.set('subcategoryId', sub); api.request<Product[]>('/productos?' + q).then(p => { if (live) setProducts(p); }).catch(e => { if (live) setError(errorText(e)); }); }, 180); return () => { live = false; clearTimeout(timer); }; }, [search, category, sub, page, stockRevision]);
  function add(p: Product) { if (busy || pending) return; setCart(c => c.some(l => l.product.id === p.id) ? c.map(l => l.product.id === p.id ? { ...l, quantity: quantityRound(l.quantity + 1) } : l) : [...c, { product: p, quantity: 1, discount: 0 }]); setSearch(''); setPage(1); searchRef.current?.focus({ preventScroll: true }); }
  async function scan(e: FormEvent) { e.preventDefault(); if (busy || pending || !search.trim()) return; try { const found = await api.request<Product[]>('/productos?active=true&pageSize=100&search=' + encodeURIComponent(search.trim())); const exact = found.find(p => p.barcode === search.trim() || p.sku === search.trim()); if (exact) add(exact); else setError('Código sin coincidencia exacta. Seleccioná un producto del catálogo.'); } catch (err) { setError(errorText(err)); } }
  const subtotal = round(cart.reduce((a, l) => a + round(l.product.price * l.quantity), 0)), discount = round(cart.reduce((a, l) => a + l.discount, 0)), tax = round(cart.reduce((a, l) => a + round((round(l.product.price * l.quantity) - l.discount) * rate), 0)), total = round(subtotal - discount + tax);
  const received = round(Object.values(paid).reduce((a, n) => a + Number(n || 0), 0)), change = round(Math.max(0, received - total));
  function showStep(next: 'cart' | 'payment') { if (busy || pending) return; if (next === 'payment') { orderRef.current?.close(); setExpanded(false); } setStep(next); }
  function selectPayment(method: string) {
    if (busy || pending) return;
    setPaymentMode(method);
    setPaid({ EFECTIVO: '', TARJETA: '', TRANSFERENCIA: '', ...(method !== 'MIXTO' ? { [method]: total.toFixed(2) } : {}) });
    setReferences({ TARJETA: '', TRANSFERENCIA: '' });
  }
  async function confirm() {
    if (!shift || busy) return; if (!pending && missingReference) { setError('Ingresá la referencia de cada pago con tarjeta o transferencia.'); return; } const input = pending ?? { key: crypto.randomUUID(), shiftId: shift.id, customer: customer || null, lines: cart.map(l => ({ productId: l.product.id, quantity: l.quantity, discount: l.discount })), payments: Object.entries(paid).filter(([, v]) => Number(v) > 0).map(([method, v]) => ({ method, received: Number(v), ...(method === 'EFECTIVO' ? {} : { reference: references[method].trim() }) })) };
    setPending(input); setBusy(true); setError('');
    try { const result = await api.request<{ id: number }>('/ventas', { method: 'POST', body: JSON.stringify(input) }); setPending(null); setCart([]); setPaid({ EFECTIVO: '', TARJETA: '', TRANSFERENCIA: '' }); setReferences({ TARJETA: '', TRANSFERENCIA: '' }); setCustomer(''); paymentRef.current?.close(); setStep('cart'); setExpanded(false); setPaymentMode('EFECTIVO'); setStockRevision(v => v + 1); refresh(); receipt(result.id); }
    catch (err) { setError(errorText(err)); if (err instanceof ApiError && err.status >= 400 && err.status < 500) setPending(null); }
    finally { setBusy(false); }
  }
  return <>
    <div className="pos-shift">{shift ? <>● Turno #{shift.id} · {shift.register}</> : 'Abrí un turno propio desde Gestión de caja para comenzar a cobrar.'}<span>Moneda NIO · Impuesto {rate * 100}%</span></div>
    {error && step === 'cart' && <p role="alert" className="notice">{error}</p>}
    <div className="pos-workbench">
      <section className="pos-storefront" aria-label="Catálogo de productos">
        <fieldset disabled={busy || !!pending} className="pos-storefront-fields">
          <div className="pos-searchbar">
            <form onSubmit={scan}><label className="pos-search-label">Buscar o escanear<input autoFocus ref={searchRef} value={search} maxLength={180} placeholder="Buscá un producto o escaneá su código" onChange={e => { setSearch(e.target.value); setPage(1); }}/></label></form>
            <label>Categoría<select value={category} onChange={e => { setCategory(e.target.value); setSub(''); setPage(1); }}><option value="">Todas las categorías</option>{categories.filter(c => c.active).map(c => <option value={c.id} key={c.id}>{c.name}</option>)}</select></label>
            <label>Subcategoría<select value={sub} onChange={e => { setSub(e.target.value); setPage(1); }}><option value="">Todas las subcategorías</option>{subs.filter(s => s.active && (!category || s.parentId === Number(category))).map(s => <option value={s.id} key={s.id}>{s.name}</option>)}</select></label>
          </div>
          <div className="pos-browse" tabIndex={0}>
            <div className="pos-products">{products.map(p => {
              const stock = stocks.filter(s => s.productId === p.id).reduce((n, s) => n + s.quantity, 0);
              const quantity = cart.find(l => l.product.id === p.id)?.quantity;
              return <button type="button" className={'pos-product' + (quantity ? ' is-in-cart' : '')} key={p.id} onClick={() => add(p)} disabled={p.trackInventory && stock <= 0}>
                <div className="pos-product-photo"><ProductImage id={p.id} reference={p.imageRef}/>{quantity && <span className="pos-product-count" aria-label={`${quantity} en la venta`}>{quantity}</span>}</div>
                <strong>{p.name}</strong><small>{p.sku}</small><div className="pos-product-bottom"><b>{unitPrice(p.price)}</b><span className="badge">{p.trackInventory ? `${stock} disp.` : 'Preparado'}</span></div>
              </button>;
            })}</div>
            {!products.length && <p className="cash-empty">No hay productos con estos filtros.</p>}
            <div className="pagination"><button className="secondary" disabled={page === 1} onClick={() => setPage(p => p - 1)}>Anterior</button><span>Página {page}</span><button className="secondary" disabled={products.length < 24} onClick={() => setPage(p => p + 1)}>Siguiente</button></div>
          </div>
        </fieldset>
      </section>
      <section className="pos-order-tray" aria-label="Venta actual">
        <div className="pos-order-summary">
          <button type="button" className="pos-order-toggle" aria-expanded={expanded} aria-haspopup="dialog" aria-controls="pos-order-dialog" disabled={!cart.length || busy || !!pending} onClick={() => setExpanded(true)}><span className="pos-order-icon" aria-hidden="true">▤</span><span><strong>{cart.length ? 'Tu venta' : 'Empezá tu venta'}</strong><small>{cart.length ? `${cart.length} ${cart.length === 1 ? 'producto' : 'productos'} · Ver venta` : 'Seleccioná productos o escaneá un código'}</small></span>{!!cart.length && <span aria-hidden="true">{'↗'}</span>}</button>
          <div className="pos-order-total"><small>Total a cobrar</small><strong>{money(total)}</strong></div>
          <button type="button" className="pos-pay-button" disabled={!shift || !cart.length || total <= 0 || busy || !!pending} onClick={() => showStep('payment')}>Cobrar <span aria-hidden="true">→</span></button>
        </div>
      </section>
    </div>
    <dialog ref={orderRef} id="pos-order-dialog" className="receipt-dialog pos-order-dialog" aria-labelledby="pos-order-title" onCancel={() => setExpanded(false)}>
      <header className="receipt-header"><div><p className="eyebrow">REVISAR PRODUCTOS</p><h2 id="pos-order-title">Tu venta · {cart.length} {cart.length === 1 ? 'producto' : 'productos'}</h2></div><button type="button" className="receipt-close" aria-label="Cerrar detalle de venta" onClick={() => setExpanded(false)}>×</button></header>
        <div className="receipt-body pos-order-lines">
          <fieldset disabled={busy || !!pending}>
            <div className="pos-order-items">{cart.map(l => <div className="pos-order-item" key={l.product.id}>
              <div className="pos-order-item-name"><strong>{l.product.name}</strong><small>{unitPrice(l.product.price)} / {l.product.unit}</small></div>
              <div className="cart-quantity"><button type="button" className="secondary" aria-label={'Disminuir ' + l.product.name} onClick={() => setCart(c => c.map(x => x === l ? { ...x, quantity: Math.max(.001, quantityRound(x.quantity - 1)) } : x))}>−</button><input aria-label={'Cantidad de ' + l.product.name} type="number" min="0.001" step="0.001" value={l.quantity} onChange={e => setCart(c => c.map(x => x === l ? { ...x, quantity: Number(e.target.value) } : x))}/><button type="button" className="secondary" aria-label={'Aumentar ' + l.product.name} onClick={() => setCart(c => c.map(x => x === l ? { ...x, quantity: quantityRound(x.quantity + 1) } : x))}>+</button></div>
              <strong className="pos-order-line-total">{money(round(round(l.product.price * l.quantity) - l.discount + round((round(l.product.price * l.quantity) - l.discount) * rate)))}</strong>
              <button type="button" className="pos-remove" aria-label={'Quitar ' + l.product.name} title="Quitar producto" onClick={() => setCart(c => c.filter(x => x !== l))}>×</button>
              {can('ventas.descuentos') && <details className="checkout-option pos-line-discount"><summary>{l.discount > 0 ? `Descuento: ${money(l.discount)}` : 'Aplicar descuento'}</summary><label>Descuento C$<input type="number" min="0" step="0.01" value={l.discount} onChange={e => setCart(c => c.map(x => x === l ? { ...x, discount: Number(e.target.value) } : x))}/></label></details>}
            </div>)}</div>
            <div className="pos-order-extras"><details className="checkout-option"><summary>{customer ? 'Cliente: ' + customer : '＋ Agregar cliente'}</summary><label>Nombre del cliente<input maxLength={180} value={customer} onChange={e => setCustomer(e.target.value)}/></label></details><span>Subtotal {money(subtotal)} · Descuentos {money(discount)} · Impuestos {money(tax)}</span></div>
          </fieldset>
        </div>
      <footer className="receipt-actions"><button type="button" className="secondary" onClick={() => setExpanded(false)}>Seguir agregando</button><button type="button" disabled={!shift || !cart.length || total <= 0} onClick={() => showStep('payment')}>Cobrar · {money(total)}</button></footer>
    </dialog>
    <dialog ref={paymentRef} className="receipt-dialog pos-payment-dialog" aria-labelledby="pos-payment-title" onCancel={e => { e.preventDefault(); showStep('cart'); }}>
      <header className="receipt-header"><div><p className="eyebrow">FINALIZAR VENTA</p><h2 id="pos-payment-title">Cobrar venta</h2></div><button type="button" className="receipt-close" aria-label="Volver a la venta" title="Volver a la venta" disabled={busy || !!pending} onClick={() => showStep('cart')}>×</button></header>
      <div className="receipt-body">
        <div className="pos-payment-total"><span>{cart.length} {cart.length === 1 ? 'producto' : 'productos'}{customer && ` · ${customer}`}</span><strong>{money(total)}</strong><small>Subtotal {money(subtotal)} · Descuentos {money(discount)} · Impuestos {money(tax)}</small></div>
        {error && <p role="alert" className="notice">{error}</p>}
        <fieldset disabled={busy || !!pending}>
          <p className="pos-payment-label">Elegí cómo paga el cliente</p>
          <div className="payment-methods" aria-label="Método de pago">{[['EFECTIVO', 'Efectivo'], ['TARJETA', 'Tarjeta NFC'], ['TRANSFERENCIA', 'Transferencia'], ['MIXTO', 'Combinado']].map(([method, label]) => <button type="button" key={method} aria-pressed={paymentMode === method} className={paymentMode === method ? 'is-active' : ''} onClick={() => selectPayment(method)}>{label}</button>)}</div>
          {paymentMode === 'MIXTO' && <p className="cash-muted">Distribuí el total entre los métodos que usa el cliente.</p>}
          {Object.entries(paid).filter(([method]) => paymentMode === 'MIXTO' || paymentMode === method).map(([method, value]) => <div key={method}><label>{method === 'TARJETA' ? 'Importe con tarjeta' : method === 'TRANSFERENCIA' ? 'Importe por transferencia' : 'Efectivo recibido'}<input inputMode="decimal" type="number" min="0" step="0.01" value={value} placeholder="0.00" onChange={e => setPaid(p => ({ ...p, [method]: e.target.value }))}/></label>{method !== 'EFECTIVO' && Number(value) > 0 && <label>{method === 'TARJETA' ? 'Referencia de la operación con tarjeta' : 'Referencia de la transferencia'}<input type="text" required maxLength={100} autoComplete="off" spellCheck={false} value={references[method]} placeholder={method === 'TARJETA' ? 'Ej.: NFC-00001234' : 'Ej.: TRF-00005678'} onChange={e => setReferences(r => ({ ...r, [method]: e.target.value }))}/><small>{method === 'TARJETA' ? 'Referencia del voucher aprobado de la terminal NFC. No ingresés número de tarjeta ni CVV.' : 'Referencia de la transferencia recibida en el banco.'}</small></label>}</div>)}
          {paymentMode === 'EFECTIVO' && <button type="button" className="secondary payment-exact" onClick={() => setPaid(p => ({ ...p, EFECTIVO: total.toFixed(2) }))}>Importe exacto · {money(total)}</button>}
          <div className="payment-summary" aria-live="polite"><span>Recibido <strong>{money(received)}</strong></span><span>{received < total ? 'Falta' : 'Cambio'} <strong>{money(received < total ? round(total - received) : change)}</strong></span></div>
        </fieldset>
        {pending && !busy && <p className="notice">Respuesta sin confirmar. Reintentar conserva la misma venta.</p>}
      </div>
      <footer className="receipt-actions"><button type="button" className="secondary" disabled={busy || !!pending} onClick={() => showStep('cart')}>Volver a la venta</button><button type="button" disabled={busy || !shift || !cart.length || (!pending && (missingReference || received < total || change >= Number(paid.EFECTIVO || 0) && change > 0))} onClick={confirm}>{busy ? 'Confirmando…' : pending ? 'Reintentar venta' : `Confirmar cobro · ${money(total)}`}</button></footer>
    </dialog>
  </>;
}
