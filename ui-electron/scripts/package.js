const fs = require('node:fs');
const path = require('node:path');
const asar = require('@electron/asar');
const { rcedit } = require('rcedit');

const root = path.resolve(__dirname, '..');
const electronDist = path.dirname(require('electron'));
const outputRoot = path.join(root, 'dist');
const stage = path.join(outputRoot, 'FrameGen-Manager-Next');
const appStage = path.join(outputRoot, 'app-stage');

async function main() {
  fs.rmSync(stage, { recursive: true, force: true });
  fs.rmSync(appStage, { recursive: true, force: true });
  fs.mkdirSync(outputRoot, { recursive: true });
  fs.cpSync(electronDist, stage, { recursive: true });

  const exe = path.join(stage, 'electron.exe');
  const targetExe = path.join(stage, 'FrameGen-Manager-Next.exe');
  await rcedit(exe, {
    icon: path.join(root, 'renderer', 'app.ico'),
    'version-string': {
      CompanyName: 'dev.framegen',
      FileDescription: 'FrameGen Manager-Next',
      ProductName: 'FrameGen Manager-Next',
      OriginalFilename: 'FrameGen-Manager-Next.exe',
    },
    'file-version': '2.3.4',
    'product-version': '2.3.4',
  });
  fs.renameSync(exe, targetExe);

  fs.rmSync(path.join(stage, 'resources', 'default_app.asar'), { force: true });
  pruneLocales(path.join(stage, 'locales'));

  fs.mkdirSync(appStage, { recursive: true });
  for (const name of ['main.js', 'preload.js', 'renderer', 'native']) {
    fs.cpSync(path.join(root, name), path.join(appStage, name), { recursive: true });
  }
  fs.writeFileSync(
    path.join(appStage, 'package.json'),
    JSON.stringify(
      {
        name: 'framegen-manager-next',
        productName: 'FrameGen Manager-Next',
        version: '2.3.4',
        main: 'main.js',
      },
      null,
      2,
    ),
  );
  await asar.createPackageWithOptions(appStage, path.join(stage, 'resources', 'app.asar'), {
    unpack: 'native/*',
  });
  fs.rmSync(appStage, { recursive: true, force: true });

  for (const name of ['packs', 'licenses']) {
    const source = path.join(root, '..', name);
    if (fs.existsSync(source)) {
      fs.cpSync(source, path.join(stage, name), { recursive: true });
    }
  }

  const size = directorySize(stage);
  console.log(`portable folder: ${stage}`);
  console.log(`unpacked size: ${(size / 1024 / 1024).toFixed(1)} MB`);
}

function pruneLocales(directory) {
  if (!fs.existsSync(directory)) return;
  const keep = new Set(['en-US.pak', 'zh-CN.pak']);
  for (const entry of fs.readdirSync(directory)) {
    if (!keep.has(entry)) fs.rmSync(path.join(directory, entry), { force: true });
  }
}

function directorySize(directory) {
  let total = 0;
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    const full = path.join(directory, entry.name);
    total += entry.isDirectory() ? directorySize(full) : fs.statSync(full).size;
  }
  return total;
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
