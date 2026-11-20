import { $, call, state } from './core.js';

let run = 0;

function placeholder(game) {
  const node = document.createElement('div');
  node.className = 'thumb ph2';
  node.dataset.cover = game.id;
  node.textContent = game.name ? game.name.slice(0, 1) : '?';
  return node;
}

export async function loadCovers() {
  const token = ++run;
  const games = [...state.games];
  let index = 0;

  const worker = async () => {
    while (index < games.length) {
      if (token !== run) return;
      const game = games[index++];
      try {
        const cover = await call('cover', { gameId: game.id });
        if (token !== run || !cover.url) continue;
        state.covers[game.id] = cover.url;
        const node = $(`[data-cover="${CSS.escape(game.id)}"]`);
        if (!node) continue;
        const image = document.createElement('img');
        image.className = 'thumb';
        image.dataset.cover = game.id;
        image.src = cover.url;
        image.alt = '';
        image.addEventListener('error', () => {
          if (image.isConnected) image.replaceWith(placeholder(game));
        }, { once: true });
        node.replaceWith(image);
      } catch {
        // Keep the initial-letter placeholder when the cover cannot be resolved.
      }
    }
  };

  const workers = Math.min(4, games.length);
  await Promise.all(Array.from({ length: workers }, worker));
}
