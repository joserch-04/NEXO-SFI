import { useRef, useState, type FormEvent, type ReactNode } from 'react';
import { api, ApiError, errorText } from './api';

export const can = (p: string) => api.session?.user.permissions.includes(p) ?? false;
export const money = (v: number) => new Intl.NumberFormat('es-NI', { style: 'currency', currency: 'NIO' }).format(v);
export const round = (v: number) => Math.round((v + Number.EPSILON) * 100) / 100;
export const quantityRound = (v: number) => Math.round((v + Number.EPSILON) * 1000) / 1000;
export const unitPrice = (v: number) => new Intl.NumberFormat('es-NI', { style: 'currency', currency: 'NIO', minimumFractionDigits: 2, maximumFractionDigits: 4 }).format(v);
export const date = (s: string) => new Date(s.endsWith('Z') ? s : s + 'Z').toLocaleString('es-NI');
export interface Register { id: number; code: string; name: string; active: boolean; outOfService: boolean; version: number; openShiftId: number | null }
export interface Shift { id: number; registerId: number; userId: number; register: string; username: string; state: string; openedAt: string; closedAt: string | null; opening: number; expected: number | null; counted: number | null; difference: number | null; notes: string | null }
export interface Sale { id: number; shiftId: number; username: string; register: string; date: string; state: string; customer: string | null; subtotal: number; discount: number; tax: number; total: number }
export interface Receipt { sale: Sale; lines: { id: number; name: string; sku: string; quantity: number; price: number; subtotal: number; discount: number; tax: number; total: number }[]; payments: { method: string; applied: number; received: number; change: number; reference?: string | null }[]; issuer: string; number: string; type: string; pendingRefund: number }
export interface Reconciliation { shift: Shift; cashSales: number; cashIn: number; cashOut: number; cashRefunds: number; expected: number; card: number; transfer: number; pendingNonCashRefunds: number; movements: { id: number; type: string; amount: number; reason: string; date: string }[] }

// Un error de transporte conserva exactamente el cuerpo y UUID del primer intento.
export function OperationForm({ path, build, done, children, title }: { path: string; build: (d: FormData) => object; done: () => void; children: ReactNode; title: string }) {
  const pending = useRef<object | null>(null), [busy, setBusy] = useState(false), [uncertain, setUncertain] = useState(false), [error, setError] = useState('');
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); if (busy) return;
    if (!pending.current) pending.current = { ...build(new FormData(e.currentTarget)), key: crypto.randomUUID() };
    setBusy(true); setError('');
    try { await api.request(path, { method: 'POST', body: JSON.stringify(pending.current) }); pending.current = null; setUncertain(false); done(); }
    catch (err) { setError(errorText(err)); if (err instanceof ApiError && err.status >= 400 && err.status < 500) { pending.current = null; setUncertain(false); } else setUncertain(true); }
    finally { setBusy(false); }
  }
  return <form onSubmit={submit}><fieldset disabled={busy || uncertain}>{children}</fieldset>{error && <p role="alert" className="notice">{error}</p>}{uncertain && <p>Respuesta sin confirmar. Reintentar conserva la misma operación y evita duplicarla.</p>}<button disabled={busy}>{busy ? 'Procesando…' : uncertain ? 'Reintentar misma operación' : title}</button></form>;
}

export function printDocument(id: string, thermal: boolean) {
  const source = document.getElementById(id); if (!source) return;
  const frame = document.createElement('iframe'); frame.title = 'Impresión NEXO'; frame.style.position = 'fixed'; frame.style.width = '0'; frame.style.height = '0'; frame.style.border = '0'; document.body.append(frame);
  const doc = frame.contentDocument; if (!doc) { frame.remove(); return; }
  doc.open(); doc.write('<!doctype html><html lang="es"><head><title>Comprobante NEXO</title></head><body></body></html>'); doc.close();
  const style = doc.createElement('style'); style.textContent = `@page{size:${thermal ? '80mm auto' : 'A4'};margin:${thermal ? '3mm' : '15mm'}}body{font: ${thermal ? '11px monospace' : '14px sans-serif'};color:#000;${thermal ? 'width:72mm;' : ''}}table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:4px 2px;border-bottom:1px dashed #aaa;overflow-wrap:anywhere}small{display:block}dl{display:grid;grid-template-columns:1fr auto}dd{margin:0;text-align:right}.total-row{display:contents}h2{font-size:1.4em}p{overflow-wrap:anywhere}`; doc.head.append(style); doc.body.append(source.cloneNode(true));
  const cleanup = () => frame.remove(); frame.contentWindow?.addEventListener('afterprint', cleanup, { once: true }); frame.contentWindow?.focus(); frame.contentWindow?.print();
}
