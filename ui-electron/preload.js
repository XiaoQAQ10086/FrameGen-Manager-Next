const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('fgm', {
  call: (name, payload) => ipcRenderer.invoke('fgm:call', name, payload),
  openUrl: (url) => ipcRenderer.invoke('fgm:openUrl', url),
});
