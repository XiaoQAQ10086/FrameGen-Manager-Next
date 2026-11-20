import {
  $, $$, apiLabel, call, esc, folderIcon, hagsClass, hagsLabel, hideProgress,
  prettyPath, showProgress, sourceLabel, state, stateLabel, toast, trashIcon,
} from './core.js';
import { openDeployDialog, restoreGame } from './deploy.js';

function selectSection(section) {
  state.section = section;
  $$('.nav-item').forEach((item) => item.classList.toggle('active', item.dataset.page === section));
  moveIndicator();
  if (section === 'framegen') {
    renderFramegen();
  } else {
    const labels = {
      'smooth-motion': 'Smooth Motion',
      dlss5: 'DLSS5 / ReShade',
      anr: 'ANR 帧生成',
      logs: '日志',
      about: '关于',
      donate: '打赏赞助',
    };
    $('#view').innerHTML = `
      <section class="page">
        <header class="page-head">
          <h1>${esc(labels[section] || section)}</h1>
          <p>该页面正在从 Flutter 版迁移到 Electron，样式沿用旧版 WebAssets。</p>
        </header>
      </section>`;
  }
}

function moveIndicator() {
  const nav = $('#nav');
  const active = $('.nav-item.active', nav);
  const indicator = $('.nav-indicator', nav);
  if (!active || !indicator) return;
  indicator.style.left = `${active.offsetLeft}px`;
  indicator.style.width = `${active.offsetWidth}px`;
  indicator.style.top = `${active.offsetTop}px`;
  indicator.style.bottom = 'auto';
  indicator.style.height = `${active.offsetHeight}px`;
}

async function boot() {
  try {
    const version = await call('version');
    $('#app-version').textContent = `v${version}`;
  } catch {}
  try {
    const [paths, gpu, modes, games, spoof] = await Promise.all([
      call('paths'),
      call('gpu'),
      call('gameModes', {}),
      call('library.list'),
      call('gpuSpoof.inspect'),
    ]);
    state.paths = paths;
    state.gpu = gpu.gpu;
    state.modes = Object.fromEntries((modes.modes || []).map((mode) => [mode.gameId, mode]));
    state.games = games.games || [];
    state.spoof = spoof;
  } catch (error) {
    console.error('boot failed', error);
    toast(`加载失败：${error.message}`, 'error');
  }
  renderFramegen();
  moveIndicator();
  const view = $('#view');
  if (view) view.scrollTop = 0;
  loadCovers();
  window.addEventListener('resize', moveIndicator);
}

async function loadCovers() {
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

function renderFramegen() {
  const view = $('#view');
  view.innerHTML = `
    <section class="page">
      <header class="listhead">
        <div class="lh-text">
          <h1>DLSS-G 帧生成</h1>
          <div class="sub" data-role="status">已加载 ${state.games.length} 个游戏</div>
        </div>
        <div class="lh-actions">
          <button class="btn" data-action="add" type="button">添加游戏</button>
          <button class="btn" data-action="removed" type="button">已移除的游戏</button>
          <button class="btn scan" data-action="scan" type="button">扫描游戏</button>
        </div>
      </header>

      <main class="layout">
        <aside class="side">
          <section class="panel" data-role="gpu"></section>
          <section class="panel" data-role="spoof"></section>
          <section class="panel" data-role="assets"></section>
          <section class="panel">
            <div class="ctitle">可部署显卡与资产来源</div>
            <div class="chips"><span class="chip gpu-chip">NVIDIA RTX 20 / 30 / 40 / 50 系</span></div>
            <a class="source-link" data-action="github" href="#">GitHub 资产来源</a>
          </section>
        </aside>
        <section class="content">
          <div class="game-list" data-role="games"></div>
        </section>
      </main>
    </section>`;

  renderGpuPanel();
  renderSpoofPanel();
  renderAssetsPanel();
  renderGames();
  bindFramegenActions();
  requestAnimationFrame(() => {
    const side = $('.side');
    if (side) side.scrollTop = 0;
  });
}

function renderGpuPanel() {
  const gpu = state.gpu;
  const panel = $('[data-role="gpu"]');
  if (!gpu) {
    panel.innerHTML = '<div class="ctitle">GPU 驱动与系统设置</div><div class="hint">正在读取显卡信息…</div>';
    return;
  }
  panel.innerHTML = `
    <div class="ctitle">GPU 驱动与系统设置</div>
    <div class="chips"><span class="chip gpu-chip" title="${esc(gpu.name)}">${esc(gpu.name)}</span></div>
    <div class="kv2"><span>驱动版本</span><b class="badge">${esc(gpu.driverVersion || '—')}</b></div>
    <div class="kv2"><span>硬件加速 GPU 计划</span><b class="badge ${hagsClass(gpu.hags)}">${esc(hagsLabel(gpu.hags))}</b></div>
    <button class="btn wide" data-action="open-hags" type="button">打开设置</button>`;
}

function renderSpoofPanel() {
  const spoof = state.spoof;
  const panel = $('[data-role="spoof"]');
  if (!spoof) {
    panel.innerHTML = '<div class="ctitle">显卡型号伪装</div><div class="hint">正在读取显卡记录…</div>';
    return;
  }
  const adapter = (spoof.adapters || [])[0] || {};
  const applied = spoof.applied || null;
  const current = applied?.appliedName || spoof.liveName || adapter.displayName || '—';
  const driver = adapter.driverName || '—';
  const presets = spoof.presets || [];
  panel.innerHTML = `
    <div class="ctitle">显卡型号伪装</div>
    <div class="kv2 spoof-name"><span>当前</span><b class="badge" title="${esc(current)}">${esc(current)}</b></div>
    <div class="kv2 spoof-name"><span>驱动记录</span><b class="badge" title="${esc(driver)}">${esc(driver)}</b></div>
    <p class="hint">需要管理员权限。修改型号后需要重启电脑。</p>
    <div class="spoof-picker">
      <select data-role="spoof-preset">${presets.map((preset) => `<option>${esc(preset)}</option>`).join('')}</select>
      <button class="btn primary wide" data-action="spoof-apply" type="button">应用伪装</button>
    </div>
    <button class="btn wide" data-action="spoof-restore" type="button" ${applied ? '' : 'disabled'}>还原为驱动名称</button>`;
}

function renderAssetsPanel() {
  const paths = state.paths || {};
  $('[data-role="assets"]').innerHTML = `
    <div class="ctitle">资产文件夹</div>
    <div class="hint">随包资产</div>
    <div class="path">${esc(prettyPath(paths.bundledPacksDirectory))}</div>
    <button class="btn wide" data-action="open-bundled" type="button">打开文件夹</button>
    <div class="hint">已下载资产</div>
    <div class="path">${esc(prettyPath(paths.packsDirectory))}</div>
    <button class="btn wide" data-action="open-data" type="button">打开文件夹</button>`;
}

function cardMarkup(game, index) {
  const mode = state.modes[game.id] || {};
  const framegen = mode.framegen || 'notDeployed';
  const deployed = framegen !== 'notDeployed';
  const cover = state.covers[game.id];
  const initial = game.name ? game.name.slice(0, 1).toUpperCase() : '?';
  const stateText = stateLabel(framegen);
  const stateClass = framegen === 'installed' ? 'ok' : (framegen === 'notDeployed' ? 'dim' : 'warn');
  const primary = framegen === 'outdated' ? '更新' : (deployed ? '还原' : '部署');
  return `
    <article class="gcard" style="animation-delay:${Math.min(index * 35, 350)}ms">
      <div class="gtop">
        ${cover
          ? `<img class="thumb" data-cover="${esc(game.id)}" src="${esc(cover)}" alt="">`
          : `<div class="thumb ph2" data-cover="${esc(game.id)}">${esc(initial)}</div>`}
        <div class="ginfo">
          <div class="gname">${esc(game.name)}</div>
          <div class="badges">
            <span class="badge">${esc(sourceLabel(game.source))}</span>
            <span class="badge">${esc(apiLabel(game.graphicsApi))}</span>
            <span class="badge state ${stateClass}">${esc(stateText)}</span>
            ${mode.smoothMotion === 'on' ? '<span class="badge mode">SM</span>' : ''}
            ${mode.anr && mode.anr !== 'notDeployed' ? '<span class="badge mode">ANR</span>' : ''}
          </div>
        </div>
      </div>
      <div class="gpath" title="${esc(prettyPath(game.targetDirectory))}">${esc(prettyPath(game.targetDirectory))}</div>
      <div class="gactions">
        <button class="btn small ${deployed ? 'danger' : 'success'}" data-action="${deployed ? 'restore' : 'deploy'}" data-id="${esc(game.id)}" type="button">${primary}</button>
        <button class="btn small launch" data-action="launch" data-id="${esc(game.id)}" type="button">启动游戏</button>
        <span class="spacer"></span>
        <button class="btn small icon-only" data-action="folder" data-id="${esc(game.id)}" type="button" title="打开文件夹" aria-label="打开文件夹">${folderIcon}</button>
        <button class="btn small danger icon-only" data-action="remove" data-id="${esc(game.id)}" type="button" title="移除" aria-label="移除">${trashIcon}</button>
      </div>
    </article>`;
}

function renderGames() {
  const list = $('[data-role="games"]');
  if (!state.games.length) {
    list.innerHTML = '';
    $('[data-role="status"]').textContent = '暂无游戏，点击“扫描游戏”开始。';
    return;
  }
  list.innerHTML = state.games.map(cardMarkup).join('');
  $('[data-role="status"]').textContent = `已加载 ${state.games.length} 个游戏`;
}

function gameById(id) {
  return state.games.find((game) => game.id === id);
}

function bindFramegenActions() {
  const view = $('#view');
  if (view.dataset.bound === '1') return;
  view.dataset.bound = '1';
  view.addEventListener('click', async (event) => {
    const target = event.target.closest('[data-action]');
    if (!target) return;
    const action = target.dataset.action;
    const game = gameById(target.dataset.id);
    try {
      if (action === 'scan') {
        await scan();
      } else if (action === 'add') {
        await addGame();
      } else if (action === 'launch' && game) {
        await call('launch', { gameId: game.id });
        toast(`已启动 ${game.name}`);
      } else if (action === 'folder' && game) {
        await call('openPath', { path: game.targetDirectory });
      } else if (action === 'library.remove' && game) {
        if (confirm(`从列表移除 ${game.name}？不会删除游戏文件。`)) {
          await call('library.remove', { gameId: game.id });
          await refresh();
        }
      } else if (action === 'deploy' && game) {
        openDeployDialog(game, refresh);
      } else if (action === 'restore' && game) {
        await restoreGame(game, refresh);
      } else if (action === 'open-hags') {
        await call('openHags');
      } else if (action === 'open-bundled') {
        await call('openFolder', { which: 'bundledPacks' });
      } else if (action === 'open-data') {
        await call('openFolder', { which: 'packs' });
      } else if (action === 'github') {
        event.preventDefault();
        window.fgm.openUrl('https://github.com/XiaoQAQ10086/FrameGen-Manager-Next');
      } else if (action === 'spoof-apply') {
        await applySpoof();
      } else if (action === 'spoof-restore') {
        await call('gpuSpoof.restore', { adapterIndex: 0, mode: 'backup' });
        toast('已还原驱动名称，重启后生效。');
        await refresh();
      }
    } catch (error) {
      toast(error.message, 'error');
    }
  });
}

async function scan() {
  showProgress('正在扫描游戏库');
  try {
    await call('library.scan');
    await refresh();
    toast('扫描完成');
  } catch (error) {
    toast(`扫描失败：${error.message}`, 'error');
  } finally {
    hideProgress();
  }
}

async function addGame() {
  const picked = await call('pickExecutable');
  if (picked.cancelled) return;
  await call('library.addManual', { path: picked.path });
  await refresh();
  toast('已添加游戏');
}

async function applySpoof() {
  const preset = $('[data-role="spoof-preset"]')?.value;
  if (!preset) return;
  if (!confirm(`把显卡型号伪装为 ${preset}？需要管理员权限，重启后生效。`)) return;
  await call('gpuSpoof.apply', { adapterIndex: 0, preset });
  toast('已写入，重启后生效。');
  await refresh();
}

async function refresh() {
  const [gpu, modes, games, spoof] = await Promise.all([
    call('gpu'),
    call('gameModes', {}),
    call('library.list'),
    call('gpuSpoof.inspect'),
  ]);
  state.gpu = gpu.gpu;
  state.modes = Object.fromEntries((modes.modes || []).map((mode) => [mode.gameId, mode]));
  state.games = games.games || [];
  state.spoof = spoof;
  renderFramegen();
  moveIndicator();
  const view = $('#view');
  if (view) view.scrollTop = 0;
  loadCovers();
}

$$('.nav-item').forEach((item) => item.addEventListener('click', () => selectSection(item.dataset.page)));
$('#lang-select').addEventListener('change', () => toast('语言切换将在后续版本接入'));
window.addEventListener('resize', moveIndicator);

boot().then(() => {
  if (new URLSearchParams(location.search).get('demo') === 'deploy' && state.games.length) {
    openDeployDialog(state.games[0], refresh);
  }
});
