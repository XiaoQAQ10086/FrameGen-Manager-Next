import {
  $, apiLabel, call, esc, hideProgress, prettyPath, showProgress, state, toast,
} from './core.js';

export async function restoreGame(game, refresh) {
  showProgress('正在还原');
  try {
    await call('restore', { family: 'framegen.sm86', targetDir: game.targetDirectory });
    await refresh();
    toast(`已还原 ${game.name}`);
  } catch (error) {
    toast(`还原失败：${error.message}`, 'error');
  } finally {
    hideProgress();
  }
}

export function openDeployDialog(game, refresh) {
  const mode = state.modes[game.id] || {};
  const deployed = (mode.framegen || 'notDeployed') !== 'notDeployed';
  const modal = document.createElement('div');
  modal.className = 'overlay';
  modal.innerHTML = `
    <section class="modal deploy-modal" role="dialog" aria-modal="true">
      <header class="modal-head">
        <div class="modal-game-head">
          <div class="modal-cover">${state.covers[game.id] ? `<img src="${esc(state.covers[game.id])}" alt="">` : esc(game.name.slice(0, 1))}</div>
          <div><h2>${esc(game.name)}</h2><p class="muted">${esc(prettyPath(game.targetDirectory))}</p></div>
        </div>
        <button class="icon-btn" data-close type="button">✕</button>
      </header>
      <div class="deploy-meta">
        <div class="kv2"><span>图形 API</span><b class="badge">${esc(apiLabel(game.graphicsApi))}</b></div>
      </div>
      <p class="hint">确定为 ${esc(game.name)} 部署帧生成组件吗？</p>
      <div class="options">
        <label><span>代理入口</span><div class="select-wrap"><select data-option="proxy">
          <option>version.dll</option><option>winmm.dll</option><option>dbghelp.dll</option>
          <option>dinput8.dll</option><option>dxgi.dll</option><option>d3d12.dll</option>
        </select></div></label>
        <label><span>插帧引擎</span><div class="select-wrap"><select data-option="engine"><option>DLSS</option><option>XeSS</option></select></div></label>
        <label><span>生成帧数</span><div class="select-wrap"><select data-option="frames"><option value="3">4X</option><option value="2">3X</option><option value="1">2X</option></select></div></label>
        <label><span>优化档位</span><div class="select-wrap"><select data-option="optimized"><option value="1">1 · 加速，与官方逐位一致</option><option value="0">0 · 原版内核</option><option value="2">2 · 更快，轻微损失</option><option value="3">3 · 最快，全部损失</option></select></div></label>
        <label><span>渲染预设</span><div class="select-wrap"><select data-option="preset"><option value="A">A</option><option value="B">B</option><option value="C">C</option></select></div></label>
        <label><span>超分辨率</span><div class="select-wrap"><select data-option="superResolution"><option value="Off">关闭</option><option value="M-INT8">M-INT8</option><option value="L-INT8">L-INT8</option><option value="M-FP16">M-FP16</option><option value="L-FP16">L-FP16</option></select></div></label>
      </div>
      <footer class="modal-foot">
        <span class="muted small">部署前请关闭游戏</span>
        <div class="foot-actions">
          <button class="btn" data-restore type="button" ${deployed ? '' : 'disabled'}>还原</button>
          <button class="btn success" data-deploy type="button">部署</button>
        </div>
      </footer>
    </section>`;
  $('#modal-root').appendChild(modal);
  const close = () => modal.remove();
  $('[data-close]', modal).onclick = close;
  modal.onclick = (event) => { if (event.target === modal) close(); };
  $('[data-restore]', modal).onclick = async () => {
    close();
    await restoreGame(game, refresh);
  };
  $('[data-deploy]', modal).onclick = async () => {
    close();
    await deployGame(game, modal, refresh);
  };
}

async function deployGame(game, modal, refresh) {
  const proxySlot = $('[data-option="proxy"]', modal)?.value || 'version.dll';
  const engine = $('[data-option="engine"]', modal)?.value || 'DLSS';
  const frames = Number($('[data-option="frames"]', modal)?.value || 3);
  const optimized = Number($('[data-option="optimized"]', modal)?.value || 1);
  const preset = $('[data-option="preset"]', modal)?.value || 'A';
  const superResolution = $('[data-option="superResolution"]', modal)?.value || 'Off';
  showProgress('正在部署帧生成组件');
  try {
    await call('deploy', {
      packId: 'framegen.sm86',
      targetDir: game.targetDirectory,
      family: 'framegen.sm86',
      proxySlot,
      gameExe: game.renderExe,
      gameId: game.id,
      ini: [
        { section: 'FrameGeneration', key: 'Engine', value: engine },
        { section: 'FrameGeneration', key: 'MaxGeneratedFrames', value: String(frames) },
        { section: 'DLSSG', key: 'Optimized', value: String(optimized) },
        { section: 'DLSSG', key: 'Preset', value: preset },
        { section: 'SuperResolution', key: 'Mode', value: superResolution },
        { section: 'XeSSFG', key: 'FlowResolution', value: 'Coarse' },
        { section: 'XeSSFG', key: 'UseHudless', value: 'Auto' },
      ],
    });
    await refresh();
    toast(`已部署 ${game.name}`);
  } catch (error) {
    toast(`部署失败：${error.message}`, 'error');
  } finally {
    hideProgress();
  }
}
