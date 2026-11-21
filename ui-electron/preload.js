const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('fgm', {
  call: (name, payload) => ipcRenderer.invoke('fgm:call', name, payload),
  openUrl: (url) => ipcRenderer.invoke('fgm:openUrl', url),
  quit: () => ipcRenderer.invoke('fgm:quit'),
  windowControl: (action) => ipcRenderer.invoke('fgm:window', action),
});
