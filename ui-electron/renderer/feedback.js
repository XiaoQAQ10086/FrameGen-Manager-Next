import { $, call, esc, state, toast } from './core.js';
import { t } from './l10n.js';

let kind = 'issue';
let unread = false;

function feedbackMeta() {
  return {
    os: navigator.userAgent,
    arch: 'x64',
    gpu: state.gpu?.name || 'unknown',
    driver: state.gpu?.driverVersion || 'unknown',
    lang: state.settings?.language || navigator.language,
    version: ($('#app-version')?.textContent || '').replace(/^v/i, ''),
  };
}

function submitLabel() {
  return kind === 'suggestion' ? t('feedbackSubmitSuggestion') : t('feedbackSubmitIssue');
}

function updateSubmit(panel) {
  const description = $('[data-role="feedback-description"]', panel)?.value.trim() || '';
  const button = $('[data-action="feedback-submit"]', panel);
  if (!button) return;
  button.disabled = !description;
  button.textContent = submitLabel();
}

function showState(panel, text, danger = false, loading = false) {
  const node = $('[data-role="feedback-state"]', panel);
  if (!node) return;
  node.className = `feedback-state${danger ? ' danger' : ''}`;
  node.innerHTML = `${loading ? '<span class="spinner"></span>' : ''}<span>${esc(text)}</span>`;
}

export function renderFeedback(root) {
  const panel = $('[data-role="feedback"]', root);
  if (!panel) return;
  kind = 'issue';
  panel.innerHTML = `
    <div class="ctitle">${t('feedbackTitle')}</div>
    <div class="feedback-types" role="group" aria-label="${esc(t('feedbackTypeLabel'))}">
      <button class="feedback-type active" data-feedback-type="issue" type="button">${t('feedbackTypeIssue')}</button>
      <button class="feedback-type" data-feedback-type="suggestion" type="button">${t('feedbackTypeSuggestion')}</button>
    </div>
    <label class="feedback-field"><span>${t('feedbackContact')}</span>
      <input data-role="feedback-contact" maxlength="200" placeholder="${esc(t('feedbackContactPlaceholder'))}">
    </label>
    <label class="feedback-field"><span>${t('feedbackDescription')}</span>
      <textarea data-role="feedback-description" maxlength="5000" rows="5" placeholder="${esc(t('feedbackPlaceholder'))}"></textarea>
    </label>
    <p class="feedback-notice">${t('feedbackNotice')}</p>
    <div class="feedback-actions">
      <span class="feedback-state" data-role="feedback-state">${t('feedbackLogAttached')}</span>
      <button class="btn" data-action="feedback-history" type="button">${t('feedbackHistory')}</button>
      <button class="btn success" data-action="feedback-submit" type="button" disabled>${submitLabel()}</button>
    </div>`;

  panel.querySelectorAll('[data-feedback-type]').forEach((button) => {
    button.addEventListener('click', () => {
      kind = button.dataset.feedbackType === 'suggestion' ? 'suggestion' : 'issue';
      panel.querySelectorAll('[data-feedback-type]').forEach((item) => {
        item.classList.toggle('active', item === button);
      });
      updateSubmit(panel);
    });
  });
  $('[data-role="feedback-description"]', panel).addEventListener('input', () => updateSubmit(panel));
  $('[data-action="feedback-submit"]', panel).addEventListener('click', () => submit(panel));
  $('[data-action="feedback-history"]', panel).addEventListener('click', () => openHistory());
  applyFeedbackDots();
}

async function submit(panel) {
  const description = $('[data-role="feedback-description"]', panel).value.trim();
  if (!description) {
    showState(panel, t('feedbackDescriptionRequired'), true);
    $('[data-role="feedback-description"]', panel).focus();
    return;
  }
  const button = $('[data-action="feedback-submit"]', panel);
  button.disabled = true;
  showState(panel, t('feedbackPreparing'), false, true);
  try {
    const bundle = await call('feedback.bundle');
    showState(panel, t('feedbackUploading'), false, true);
    const reply = await window.fgm.feedbackSubmit({
      type: kind,
      description,
      contact: $('[data-role="feedback-contact"]', panel).value.trim(),
      meta: feedbackMeta(),
      version: feedbackMeta().version,
      logPath: bundle.path,
    });
    if (!reply.ok) {
      showState(panel, t('feedbackFailed', { message: reply.error?.message || '' }), true);
      button.disabled = false;
      button.textContent = t('feedbackRetry');
      return;
    }
    showState(panel, t('feedbackSuccess', { id: reply.id }));
    button.textContent = t('feedbackSubmitted');
    button.disabled = true;
    await refreshFeedbackBadge();
  } catch (error) {
    showState(panel, t('feedbackFailed', { message: error.message }), true);
    button.disabled = false;
    button.textContent = t('feedbackRetry');
  }
}

function historyRow(item) {
  const status = item.reply && item.statusText === t('feedbackStatusPending') ? t('feedbackReplied') : item.statusText;
  return `
    <article class="feedback-history-row">
      <div class="feedback-history-top"><b>${esc(item.id)}</b><span class="badge">${esc(status)}</span></div>
      <div class="feedback-history-summary">${esc(item.summary)}</div>
      <p class="hint">${esc(item.reply || t('feedbackNoReply'))}</p>
      <button class="btn small danger" data-delete-local="${esc(item.id)}" type="button">${t('feedbackDeleteLocal')}</button>
    </article>`;
}

async function openHistory() {
  unread = false;
  applyFeedbackDots();
  const overlay = document.createElement('div');
  overlay.className = 'overlay';
  overlay.innerHTML = `
    <section class="modal feedback-history-modal" role="dialog" aria-modal="true">
      <header class="modal-head"><div><h2>${t('feedbackHistory')}</h2><p class="muted">${t('feedbackHistoryHint')}</p></div>
        <button class="icon-btn" data-close type="button">✕</button></header>
      <div class="feedback-history-list" data-role="feedback-history-list"><div class="feedback-loading"><span class="spinner"></span>${t('feedbackLoading')}</div></div>
      <footer class="modal-foot"><span></span><div class="foot-actions">
        <button class="btn" data-refresh type="button">${t('feedbackRefresh')}</button>
      </div></footer>
    </section>`;
  $('#modal-root').appendChild(overlay);
  const close = () => overlay.remove();
  overlay.querySelector('[data-close]').onclick = close;
  overlay.addEventListener('click', (event) => { if (event.target === overlay) close(); });
  const list = overlay.querySelector('[data-role="feedback-history-list"]');
  const load = async (markRead = false) => {
    list.innerHTML = `<div class="feedback-loading"><span class="spinner"></span>${t('feedbackLoading')}</div>`;
    const reply = await window.fgm.feedbackHistory({ markRead });
    const items = reply.items || [];
    const warning = reply.syncFailed
      ? `<div class="feedback-sync-warning">${t('feedbackSyncFailed')}</div>`
      : '';
    list.innerHTML = warning + (items.length
      ? items.map(historyRow).join('')
      : `<div class="feedback-history-empty">${t('feedbackNoHistory')}</div>`);
    if (markRead) {
      unread = false;
      applyFeedbackDots();
    }
  };
  overlay.querySelector('[data-refresh]').onclick = () => load(true);
  list.addEventListener('click', async (event) => {
    const target = event.target.closest('[data-delete-local]');
    if (!target) return;
    await window.fgm.feedbackDelete(target.dataset.deleteLocal);
    await load(false);
  });
  await load(true);
}

function applyFeedbackDots() {
  const onLogs = Boolean(document.querySelector('.page-host[data-section="logs"]'));
  document.querySelector('.nav-item[data-page="logs"]')?.classList.toggle('has-feedback', unread && !onLogs);
  document.querySelector('[data-action="feedback-history"]')?.classList.toggle('has-feedback', unread);
}

export async function refreshFeedbackBadge() {
  try {
    const reply = await window.fgm.feedbackHistory();
    unread = Boolean(reply.hasUnread);
  } catch {
    unread = false;
  }
  applyFeedbackDots();
}
