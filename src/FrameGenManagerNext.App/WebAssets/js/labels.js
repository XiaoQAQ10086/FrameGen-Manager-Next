export const launcherKeys = {
  Steam: 'Common.Steam',
  Epic: 'Common.Epic',
  WeGame: 'Common.WeGame',
  Loose: 'Common.Loose',
  Manual: 'Common.Manual',
};

export const stateKeys = {
  Installed: 'State.Installed',
  Outdated: 'State.Outdated',
  Partial: 'State.Partial',
  Conflict: 'State.Conflict',
  InstalledWithoutManifest: 'State.InstalledWithoutManifest',
  NotDeployed: 'State.NotDeployed',
};

export const componentKeys = {
  Installed: 'SmoothMotion.Component.Deployed',
  Outdated: 'SmoothMotion.Component.Outdated',
  Partial: 'SmoothMotion.Component.Partial',
  Conflict: 'SmoothMotion.Component.Conflict',
  InstalledWithoutManifest: 'SmoothMotion.Component.Present',
  NotDeployed: 'State.NotDeployed',
};

export const routeKeys = {
  Sm86: 'Gpu.Route.Sm86',
  Sm75: 'Gpu.Route.Sm75',
  Gtx16: 'Gpu.Route.Gtx16',
  NoTensorCore: 'Gpu.Route.NoTensorCore',
  NotNeeded: 'Gpu.Route.NotNeeded',
  Unsupported: 'Gpu.Route.Unsupported',
  Unknown: 'Gpu.Route.Unknown',
};

export function apiLabel(api) {
  return api?.startsWith('Direct3D') ? api.replace('Direct3D', 'DX') : api || 'Unknown';
}

export function prettyPath(value) {
  return String(value ?? '').replace(/^([a-z]):/, (_, drive) => `${drive.toUpperCase()}:`);
}

export function stateLabel(t, state) {
  return t(stateKeys[state] || 'State.NotDeployed');
}

export function launcherLabel(t, launcher) {
  return t(launcherKeys[launcher] || 'Common.Unknown');
}
