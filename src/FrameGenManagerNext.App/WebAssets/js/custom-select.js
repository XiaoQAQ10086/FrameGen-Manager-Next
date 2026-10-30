let openWrap = null;
let openMenu = null;

function closeOpen() {
  openWrap?.classList.remove('open');
  openWrap?.querySelector('.select-trigger')?.setAttribute('aria-expanded', 'false');
  openMenu?.classList.remove('open');
  openMenu?.remove();
  openWrap = null;
  openMenu = null;
}

function positionMenu(trigger, menu) {
  const rect = trigger.getBoundingClientRect();
  const estimatedHeight = Math.min(260, Math.max(44, menu.scrollHeight || 260));
  const below = rect.bottom + 6;
  const top = below + estimatedHeight <= window.innerHeight
    ? below
    : Math.max(8, rect.top - estimatedHeight - 6);
  menu.style.position = 'fixed';
  menu.style.left = `${Math.max(8, Math.min(rect.left, window.innerWidth - Math.max(120, rect.width) - 8))}px`;
  menu.style.top = `${top}px`;
  menu.style.width = `${Math.max(120, rect.width)}px`;
}

function refresh(select, trigger, menu) {
  const selected = select.selectedIndex >= 0 ? select.options[select.selectedIndex] : null;
  trigger.textContent = selected?.textContent ?? '—';
  trigger.disabled = select.disabled;
  menu.replaceChildren();
  Array.from(select.options).forEach((option, index) => {
    const item = document.createElement('button');
    item.type = 'button';
    item.className = `select-option${index === select.selectedIndex ? ' selected' : ''}`;
    item.textContent = option.textContent;
    item.setAttribute('role', 'option');
    item.setAttribute('aria-selected', String(index === select.selectedIndex));
    item.addEventListener('click', (event) => {
      event.stopPropagation();
      select.value = option.value;
      select.dispatchEvent(new Event('change', { bubbles: true }));
      closeOpen();
      trigger.focus();
    });
    menu.appendChild(item);
  });
}

export function enhanceSelect(select) {
  if (select.dataset.customSelect === '1') return;

  select.dataset.customSelect = '1';
  select.tabIndex = -1;
  select.setAttribute('aria-hidden', 'true');
  const wrap = document.createElement('div');
  wrap.className = 'select-wrap';
  if (select.classList.contains('lang-select')) {
    wrap.classList.add('select-wrap-lang');
  }
  select.parentNode.insertBefore(wrap, select);
  wrap.appendChild(select);

  const trigger = document.createElement('button');
  trigger.type = 'button';
  trigger.className = 'select-trigger';
  trigger.setAttribute('aria-haspopup', 'listbox');
  trigger.setAttribute('aria-expanded', 'false');

  // A menu inside .side / .modal would be clipped by their overflow. Keep every
  // menu at body level and position it in viewport coordinates instead.
  const menu = document.createElement('div');
  menu.className = 'select-menu';
  menu.setAttribute('role', 'listbox');
  wrap.appendChild(trigger);

  const valueDescriptor = Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value');
  Object.defineProperty(select, 'value', {
    configurable: true,
    get() { return valueDescriptor.get.call(this); },
    set(value) {
      valueDescriptor.set.call(this, value);
      refresh(select, trigger, menu);
    },
  });

  const update = () => refresh(select, trigger, menu);
  const observer = new MutationObserver(update);
  observer.observe(select, { childList: true, subtree: true });

  trigger.addEventListener('click', (event) => {
    event.stopPropagation();
    if (select.disabled) return;
    if (openWrap === wrap) {
      closeOpen();
      return;
    }
    closeOpen();
    document.body.appendChild(menu);
    positionMenu(trigger, menu);
    openWrap = wrap;
    openMenu = menu;
    wrap.classList.add('open');
    menu.classList.add('open');
    trigger.setAttribute('aria-expanded', 'true');
  });

  refresh(select, trigger, menu);
}

export function enhanceSelects(root = document) {
  root.querySelectorAll?.('select:not([data-custom-select])').forEach(enhanceSelect);
}

export function observeSelects() {
  const observer = new MutationObserver((mutations) => {
    for (const mutation of mutations) {
      for (const node of mutation.addedNodes) {
        if (!(node instanceof Element)) continue;
        if (node.matches?.('select')) enhanceSelect(node);
        enhanceSelects(node);
      }
    }
  });
  observer.observe(document.body, { childList: true, subtree: true });
}

document.addEventListener('click', closeOpen);
document.addEventListener('keydown', (event) => {
  if (event.key === 'Escape') closeOpen();
});
window.addEventListener('resize', closeOpen);
window.addEventListener('scroll', (event) => {
  if (!openWrap || !event.target?.contains?.(openWrap)) closeOpen();
}, true);
