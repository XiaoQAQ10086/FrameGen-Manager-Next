import {
  $, apiLabel, call, esc, hideProgress, prettyPath, showProgress, state, toast,
} from './core.js';
import { t } from './l10n.js';

export async function restoreGame(game, refresh) {
  showProgress(t('restore'));
  try {
    await call('restore', { family: 'framegen.sm86', targetDir: game.targetDirectory });
    await refresh();
    toast(`${t('restore')}: ${game.name}`);
  } catch (error) {
    toast(`${t('restore')}: ${error.message}`, 'error');
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
      <p class="hint">${t('deployConfirmBody', { game: esc(game.name) })}</p>
      <div class="options">
        <div class="deploy-api"><span>${t('deployGraphicsApi')}</span><b class="badge">${esc(apiLabel(game.graphicsApi))}</b></div>
        <div class="deploy-api"><span>${t('deployFrameGenVersion')}</span><b class="badge">${esc(mode.framegenPackVersion || mode.framegenVersion || '—')}</b></div>
        <label><span>${t('optionProxySlot')}</span><div class="select-wrap"><select data-option="proxy">
          <option>version.dll</option><option>winmm.dll</option><option>dbghelp.dll</option>
          <option>dinput8.dll</option><option>dxgi.dll</option><option>d3d12.dll</option>
        </select></div></label>
        <label><span>${t('optionEngine')}</span><div class="select-wrap"><select data-option="engine"><option>DLSS</option><option>XeSS</option></select></div></label>
        <label><span>${t('optionFrames')}</span><div class="select-wrap"><select data-option="frames"><option value="3">4X</option><option value="2">3X</option><option value="1">2X</option></select></div></label>
        <label><span>${t('optionOptimized')}</span><div class="select-wrap"><select data-option="optimized"><option value="0">${t('optionOptimized0')}</option><option value="1" selected>${t('optionOptimized1')}</option><option value="2">${t('optionOptimized2')}</option><option value="3">${t('optionOptimized3')}</option></select></div></label>
        <label><span>${t('optionPreset')}</span><div class="select-wrap"><select data-option="preset"><option value="A">A</option><option value="B">B</option><option value="C">C</option></select></div></label>
        <label><span>${t('optionSuperResolution')}</span><div class="select-wrap"><select data-option="superResolution"><option value="Off">关闭</option><option value="M-INT8">M-INT8</option><option value="L-INT8">L-INT8</option><option value="M-FP16">M-FP16</option><option value="L-FP16">L-FP16</option></select></div></label>
        <label data-xess class="hidden"><span>${t('optionFlowResolution')}</span><div class="select-wrap"><select data-option="xessFlow"><option>Coarse</option><option selected>Middle</option><option>Fine</option><option>CoarseAt4K</option></select></div></label>
        <label data-xess class="hidden"><span>${t('optionUseHudless')}</span><div class="select-wrap"><select data-option="xessHudless"><option>Auto</option><option value="1">1</option><option value="0">0</option></select></div></label>
      </div>
      <p class="option-note hidden" data-xess-note>${t('deployXeSSNote')}</p>
      <footer class="modal-foot">
        <span class="muted small">${t('deployCloseGameHint')}</span>
        <div class="foot-actions">
          <button class="btn" data-restore type="button" ${deployed ? '' : 'disabled'}>${t('restore')}</button>
          <button class="btn success" data-deploy type="button">${t('deploy')}</button>
        </div>
      </footer>
    </section>`;
  $('#modal-root').appendChild(modal);
  const syncXess = () => {
    const engine = $('[data-option="engine"]', modal)?.value || 'DLSS';
    const xess = engine === 'XeSS';
    modal.querySelectorAll('[data-xess]').forEach((node) => node.classList.toggle('hidden', !xess));
    const note = $('[data-xess-note]', modal);
    if (note) note.classList.toggle('hidden', !xess);
    const optimized = $('[data-option="optimized"]', modal);
    const preset = $('[data-option="preset"]', modal);
    if (optimized) optimized.disabled = xess;
    if (preset) preset.disabled = xess;
  };
  const engineSelect = $('[data-option="engine"]', modal);
  if (engineSelect) engineSelect.onchange = syncXess;
  syncXess();
  call('proxyStatus', { targetDir: game.targetDirectory })
    .then((reply) => {
      const occupied = new Set((reply.proxies || [])
        .filter((entry) => !entry.ownedByFramegen)
        .map((entry) => String(entry.module).toLowerCase()));
      const select = $('[data-option="proxy"]', modal);
      if (!select) return;
      [...select.options].forEach((option) => {
        option.disabled = occupied.has(option.value.toLowerCase());
      });
      if (select.selectedOptions[0]?.disabled) {
        const free = [...select.options].find((option) => !option.disabled);
        if (free) select.value = free.value;
      }
    })
    .catch(() => {});
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
  const xessFlow = $('[data-option="xessFlow"]', modal)?.value || 'Middle';
  const xessHudless = $('[data-option="xessHudless"]', modal)?.value || 'Auto';
  const ini = [
    { section: 'FrameGeneration', key: 'Engine', value: engine },
    { section: 'FrameGeneration', key: 'MaxGeneratedFrames', value: String(frames) },
    { section: 'DLSSG', key: 'Optimized', value: String(optimized) },
    { section: 'DLSSG', key: 'Preset', value: preset },
    { section: 'SuperResolution', key: 'Mode', value: superResolution },
  ];
  if (engine === 'XeSS') {
    ini.push(
      { section: 'XeSSFG', key: 'FlowResolution', value: xessFlow },
      { section: 'XeSSFG', key: 'UseHudless', value: xessHudless },
    );
  }
  showProgress(t('deploy'));
  try {
    await call('deploy', {
      packId: 'framegen.sm86',
      targetDir: game.targetDirectory,
      family: 'framegen.sm86',
      proxySlot,
      gameExe: game.renderExe,
      gameId: game.id,
      ini,
    });
    await refresh();
    toast(`${t('deploy')}: ${game.name}`);
  } catch (error) {
    toast(`${t('deploy')}: ${error.message}`, 'error');
  } finally {
    hideProgress();
  }
}
