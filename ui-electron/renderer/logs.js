import { $, call, esc, toast } from './core.js';
import { t } from './l10n.js';

export async function renderLogs(root) {
  root.innerHTML = `
    <section class="page">
      <header class="listhead">
        <div class="lh-text">
          <h1>${t('navLogs')}</h1>
          <div class="sub">${t('logsSubtitle')}</div>
        </div>
        <div class="lh-actions">
          <button class="btn" data-action="open" type="button">${t('logsOpenFolder')}</button>
          <button class="btn" data-action="clear" type="button">${t('logsClear')}</button>
          <button class="btn scan" data-action="export" type="button">${t('diagnosticsExport')}</button>
        </div>
      </header>
      <pre class="log-box" data-role="log">${t('logsReading')}</pre>
    </section>`;
  await load(root);
  bind(root);
  root.dataset.ready = '1';
}

function format(line) {
  try {
    const value = JSON.parse(line);
    const date = new Date(Number(value.ts || 0));
    const stamp = `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}:${String(date.getSeconds()).padStart(2, '0')}`;
    const fields = Object.entries(value.fields || {})
      .map(([key, item]) => `${key}=${item}`)
      .join('  ');
    return `${stamp}  ${String(value.level || 'info').toUpperCase().padEnd(5)}  ${value.message || ''}${fields ? `  ${fields}` : ''}`;
  } catch {
    return line;
  }
}

async function load(root) {
  try {
    const reply = await call('logs.tail', { maxLines: 800 });
    const lines = reply.lines || [];
    $('[data-role="log"]', root).textContent = lines.length
      ? lines.map(format).join('\n')
      : t('logsEmpty');
  } catch (error) {
    $('[data-role="log"]', root).textContent = error.message;
  }
}

function bind(root) {
  root.onclick = async (event) => {
    const target = event.target.closest('[data-action]');
    if (!target) return;
    try {
      if (target.dataset.action === 'open') {
        await call('openFolder', { which: 'logs' });
      } else if (target.dataset.action === 'clear') {
        const reply = await call('logs.clear');
        await load(root);
        toast(reply.removed > 0
          ? t('logsCleared', { count: reply.removed })
          : t('logsNothingCleared'));
      } else if (target.dataset.action === 'export') {
        const reply = await call('diagnostics.export', {});
        if (reply.cancelled) return;
        toast(reply.ok ? t('diagnosticsSaved', { path: reply.path || '' }) : t('diagnosticsFailed'));
      }
    } catch (error) {
      toast(error.message, 'error');
    }
  };
}
