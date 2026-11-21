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
