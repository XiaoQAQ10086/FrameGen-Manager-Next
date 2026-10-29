const bridge = window.chrome?.webview;
const $ = (id) => document.getElementById(id);

let state = null;
let currentGame = null;
let busy = false;

function post(type, payload = {}) {
  bridge?.postMessage({ type, ...payload });
}

function text(value, fallback = '—') {
  return value === null || value === undefined || value === '' ? fallback : String(value);
}

function setBusy(value) {
  busy = value;
  document.querySelectorAll('button').forEach((button) => {
    if (button.id === 'cancel-scan') {
      button.disabled = !value;
      return;
    }
    if (button.classList.contains('modal-action') || button.id === 'modal-deploy' || button.id === 'modal-restore') {
      return;
    }
    if (button.id === 'scan-games' || button.id === 'refresh' || button.id === 'add-game') {
      button.disabled = value;
    }
  });
}

function showToast(message, level = '') {
  const toast = $('toast');
  toast.textContent = message;
  toast.className = `toast ${level}`.trim();
  clearTimeout(showToast.timer);
  showToast.timer = setTimeout(() => toast.classList.add('hidden'), 4200);
}

function setNotice(message) {
  const notice = $('notice');
  notice.textContent = message || '';
  notice.classList.toggle('hidden', !message);
}

function setProgress(phase, subject, current, total) {
  $('status').textContent = subject ? `${phase}：${subject}` : phase;
  const percent = total > 0 ? Math.min(100, (current * 100) / total) : 0;
  $('progress').style.width = `${percent}%`;
}

function applyGpu(gpu) {
  $('gpu-name').textContent = text(gpu.name);
  $('gpu-driver').textContent = text(gpu.driver);
  $('gpu-fg').textContent = text(gpu.frameGeneration);
  $('gpu-sm').textContent = text(gpu.smoothMotion);
  $('gpu-route').textContent = text(gpu.route);
  $('gpu-hags').textContent = text(gpu.hags);
  $('gpu-status').textContent = text(gpu.status, '');
  $('spoof-current').textContent = text(gpu.spoofCurrent);
  $('spoof-driver').textContent = text(gpu.spoofDriver);
}

function renderGames(games) {
  const list = $('games');
  list.innerHTML = '';
  games.forEach((game, index) => {
    const card = document.createElement('article');
    card.className = 'game-card';
    card.style.animationDelay = `${Math.min(index * 28, 180)}ms`;

    const cover = game.cover
      ? `<img class="cover" src="${game.cover}" alt="" />`
      : `<div class="cover-fallback">${escapeHtml(game.initial || '?')}</div>`;

    const stateClass = game.stateClass || '';
    card.innerHTML = `
      ${cover}
      <span></span>
      <div class="game-main">
        <div class="game-info">
          <div class="game-name">${escapeHtml(game.name)}</div>
          <div class="game-path" title="${escapeHtml(game.targetDirectory)}">${escapeHtml(game.targetDirectory)}</div>
          <div class="chips">
            <span class="chip">${escapeHtml(game.launcher)}</span>
            <span class="chip">${escapeHtml(game.api)}</span>
            <span class="chip ${stateClass}">${escapeHtml(game.state)}</span>
          </div>
        </div>
        <div class="actions">
          <button class="btn small" data-action="deploy" data-id="${game.id}">部署</button>
          <button class="btn small ghost" data-action="restore" data-id="${game.id}">还原</button>
          <button class="btn small ghost" data-action="open" data-id="${game.id}">打开</button>
          <button class="btn small ghost" data-action="remove" data-id="${game.id}">移除</button>
        </div>
      </div>`;
    list.appendChild(card);
  });

  const empty = $('empty');
  empty.classList.toggle('hidden', games.length > 0);
  list.classList.toggle('hidden', games.length === 0);
}

function escapeHtml(value) {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}

function populateSelect(id, values, selected) {
  const select = $(id);
  select.innerHTML = '';
  values.forEach((value) => {
    const option = document.createElement('option');
    option.value = value;
    option.textContent = value;
    option.selected = String(value) === String(selected);
    select.appendChild(option);
  });
}

function fillOptions(options) {
  populateSelect('opt-proxy', options.proxySlots, 'version.dll');
  populateSelect('opt-optimized', options.optimizedLevels, 1);
  populateSelect('opt-frames', options.frameMultipliers, 3);
  populateSelect('opt-preset', options.presets, 'A');
  populateSelect('opt-engine', options.engines, 'DLSS');
  populateSelect('opt-sr', options.superResolutionModes, 'Off');
  populateSelect('opt-flow', options.xeSsFlowResolutions, 'Middle');
  populateSelect('opt-hudless', options.xeSsHudlessValues, 'Auto');
}

function syncXeSs() {
  const enabled = $('opt-engine').value === 'XeSS';
  document.querySelectorAll('.xess').forEach((element) => element.classList.toggle('hidden', !enabled));
}

function openModal(game) {
  currentGame = game;
  $('modal-title').textContent = game.name;
  $('modal-path').textContent = game.targetDirectory;
  $('overlay').classList.remove('hidden');
  syncXeSs();
}

function closeModal() {
  $('overlay').classList.add('hidden');
  currentGame = null;
}

function applyState(next) {
  state = next;
  applyGpu(next.gpu);
  fillOptions(next.options);
  renderGames(next.games);
  $('status').textContent = next.status || `已加载 ${next.games.length} 个游戏`;
  $('progress').style.width = '0%';
  setNotice(next.warning || '');
}

function applyMessage(message) {
  switch (message.type) {
    case 'state':
      applyState(message.data);
      break;
    case 'busy':
      setBusy(Boolean(message.data?.value));
      break;
    case 'progress':
      setProgress(message.data?.phase, message.data?.subject, message.data?.current, message.data?.total);
      break;
    case 'toast':
      showToast(message.data?.message, message.data?.level || '');
      break;
    case 'error':
      showToast(message.data?.message || '操作失败', 'error');
      break;
    case 'modal':
      if (message.action === 'close') closeModal();
      break;
  }
}

bridge?.addEventListener('message', (event) => applyMessage(event.data));

$('scan-games').addEventListener('click', () => post('scan'));
$('empty-scan').addEventListener('click', () => post('scan'));
$('add-game').addEventListener('click', () => post('addGame'));
$('refresh').addEventListener('click', () => post('refresh'));
$('cancel-scan').addEventListener('click', () => post('cancelScan'));
$('open-hags').addEventListener('click', () => post('openHags'));
$('close-modal').addEventListener('click', closeModal);
$('modal-cancel').addEventListener('click', closeModal);
$('opt-engine').addEventListener('change', syncXeSs);

$('modal-deploy').addEventListener('click', () => {
  if (!currentGame) return;
  const options = {
    proxySlot: $('opt-proxy').value,
    optimized: Number($('opt-optimized').value),
    frames: Number($('opt-frames').value),
    preset: $('opt-preset').value,
    engine: $('opt-engine').value,
    superResolutionMode: $('opt-sr').value,
    xeSsFlow: $('opt-flow').value,
    xeSsHudless: $('opt-hudless').value,
  };
  post('deploy', { id: currentGame.id, options });
  closeModal();
});

$('modal-restore').addEventListener('click', () => {
  if (!currentGame) return;
  post('restore', { id: currentGame.id });
  closeModal();
});

$('games').addEventListener('click', (event) => {
  const target = event.target.closest('[data-action]');
  if (!target) return;
  const game = state?.games.find((item) => item.id === target.dataset.id);
  if (!game) return;
  const action = target.dataset.action;
  if (action === 'deploy') openModal(game);
  if (action === 'restore') post('restore', { id: game.id });
  if (action === 'open') post('openFolder', { id: game.id });
  if (action === 'remove' && confirm(`确定要从列表中移除“${game.name}”吗？`)) post('remove', { id: game.id });
});

$('overlay').addEventListener('click', (event) => {
  if (event.target === $('overlay')) closeModal();
});

document.addEventListener('keydown', (event) => {
  if (event.key === 'Escape' && !$('overlay').classList.contains('hidden')) closeModal();
});

$('status').textContent = '正在连接后端…';
post('ready');
