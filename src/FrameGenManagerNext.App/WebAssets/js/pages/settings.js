export default {
  async mount(root, ctx) {
    const { api, t, ui } = ctx;
    const info = ctx.info || await api.request('app.info');

    root.innerHTML = `
      <section class="page">
        <header class="page-head">
          <div><h1>${t('Settings.Title')}</h1><p>${t('Settings.Subtitle')}</p></div>
          <div class="head-actions"><button class="btn primary" data-action="save" type="button">${t('Settings.Save')}</button></div>
        </header>
        <div class="panel form-panel">
          <div class="form-row">
            <div><h3>${t('Settings.Language')}</h3><p>${t('Settings.LanguageHint')}</p></div>
            <select data-field="language">
              <option value="zh" ${info.language === 'zh' ? 'selected' : ''}>简体中文</option>
              <option value="en" ${info.language === 'en' ? 'selected' : ''}>English</option>
            </select>
          </div>
          <div class="form-row">
            <div><h3>${t('Settings.ShowCovers')}</h3></div>
            <input type="checkbox" data-field="showCovers" ${info.settings.showCovers ? 'checked' : ''}>
          </div>
          <div class="form-row">
            <div><h3>${t('Settings.AutoScan')}</h3></div>
            <input type="checkbox" data-field="autoScanOnStartup" ${info.settings.autoScanOnStartup ? 'checked' : ''}>
          </div>
        </div>

        <div class="panel form-panel" style="margin-top:14px">
          <h3>${t('Settings.DataDirectory')}</h3>
          <div class="path-block"><code>${info.dataRoot}</code><button class="btn small" data-folder="root" type="button">${t('Common.Open')}</button></div>
          <div class="path-block"><code>${info.packsDirectory}</code><button class="btn small" data-folder="packs" type="button">${t('Common.Open')}</button></div>
          <div class="path-block"><code>${info.backupsDirectory}</code><button class="btn small" data-folder="backups" type="button">${t('Common.Open')}</button></div>
          <div class="path-block"><code>${info.configDirectory}</code><button class="btn small" data-folder="config" type="button">${t('Common.Open')}</button></div>
          <div class="path-block"><code>${info.logsDirectory}</code><button class="btn small" data-folder="logs" type="button">${t('Common.Open')}</button></div>
        </div>

        <div class="panel form-panel" style="margin-top:14px">
          <div class="form-row">
            <div><h3>${t('Settings.SystemShortcuts')}</h3><p>${t('Settings.HagsHint')}</p></div>
            <button class="btn" data-action="open-hags" type="button">${t('Settings.OpenHags')}</button>
          </div>
        </div>
      </section>`;

    const onClick = async (event) => {
      const button = event.target.closest('[data-action]');
      if (button?.dataset.action === 'open-hags') {
        await api.request('settings.openHags').catch((error) => ui.toast(error.message, 'error'));
        return;
      }
      const folder = event.target.closest('[data-folder]');
      if (folder) {
        const which = folder.dataset.folder;
        if (which === 'root') await api.request('app.openData').catch((error) => ui.toast(error.message, 'error'));
        else await api.request('settings.openFolder', { which }).catch((error) => ui.toast(error.message, 'error'));
        return;
      }
      if (!button) return;
      if (button.dataset.action === 'save') {
        const payload = {
          language: root.querySelector('[data-field="language"]').value,
          autoScanOnStartup: root.querySelector('[data-field="autoScanOnStartup"]').checked,
          showCovers: root.querySelector('[data-field="showCovers"]').checked,
        };
        try {
          await api.request('app.saveSettings', payload);
          await ctx.refreshInfo();
          await ctx.reloadAll();
          ui.toast(t('Settings.Status.Saved'));
        } catch (error) {
          ui.toast(error.message, 'error');
        }
      }
    };
    root.addEventListener('click', onClick);
    return () => root.removeEventListener('click', onClick);
  },
};