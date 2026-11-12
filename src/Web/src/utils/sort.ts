// Sortierung für Tabellen
import type { ReactNode } from 'react';

export interface SortConfig<T> {
  key: keyof T;
  direction: 'asc' | 'desc';
}

export function getSortIcon<T>(sortConfig: SortConfig<T> | null, key: keyof T): ReactNode {
  if (!sortConfig || sortConfig.key !== key) {
    return null;
  }
  return sortConfig.direction === 'asc' ? 'up' : 'down';
}

export function sortData<T>(data: T[], sortConfig: SortConfig<T> | null): T[] {
  if (!sortConfig) return data;

  const sorted = [...data];
  sorted.sort((itemA, itemB) => {
    const aValue = itemA[sortConfig.key];
    const bValue = itemB[sortConfig.key];

    if (aValue == null && bValue == null) return 0;
    if (aValue == null) return sortConfig.direction === 'asc' ? -1 : 1;
    if (bValue == null) return sortConfig.direction === 'asc' ? 1 : -1;

    const a = String(aValue).toLowerCase();
    const b = String(bValue).toLowerCase();

    if (a < b) return sortConfig.direction === 'asc' ? -1 : 1;
    if (a > b) return sortConfig.direction === 'asc' ? 1 : -1;
    return 0;
  });

  return sorted;
}
