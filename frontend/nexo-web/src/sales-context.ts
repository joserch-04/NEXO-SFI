import { createContext, useContext } from 'react';
import type { Shift } from './sales-shared';

export interface SalesContextValue { shifts: Shift[]; own?: Shift; revision: number; reload: () => void }
export const SalesContext = createContext<SalesContextValue | null>(null);
export function useSalesWorkspace() {
  const context = useContext(SalesContext);
  if (!context) throw new Error('Falta SalesWorkspace.');
  return context;
}
