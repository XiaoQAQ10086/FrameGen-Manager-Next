import { confirmDialog, escapeHtml as esc } from './ui.js';
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
          ${game.cover
            ? `<img class="removed-cover" src="${game.cover}" alt="" loading="lazy" decoding="async">`
            : `<div class="removed-initial">${esc(game.initial)}</div>`}
          <div class="removed-info">
            <div class="removed-name">${esc(game.name)}</div>
            <div class="game-path" title="${esc(prettyPath(game.targetDirectory))}">${esc(prettyPath(game.targetDirectory))}</div>
            <div class="chips">
              <span class="chip">${esc(t(launcherKeys[game.launcher] || 'Common.Unknown'))}</span>
              <span class="chip">${esc(apiLabel(game.api))}</span>
              ${game.available === false ? `<span class="chip warn">${esc(t('Library.Missing'))}</span>` : ''}
            </div>
          </div>
          <div class="removed-actions">
            <button class="btn small" data-action="restore-removed" data-id="${esc(game.id)}" type="button" ${game.available === false ? 'disabled' : ''} ${game.available === false ? `title="${esc(t('Library.Missing'))}"` : ''}>${esc(t('Library.RestoreRemoved'))}</button>
            <button class="btn small danger" data-action="forget-removed" data-id="${esc(game.id)}" type="button">${esc(t('Library.Forget'))}</button>
          </div>
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
    const forgetButton = event.target.closest('[data-action="forget-removed"]');
    if (forgetButton) {
      const game = getRemoved().find((item) => item.id === forgetButton.dataset.id);
      if (!game) return;
      const confirmed = await confirmDialog(
        t('Library.Forget'),
        t('Library.Confirm.Forget').replace('{0}', game.name),
        t('Library.Forget'),
      );
      if (!confirmed) return;

      try {
        await api.request('games.forget', { id: game.id });
        await reload();
        render();
        ui.toast(t('Common.OperationDone'));
      } catch (error) {
        ui.toast(error.message || error.code || t('Library.Forget'), 'error');
      }
      return;
    }

    const button = event.target.closest('[data-action="restore-removed"]');
    if (button) {
      try {
        await api.request('games.restore', { id: button.dataset.id });
        await reload();
        render();
      } catch (error) {
        ui.toast(error.message || error.code || t('Library.RestoreRemoved'), 'error');
      }
      return;
    }

    if (event.target === overlay || event.target.closest('[data-close-removed]')) {
      close();
    }
  });

  return { open, close, render };
}
