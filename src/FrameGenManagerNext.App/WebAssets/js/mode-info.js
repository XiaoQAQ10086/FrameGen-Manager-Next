export const modeSources = {
  framegen: 'https://github.com/sdli1995/dlssg_for_sm86',
  smoothMotion: 'https://github.com/pipotoufikxyz-lgtm/dlssg_for_sm86-MFG-version',
  dlss5: 'https://github.com/ghostinthecamera/GITC-Uplift',
  anr: 'https://github.com/apex-compute/ANR',
};

export function githubSource(t, url) {
  return `<button class="mode-source" type="button" data-action="github" data-url="${url}">
    <img class="github-icon" src="github.png" alt="" />
    <span>${t('Mode.GithubSource')}</span>
  </button>`;
}
