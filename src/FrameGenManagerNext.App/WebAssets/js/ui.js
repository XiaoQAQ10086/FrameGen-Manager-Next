export function escapeHtml(value) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}

export function el(tag, className = '', html = '') {
  const element = document.createElement(tag);
  if (className) element.className = className;
  if (html) element.innerHTML = html;
  return element;
}

export function toast(message, level = '') {
  const node = document.getElementById('toast');
  node.textContent = message || '';
  node.className = `toast ${level}`.trim();
  clearTimeout(toast.timer);
  toast.timer = setTimeout(() => node.classList.add('hidden'), 4200);
}

export function progress(phase, subject, current, total) {
  const bar = document.getElementById('global-progress');
  if (!bar) return;
  const percent = total > 0 ? Math.min(100, (current * 100) / total) : 0;
  bar.querySelector('i').style.width = `${percent}%`;
  if (phase === 'done' || percent >= 100) {
    setTimeout(() => {
      bar.querySelector('i').style.width = '0%';
    }, 350);
  }
}

export function modal(content, options = {}) {
  const root = document.getElementById('modal-root');
  const overlay = el('div', 'overlay');
  const card = el('section', 'modal');
  card.innerHTML = content;
  overlay.appendChild(card);
  root.appendChild(overlay);

  const close = () => {
    overlay.remove();
    document.removeEventListener('keydown', onKey);
  };
  const onKey = (event) => {
    if (event.key === 'Escape') close();
  };
  overlay.addEventListener('click', (event) => {
    if (event.target === overlay) close();
  });
  document.addEventListener('keydown', onKey);
  card.querySelectorAll('[data-close]').forEach((button) => button.addEventListener('click', close));
  options.onOpen?.(card, close);
  return { root: card, close };
}

export function confirmDialog(title, message, okLabel = '确定') {
  return new Promise((resolve) => {
    const dialog = modal(
      `<header class="modal-head"><div><h2>${escapeHtml(title)}</h2></div></header>
       <p class="hint">${escapeHtml(message)}</p>
       <footer class="modal-foot"><span></span><div class="foot-actions">
         <button class="btn ghost" data-close type="button">取消</button>
         <button class="btn primary" data-ok type="button">${escapeHtml(okLabel)}</button>
       </div></footer>`,
    );
    dialog.root.querySelector('[data-ok]').addEventListener('click', () => {
      dialog.close();
      resolve(true);
    });
    dialog.root.querySelector('[data-close]').addEventListener('click', () => resolve(false));
    dialog.root.closest('.overlay').addEventListener('click', (event) => {
      if (event.target === event.currentTarget) resolve(false);
    });
  });
}

export function label(code, prefix) {
  return code ? `${prefix}.${code}` : `${prefix}.Unknown`;
}
