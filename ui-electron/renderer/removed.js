import { $, call, esc, prettyPath, state, toast } from './core.js';
import { loadCovers } from './covers.js';
import { t } from './l10n.js';

export async function showRemoved(refresh) {
  const reply = await call('library.removed');
  const games = reply.games || [];
  const modal = document.createElement('div');
  modal.className = 'overlay';
  modal.innerHTML = `
    <section class="modal removed-modal" role="dialog" aria-modal="true">
      <header class="modal-head">
        <div><h2>${t('removedTitle')}</h2><p class="muted">${t('removedHint')}</p></div>
        <button class="icon-btn" data-close type="button">✕</button>
      </header>
      <div class="removed-list">
        ${games.length ? games.map(row).join('') : `<div class="removed-empty">${t('removedEmpty')}</div>`}
      </div>
    </section>`;
  $('#modal-root').appendChild(modal);
  loadCovers();
  const close = () => modal.remove();
  $('[data-close]', modal).onclick = close;
  modal.onclick = (event) => {
    if (event.target === modal) close();
  };
  modal.addEventListener('click', async (event) => {
    const target = event.target.closest('[data-action]');
    if (!target) return;
    try {
      if (target.dataset.action === 'restore') {
        await call('library.restoreRemoved', { gameId: target.dataset.id });
        close();
        await refresh();
        toast(t('removedRestored'));
      } else if (target.dataset.action === 'forget') {
        await call('library.forget', { gameId: target.dataset.id });
        close();
        await refresh();
        toast(t('removedForgotten'));
      }
    } catch (error) {
      toast(error.message, 'error');
    }
  });
}

function row(game) {
  const initial = game.name ? game.name.slice(0, 1) : '?';
  return `
    <div class="removed-row">
      ${state.covers[game.id]
        ? `<img class="removed-cover" data-cover="${esc(game.id)}" src="${esc(state.covers[game.id])}" alt="" decoding="async">`
        : `<div class="removed-initial" data-cover="${esc(game.id)}">${esc(initial)}</div>`}
      <div class="removed-info">
        <div class="removed-name">${esc(game.name)}</div>
        <div class="game-path">${esc(prettyPath(game.targetDirectory))}</div>
      </div>
      <div class="removed-actions">
        <button class="btn small primary" data-action="restore" data-id="${esc(game.id)}" type="button">${t('restoreGame')}</button>
        <button class="btn small danger" data-action="forget" data-id="${esc(game.id)}" type="button">${t('deleteRecord')}</button>
      </div>
    </div>`;
}
