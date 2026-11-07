import { checkForUpdates, openCloudDownload } from '../update.js';

export default {
  async mount(root, ctx) {
    const { api, t, ui } = ctx;
    const info = ctx.info || await api.request('app.info');
    root.innerHTML = `
      <section class="page about-page">
        <header class="page-head"><div><h1>${t('About.Title')}</h1><p>${t('About.Subtitle')}</p></div></header>
        <div class="panel form-panel">
          <div class="form-row"><div><h3>${t('About.Version')}</h3><p>v${info.version}</p></div></div>
          <div class="about-actions">
            <button class="btn success" data-action="check-update" type="button">${t('About.CheckUpdate')}</button>
            <button class="btn" data-action="cloud-download" type="button">${t('Update.Cloud')}</button>
          </div>
          <div class="form-row">
            <div><h3>GitHub</h3><p>${info.repository}</p></div>
            <button class="btn small" data-action="open-repo" type="button">${t('About.OpenGitHub')}</button>
          </div>
        </div>
        <div class="panel form-panel">
          <div class="ctitle">${t('About.Startup')}</div>
          <div class="form-row">
            <div><h3>${t('About.AutoScan')}</h3><p>${t('About.AutoScanHint')}</p></div>
            <label class="switch">
              <input type="checkbox" data-role="auto-scan" aria-label="${t('About.AutoScan')}"
                ${info.settings?.autoScanOnStartup ? 'checked' : ''}>
              <span></span>
            </label>
          </div>
        </div>
        <div class="panel form-panel">
          <div class="ctitle">${t('About.ReEngine')}</div>
          <div class="form-row">
            <div><h3>${t('About.ReEngineAuto')}</h3><p>${t('About.ReEngineAutoHint')}</p></div>
            <label class="switch">
              <input type="checkbox" data-role="auto-reframework" aria-label="${t('About.ReEngineAuto')}"
                ${info.settings?.autoInstallReframework !== false ? 'checked' : ''}>
              <span></span>
            </label>
          </div>
        </div>
        <div class="panel form-panel">
          <div class="ctitle">${t('About.Feedback')}</div>
          <p class="hint">${t('About.FeedbackBody')}</p>
        </div>
      </section>`;

    const autoScan = root.querySelector('[data-role="auto-scan"]');
    const autoReframework = root.querySelector('[data-role="auto-reframework"]');
    const saveSettings = async () => {
      autoScan.disabled = true;
      autoReframework.disabled = true;
      try {
        await api.request('app.saveSettings', {
          language: info.language === 'en' ? 'en' : 'zh',
          autoScanOnStartup: autoScan.checked,
          autoInstallReframework: autoReframework.checked,
        });
        await ctx.refreshInfo();
        ui.toast(t('Settings.Status.Saved'));
        return true;
      } catch (error) {
        ui.toast(error.message || error.code || t('Settings.Status.SaveFailed'), 'error');
        return false;
      } finally {
        autoScan.disabled = false;
        autoReframework.disabled = false;
      }
    };

    const onToggle = async () => {
      const desired = autoScan.checked;
      if (!(await saveSettings())) {
        autoScan.checked = !desired;
      }
    };
    const onReframeworkToggle = async () => {
      const desired = autoReframework.checked;
      if (!(await saveSettings())) {
        autoReframework.checked = !desired;
      }
    };
    autoScan.addEventListener('change', onToggle);
    autoReframework.addEventListener('change', onReframeworkToggle);

    const onClick = async (event) => {
      const button = event.target.closest('[data-action]');
      if (!button) return;
      if (button.dataset.action === 'check-update') {
        await checkForUpdates(ctx);
      }
      if (button.dataset.action === 'cloud-download') {
        openCloudDownload(ctx);
      }
      if (button.dataset.action === 'open-repo') {
        await api.request('app.openRepository').catch((error) => ui.toast(error.message, 'error'));
      }
    };
    root.addEventListener('click', onClick);
    return () => {
      root.removeEventListener('click', onClick);
      autoScan.removeEventListener('change', onToggle);
      autoReframework.removeEventListener('change', onReframeworkToggle);
    };
  },
};
