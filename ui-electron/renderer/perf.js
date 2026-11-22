const enabled = new URLSearchParams(location.search).get('perf') === '1';

export function perfMark(name, detail = null) {
  if (!enabled) return;
  try {
    window.fgm.perf({
      name,
      at: performance.now(),
      wall: Date.now(),
      detail,
    });
  } catch {}
}

export function perfEnabled() {
  return enabled;
}

if (enabled && 'PerformanceObserver' in window) {
  try {
    new PerformanceObserver((list) => {
      for (const entry of list.getEntries()) {
        perfMark('longtask', { duration: Math.round(entry.duration), name: entry.name });
      }
    }).observe({ entryTypes: ['longtask'] });
  } catch {}
}
