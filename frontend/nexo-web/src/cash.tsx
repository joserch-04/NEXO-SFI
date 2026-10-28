import { useEffect, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { api, errorText } from './api';
import { can, money, date, round, OperationForm, printDocument, type Register, type Shift, type Reconciliation } from './sales-shared';
import { useSalesWorkspace } from './sales-context';

type CashView = 'summary' | 'history' | 'registers' | 'reconciliation';

export function Cash() {
  const { shifts, own, revision, reload } = useSalesWorkspace();
  return <><div className="module-heading"><div><p className="eyebrow">PUNTO DE VENTA / CAJA</p><h1>Caja y turnos</h1><p className="lead">Tu turno, sus movimientos y el cierre, paso a paso.</p></div>{can('ventas.registrar') && <Link className="secondary link-button" to="/pos">Ir a vender →</Link>}</div><CashDesk key={revision} shifts={shifts} own={own} refresh={reload}/></>;
}

function CashDesk({ shifts, own, refresh }: { shifts: Shift[]; own?: Shift; refresh: () => void }) {
  const [operationPending, setOperationPending] = useState(false);
  const [registers, setRegisters] = useState<Register[]>([]), [editing, setEditing] = useState<Register | null>(null);
  const [error, setError] = useState(''), [saving, setSaving] = useState(false), [saved, setSaved] = useState('');
  const [view, setView] = useState<CashView>('summary'), [selected, setSelected] = useState<Shift | null>(null);
  const [page, setPage] = useState(1), [history, setHistory] = useState(shifts), [registerForm, setRegisterForm] = useState(false);
  useEffect(() => { let live = true; api.request<Register[]>('/cajas').then(r => { if (live) setRegisters(r); }).catch(e => { if (live) setError(errorText(e)); }); return () => { live = false; }; }, []);
  useEffect(() => { let live = true; api.request<Shift[]>('/turnos?page=' + page).then(r => { if (live) setHistory(r); }).catch(e => { if (live) setError(errorText(e)); }); return () => { live = false; }; }, [page]);
  const allShifts = (api.session?.user.roles.includes('Administrador') ?? false) && can('cajas.historial');
  const visibleHistory = history.filter(s => allShifts || s.userId === api.session?.user.id);
  const canReconcile = (s: Shift) => can('cajas.arquear') && (allShifts || s.userId === api.session?.user.id);
  const available = registers.filter(r => r.active && !r.outOfService && !r.openShiftId);
  function reconcile(s: Shift) { if (operationPending) return; setSelected(s); setView('reconciliation'); }
  async function save(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); if (saving) return;
    const d = new FormData(e.currentTarget); setSaving(true); setError('');
    try {
      await api.request('/cajas' + (editing?.id ? '/' + editing.id : ''), { method: editing?.id ? 'PUT' : 'POST', body: JSON.stringify({ id: editing?.id ?? 0, version: editing?.version ?? 0, code: d.get('code'), name: d.get('name'), active: d.has('active'), outOfService: d.has('outOfService') }) });
      setRegisters(await api.request<Register[]>('/cajas')); setRegisterForm(false); setEditing(null); setSaved('Caja guardada correctamente.');
    } catch (err) { setError(errorText(err)); } finally { setSaving(false); }
  }
  return <>
    <div className="cash-navigation" aria-label="Secciones de caja">
      {([['summary', 'Mi turno'], ['history', 'Historial'], ['registers', 'Cajas registradoras']] as const).map(([key, label]) => <button key={key} type="button" className={view === key ? 'is-active' : ''} aria-pressed={view === key} disabled={operationPending} onClick={() => setView(key)}>{label}</button>)}
    </div>
    {operationPending && <p role="status" className="notice">Termina o reintentá la operación actual antes de cambiar de sección.</p>}{error && <p role="alert" className="notice">{error}</p>}{saved && <p role="status" className="cash-success">{saved}</p>}
    <section hidden={view !== 'summary'}>
      <div className="cash-turn-card">
        <div><span className="badge">{own ? 'Turno abierto' : 'Sin turno abierto'}</span><h2>{own ? own.register : 'Prepará tu caja para comenzar'}</h2><p>{own ? `Turno #${own.id} · ${own.username}` : 'Elegí una caja disponible e ingresá el fondo inicial.'}</p></div>
        {own && <div className="cash-opening"><small>Fondo inicial</small><strong>{money(own.opening)}</strong><small>Abierto: {date(own.openedAt)}</small></div>}
      </div>
      <div className="cash-action-grid">
        {!own && can('cajas.abrir') && <details className="cash-action card" name="cash-action"><summary><span className="cash-action-icon">＋</span><span><strong>Abrir mi turno</strong><small>{available.length} cajas disponibles</small></span><span className="cash-chevron">⌄</span></summary><OperationForm onPendingChange={setOperationPending} path="/turnos" title="Confirmar apertura" done={refresh} build={d => ({ registerId: Number(d.get('registerId')), opening: Number(d.get('opening')) })}><label>Caja<select name="registerId" required><option value="">Seleccionar caja</option>{available.map(r => <option key={r.id} value={r.id}>{r.code} · {r.name}</option>)}</select></label><label>Fondo inicial C$<input name="opening" type="number" min="0" step="0.01" required/></label></OperationForm>{!available.length && <p className="cash-muted">No hay cajas disponibles. Consultá al administrador.</p>}</details>}
        {own && can('cajas.movimientos') && <details className="cash-action card" name="cash-action"><summary><span className="cash-action-icon">⇄</span><span><strong>Movimiento de efectivo</strong><small>Registrar una entrada o salida</small></span><span className="cash-chevron">⌄</span></summary><OperationForm onPendingChange={setOperationPending} path={`/turnos/${own.id}/movimientos`} title="Confirmar movimiento" done={refresh} build={d => ({ type: d.get('type'), amount: Number(d.get('amount')), reason: d.get('reason') })}><label>Tipo<select name="type"><option value="INGRESO">Entrada de efectivo</option><option value="EGRESO">Salida de efectivo</option></select></label><label>Importe C$<input type="number" name="amount" required min="0.01" step="0.01"/></label><label>Motivo<input name="reason" required maxLength={500}/></label></OperationForm></details>}
        {own && canReconcile(own) && <button type="button" className="cash-action card" disabled={operationPending} onClick={() => reconcile(own)}><span className="cash-action-icon">✓</span><span><strong>Arqueo y cierre</strong><small>Revisar el turno y contar el efectivo</small></span><span className="cash-chevron">→</span></button>}
        <button type="button" className="cash-action card" disabled={operationPending} onClick={() => setView('history')}><span className="cash-action-icon">◷</span><span><strong>Consultar historial</strong><small>{allShifts ? 'Turnos y cierres de los cajeros' : 'Tus aperturas y cierres anteriores'}</small></span><span className="cash-chevron">→</span></button>
      </div>
    </section>
    <section className="card" hidden={view !== 'history'}><div className="cash-section-title"><div><h2>Aperturas y cierres</h2><p>{allShifts ? 'Historial de turnos de la tienda.' : 'Solo se muestran tus turnos.'}</p></div><span className="badge">Página {page}</span></div><div className="table-scroll"><table><thead><tr><th>Turno / caja</th><th>Cajero</th><th>Apertura</th><th>Cierre</th><th>Estado</th><th>Acción</th></tr></thead><tbody>{visibleHistory.map(s => <tr key={s.id}><td>#{s.id} · {s.register}</td><td>{s.username}</td><td>{date(s.openedAt)}</td><td>{s.closedAt ? date(s.closedAt) : '—'}</td><td><span className="badge">{s.state === 'ABIERTO' ? 'Abierto' : 'Cerrado'}</span></td><td>{canReconcile(s) && <button className="secondary" onClick={() => reconcile(s)}>Ver arqueo</button>}</td></tr>)}</tbody></table></div>{!visibleHistory.length && <p className="cash-empty">Todavía no hay turnos para mostrar.</p>}<div className="pagination"><button className="secondary" disabled={page === 1} onClick={() => setPage(p => p - 1)}>Anterior</button><button className="secondary" disabled={history.length < 50} onClick={() => setPage(p => p + 1)}>Siguiente</button></div></section>
    <section hidden={view !== 'registers'}>
      <div className="cash-section-title"><div><h2>Cajas registradoras</h2><p>Disponibilidad y estado de cada caja.</p></div>{can('cajas.administrar') && <button onClick={() => { setEditing(null); setRegisterForm(true); }}>＋ Nueva caja</button>}</div>
      <div className="cash-register-grid">{registers.map(c => <div className="card cash-register" key={c.id}><span className="badge">{!c.active ? 'Inactiva' : c.outOfService ? 'Fuera de servicio' : c.openShiftId ? 'Abierta' : 'Disponible'}</span><h3>{c.name}</h3><p>{c.code}</p>{can('cajas.administrar') && <button className="secondary" onClick={() => { setEditing(c); setRegisterForm(true); }}>Editar caja</button>}</div>)}</div>
      {can('cajas.administrar') && <section className="card cash-register-form" hidden={!registerForm}><div className="cash-section-title"><h2>{editing ? 'Editar caja' : 'Nueva caja'}</h2><button type="button" disabled={saving} className="secondary" onClick={() => setRegisterForm(false)}>Cancelar</button></div><form key={editing?.id ?? 0} onSubmit={save}><fieldset disabled={saving}><div className="form-grid"><label>Código<input name="code" defaultValue={editing?.code} required maxLength={30}/></label><label>Nombre<input name="name" defaultValue={editing?.name} required maxLength={100}/></label></div><label className="check"><input name="active" type="checkbox" defaultChecked={editing?.active ?? true}/>Activa</label><label className="check"><input name="outOfService" type="checkbox" defaultChecked={editing?.outOfService ?? false}/>Fuera de servicio</label><button>{saving ? 'Guardando…' : 'Guardar caja'}</button></fieldset></form></section>}
    </section>
    <section hidden={view !== 'reconciliation'}><button className="secondary cash-back" disabled={operationPending} onClick={() => setView('summary')}>← Volver a mi turno</button>{selected && canReconcile(selected) && <ShiftReconciliation key={selected.id} id={selected.id} own={own} refresh={refresh} onPendingChange={setOperationPending}/>}</section>
  </>;
}

function ShiftReconciliation({ id, own, refresh, onPendingChange }: { id: number; own?: Shift; refresh: () => void; onPendingChange: (pending: boolean) => void }) {
  const [r, setR] = useState<Reconciliation | null>(null), [error, setError] = useState(''), [counted, setCounted] = useState('');
  useEffect(() => { let live = true; api.request<Reconciliation>(`/turnos/${id}/arqueo`).then(v => { if (live) setR(v); }).catch(e => { if (live) setError(errorText(e)); }); return () => { live = false; }; }, [id]);
  return <section className="card">{error && <p role="alert" className="notice">{error}</p>}{!r && !error && <p role="status">Cargando arqueo…</p>}{r && <><div id="close-print" className="print-document"><h2>Arqueo del turno #{id}</h2><p>{r.shift.register} · {r.shift.username} · {r.shift.state}</p><p>Apertura: {date(r.shift.openedAt)}{r.shift.closedAt && <> · Cierre: {date(r.shift.closedAt)}</>}</p><dl className="pos-totals">{[['Fondo inicial', r.shift.opening], ['Cobros en efectivo (netos de cambio)', r.cashSales], ['Entradas', r.cashIn], ['Salidas', r.cashOut], ['Devoluciones efectivas', r.cashRefunds], ['Efectivo esperado', r.shift.expected ?? r.expected], ['Efectivo contado', r.shift.counted ?? (counted === '' ? null : Number(counted))], ['Diferencia', r.shift.difference ?? (counted === '' ? null : round(Number(counted) - r.expected))], ['Tarjeta cobrada', r.card], ['Transferencia cobrada', r.transfer], ['Reembolsos bancarios pendientes', r.pendingNonCashRefunds]].map(([label, value]) => <div className="total-row" key={String(label)}><dt>{label}</dt><dd>{value === null ? '—' : money(Number(value))}</dd></div>)}</dl>{r.shift.notes && <p>Observaciones: {r.shift.notes}</p>}<h3>Movimientos autorizados</h3>{!r.movements.length && <p>Sin movimientos adicionales.</p>}{r.movements.map(m => <p key={m.id}>{date(m.date)} · {m.type} · {money(m.amount)} · {m.reason}</p>)}</div><button className="secondary" onClick={() => printDocument('close-print', false)}>Imprimir resumen</button>{r.shift.state === 'ABIERTO' && own?.id === id && can('cajas.cerrar') && <details className="cash-close-step"><summary>Contar efectivo y cerrar turno <span>→</span></summary><p>Contá el dinero físico antes de confirmar. Este cierre es definitivo.</p><OperationForm onPendingChange={onPendingChange} path={`/turnos/${id}/cierre`} title="Confirmar cierre definitivo" done={refresh} build={d => ({ counted: Number(d.get('counted')), notes: d.get('notes') })}><label>Efectivo contado físicamente C$<input name="counted" required min="0" type="number" step="0.01" value={counted} onChange={e => setCounted(e.target.value)}/></label><p className="cash-count-result" aria-live="polite">{counted === '' ? 'Ingresá el monto contado para comparar.' : `Diferencia: ${money(round(Number(counted) - r.expected))}`}</p><label>Observaciones<textarea name="notes" maxLength={1000}/></label></OperationForm></details>}</>}</section>;
}
