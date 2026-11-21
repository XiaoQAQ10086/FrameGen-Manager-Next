import { $, call, esc, hideProgress, showProgress, toast } from './core.js';
import { t } from './l10n.js';
import { showUpdateDialog } from './update.js';

let settings = {};
let paths = {};

export async function renderAbout(root) {
  try {
    const [settingsReply, pathsReply] = await Promise.all([
      call('settings.get'),
      call('paths'),
    ]);
    settings = settingsReply.settings || {};
    paths = pathsReply || {};
  } catch (error) {
    toast(error.message, 'error');
  }
  root.innerHTML = `
    <section class="page about-page">
      <header class="page-head"><h1>${t('navAbout')}</h1></header>
      <div class="form-panel">
        <section class="panel">
          <div class="ctitle">${t('aboutSoftwareVersion')}</div>
          <div class="kv2 stacked"><b class="about-version">v2.3.4</b><span>${t('aboutDescription')}</span></div>
          <div class="about-actions">
            <button class="btn success" data-action="update" type="button">${t('aboutCheckUpdates')}</button>
            <button class="btn" data-action="quark" type="button">${t('aboutQuark')}</button>
            <button class="btn" data-action="baidu" type="button">${t('aboutBaidu')}</button>
          </div>
          <div class="about-links">
            <section class="panel about-link">
              <div class="ctitle">${t('aboutGithub')}</div>
              <div class="path" title="${esc(paths.repository || '')}">${esc(paths.repository || '')}</div>
              <button class="btn wide" data-action="github" type="button">${t('aboutOpenRepo')}</button>
            </section>
            <section class="panel about-link">
              <div class="ctitle">${t('aboutLicenses')}</div>
              <p class="hint">${t('aboutLicensesHint')}</p>
              <button class="btn wide" data-action="licenses" type="button">${t('aboutOpenLicenses')}</button>
            </section>
          </div>
        </section>
        <section class="panel">
          <div class="ctitle">${t('aboutStartup')}</div>
          <div class="kv2"><span>${t('aboutStartupScan')}<br><small class="muted">${t('aboutStartupScanHint')}</small></span>
            <label class="switch"><input type="checkbox" data-setting="autoScanOnStartup" ${settings.autoScanOnStartup ? 'checked' : ''}><span></span></label></div>
        </section>
        <section class="panel">
          <div class="ctitle">${t('aboutReEngine')}</div>
          <div class="kv2"><span>${t('aboutReEngineInstall')}<br><small class="muted">${t('aboutReEngineHint')}</small></span>
            <label class="switch"><input type="checkbox" data-setting="autoInstallReframework" ${settings.autoInstallReframework !== false ? 'checked' : ''}><span></span></label></div>
        </section>
        <section class="panel">
          <div class="ctitle">${t('aboutFeedback')}</div>
          <p class="hint">${t('aboutFeedbackBody')}</p>
        </section>
      </div>
    </section>`;
  bind(root);
}

function bind(root) {
  root.onchange = async (event) => {
    const input = event.target.closest('[data-setting]');
    if (!input) return;
    settings = { ...settings, [input.dataset.setting]: input.checked };
    try {
      await call('settings.set', settings);
      toast(t('settingsSaved'));
    } catch (error) {
      toast(error.message, 'error');
    }
  };
  root.onclick = async (event) => {
    const target = event.target.closest('[data-action]');
    if (!target) return;
    const action = target.dataset.action;
    try {
      if (action === 'github') {
        window.fgm.openUrl(paths.repository || 'https://github.com/XiaoQAQ10086/FrameGen-Manager-Next');
      } else if (action === 'quark') {
        window.fgm.openUrl(paths.netdiskUrl || 'https://pan.quark.cn');
      } else if (action === 'baidu') {
        window.fgm.openUrl('https://pan.baidu.com/s/1hL4B4WJEV0ui6hVqxiPvRg?pwd=1234');
      } else if (action === 'licenses') {
        await call('openFolder', { which: 'licenses' });
      } else if (action === 'update') {
        const reply = await call('update.check');
        if (!reply.available) {
          toast(t('updateLatest'));
        } else {
          alert(`${t('updateAvailable', { version: reply.version })}\n\n${reply.notes || ''}`);
        }
      }
    } catch (error) {
      toast(`${t('updateFailed', { code: error.code || '' })}: ${error.message}`, 'error');
    }
  };
}
