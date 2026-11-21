import { $$, call, state } from './core.js';

const FAIL_TTL_MS = 10 * 60 * 1000;
const MAX_ACTIVE = 2;
const failed = new Map();
const inFlight = new Set();
const queue = [];
let active = 0;

const observer = new IntersectionObserver((entries) => {
  for (const entry of entries) {
    if (!entry.isIntersecting) continue;
    observer.unobserve(entry.target);
    enqueue(entry.target.dataset.cover);
  }
}, { rootMargin: '200px 0px' });

function gameById(id) {
  return state.games.find((game) => game.id === id);
}

function placeholder(game) {
  const node = document.createElement('div');
  node.className = 'thumb ph2';
  if (game) node.dataset.cover = game.id;
  node.textContent = game?.name ? game.name.slice(0, 1) : '?';
  return node;
}

function applyCover(id, url) {
  const game = gameById(id);
  for (const node of $$(`[data-cover="${CSS.escape(id)}"]`)) {
    if (node.tagName === 'IMG') {
      node.src = url;
      continue;
    }
    const image = document.createElement('img');
    image.className = node.classList.contains('thumb') ? 'thumb' : '';
    image.dataset.cover = id;
    image.src = url;
    image.alt = '';
    image.decoding = 'async';
    image.addEventListener('error', () => {
      delete state.covers[id];
      failed.set(id, Date.now());
      if (image.isConnected) image.replaceWith(placeholder(game));
    }, { once: true });
    node.replaceWith(image);
  }
}

function enqueue(id) {
  if (!id || state.covers[id] || inFlight.has(id)) return;
  const at = failed.get(id);
  if (at && Date.now() - at < FAIL_TTL_MS) return;
  if (!queue.includes(id)) queue.push(id);
  pump();
}

async function pump() {
  while (active < MAX_ACTIVE && queue.length) {
    const id = queue.shift();
    if (state.covers[id] || inFlight.has(id)) continue;
    active += 1;
    inFlight.add(id);
    try {
      const cover = await call('cover', { gameId: id });
      if (cover.url) {
        state.covers[id] = cover.url;
        applyCover(id, cover.url);
      } else {
        failed.set(id, Date.now());
      }
    } catch {
      failed.set(id, Date.now());
    } finally {
      inFlight.delete(id);
      active -= 1;
    }
    pump();
  }
}

export function loadCovers() {
  for (const node of $$('[data-cover]')) {
    const id = node.dataset.cover;
    if (!id) continue;
    if (state.covers[id]) {
      if (node.tagName !== 'IMG' || node.src !== state.covers[id]) applyCover(id, state.covers[id]);
      continue;
    }
    observer.observe(node);
  }
}
