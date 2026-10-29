let strings = {};
let language = 'zh';

export function setStrings(map, lang) {
  strings = map || {};
  if (lang) language = lang;
  document.documentElement.lang = language === 'en' ? 'en' : 'zh-CN';
  document.querySelectorAll('[data-i18n]').forEach((element) => {
    const key = element.dataset.i18n;
    element.textContent = t(key);
  });
  document.querySelectorAll('[data-nav-label]').forEach((element) => {
    element.textContent = t(element.dataset.navLabel);
  });
}

export function t(key, fallback = '') {
  return strings[key] ?? fallback ?? key;
}

export function lang() {
  return language;
}
