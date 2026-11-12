// Skeleton-Komponenten für Ladezustände

export function Skeleton({ className }: { className?: string }) {
  return <div className={`animate-pulse bg-slate-200 dark:bg-slate-700 ${className}`} />;
}

export function SkeletonText({ lines = 3 }: { lines?: number }) {
  return (
    <div className="space-y-2">
      {Array.from({ length: lines }).map((_, i) => (
        <Skeleton key={i} className="h-4 w-full" />
      ))}
    </div>
  );
}

export function SkeletonBlock({ width = '100%', height = '200px' }: { width?: string; height?: string }) {
  return <Skeleton className={`rounded ${width} ${height}`} />;
}

export function SkeletonCard() {
  return (
    <div className="rounded bg-white p-4 shadow-sm dark:bg-slate-800">
      <Skeleton className="mb-4 h-6 w-1/2" />
      <Skeleton className="mb-2 h-4 w-full" />
      <Skeleton className="mb-2 h-4 w-5/6" />
      <Skeleton className="h-4 w-4/6" />
    </div>
  );
}
