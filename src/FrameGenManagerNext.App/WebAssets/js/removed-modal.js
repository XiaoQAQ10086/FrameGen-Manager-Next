import { escapeHtml as esc } from './ui.js';
import { apiLabel, launcherKeys, prettyPath } from './labels.js';

export function removedMarkup(t) {
  return `
    <div class="overlay hidden" data-role="removed-overlay">
      <section class="modal removed-modal" role="dialog" aria-modal="true">
        <header class="modal-head">
          <div>
            <h2>${esc(t('Library.RemovedTitle'))}</h2>
            <p class="muted">${esc(t('Library.RemovedHint'))}</p>
          </div>
          <button class="icon-btn" data-close-removed type="button">✕</button>
        </header>
        <div class="removed-list" data-role="removed-list"></div>
      </section>
    </div>`;
}

export function createRemovedController(root, ctx, getRemoved, reload) {
  const { api, t, ui } = ctx;
  const overlay = root.querySelector('[data-role="removed-overlay"]');
  const list = root.querySelector('[data-role="removed-list"]');
  const count = root.querySelector('[data-role="removed-count"]');

  function render() {
    const removed = getRemoved();
    count.textContent = removed.length > 0 ? ` (${removed.length})` : '';
    list.innerHTML = removed.length > 0
      ? removed.map((game) => `
        <div class="removed-row">
          <div class="removed-initial">${esc(game.initial)}</div>
          <div class="removed-info">
            <div class="removed-name">${esc(game.name)}</div>
            <div class="game-path" title="${esc(prettyPath(game.targetDirectory))}">${esc(prettyPath(game.targetDirectory))}</div>
            <div class="chips">
              <span class="chip">${esc(t(launcherKeys[game.launcher] || 'Common.Unknown'))}</span>
              <span class="chip">${esc(apiLabel(game.api))}</span>
            </div>
          </div>
          <button class="btn small" data-action="restore-removed" data-id="${esc(game.id)}" type="button">${esc(t('Library.RestoreRemoved'))}</button>
        </div>`).join('')
      : `<p class="muted removed-empty">${esc(t('Library.RemovedEmpty'))}</p>`;
  }

  function open() {
    render();
    overlay.classList.remove('hidden');
  }

  function close() {
    overlay.classList.add('hidden');
  }

  overlay.addEventListener('click', async (event) => {
    const button = event.target.closest('[data-action="restore-removed"]');
    if (button) {
      try {
        await api.request('games.restore', { id: button.dataset.id });
        await reload();
        render();
      } catch (error) {
        ui.toast(error.message || error.code || '恢复失败', 'error');
      }
      return;
    }

    if (event.target === overlay || event.target.closest('[data-close-removed]')) {
      close();
    }
  });

  return { open, close, render };
}
