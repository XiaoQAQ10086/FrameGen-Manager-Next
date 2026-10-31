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

export function progress(phase, subject, current, total, options = {}) {
  const card = document.getElementById('global-progress');
  if (!card) return;
  clearTimeout(progress.timer);
  const indeterminate = options.indeterminate === true || total <= 0;
  const percent = total > 0 ? Math.min(100, Math.round((current * 100) / total)) : 0;
  if (card.classList.contains('hidden')) progress.shownAt = Date.now();
  card.classList.remove('hidden');
  card.classList.toggle('indeterminate', indeterminate);
  card.querySelector('.gp-phase').textContent = phase || '';
  card.querySelector('.gp-percent').textContent = total > 0 ? `${current} / ${total} · ${percent}%` : '';
  card.querySelector('.gp-subject').textContent = subject || '';
  const fill = card.querySelector('.gp-track i');
  fill.style.width = indeterminate ? '' : `${percent}%`;
  if (!indeterminate && percent >= 100) {
    progress.timer = setTimeout(progressDone, 900);
  }
}

export function progressDone() {
  const card = document.getElementById('global-progress');
  if (!card) return;
  clearTimeout(progress.timer);
  const remaining = 650 - (Date.now() - (progress.shownAt || 0));
  if (remaining > 0) {
    progress.timer = setTimeout(hideProgress, remaining);
    return;
  }
  hideProgress();
}

function hideProgress() {
  const card = document.getElementById('global-progress');
  if (!card) return;
  card.classList.add('hidden');
  card.classList.remove('indeterminate');
  card.querySelector('.gp-track i').style.width = '0%';
}

export function autoHideScrollbar(element, idleMs = 700) {
  if (!element || element.dataset.scrollFade === '1') return () => {};
  element.dataset.scrollFade = '1';
  let timer = null;
  const onScroll = () => {
    element.classList.add('scrolling');
    clearTimeout(timer);
    timer = setTimeout(() => element.classList.remove('scrolling'), idleMs);
  };
  element.addEventListener('scroll', onScroll, { passive: true });
  return () => {
    clearTimeout(timer);
    element.removeEventListener('scroll', onScroll);
    element.classList.remove('scrolling');
    delete element.dataset.scrollFade;
  };
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

export function libraryChanged(source) {
  document.dispatchEvent(new CustomEvent('fgm:library-changed', { detail: { source } }));
}

export function label(code, prefix) {
  return code ? `${prefix}.${code}` : `${prefix}.Unknown`;
}
