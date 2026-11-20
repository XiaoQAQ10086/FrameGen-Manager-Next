import { $, call, state } from './core.js';

export async function loadCovers() {
  for (const game of state.games) {
    try {
      const cover = await call('cover', { gameId: game.id });
      if (cover.url) {
        state.covers[game.id] = cover.url;
        const node = $(`[data-cover="${CSS.escape(game.id)}"]`);
        if (node) {
          const image = document.createElement('img');
          image.className = 'thumb';
          image.dataset.cover = game.id;
          image.src = cover.url;
          image.alt = '';
          node.replaceWith(image);
        }
      }
    } catch {}
  }
}
