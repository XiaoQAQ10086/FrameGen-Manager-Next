const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('fgm', {
  call: (name, payload) => ipcRenderer.invoke('fgm:call', name, payload),
  openUrl: (url) => ipcRenderer.invoke('fgm:openUrl', url),
  quit: () => ipcRenderer.invoke('fgm:quit'),
  windowControl: (action) => ipcRenderer.invoke('fgm:window', action),
  perf: (entry) => ipcRenderer.send('fgm:perf', entry),
  feedbackSubmit: (payload) => ipcRenderer.invoke('fgm:feedbackSubmit', payload),
  feedbackHistory: () => ipcRenderer.invoke('fgm:feedbackHistory'),
  feedbackDelete: (id) => ipcRenderer.invoke('fgm:feedbackDelete', id),
});
