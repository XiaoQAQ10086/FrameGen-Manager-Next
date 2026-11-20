const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const os = require('node:os');

process.env.FGM_DATA_DIR ||= path.join(os.tmpdir(), 'fgm-electron-bridge-test');
const native = require('../native/fgm_node.node');

test('reports the product version', () => {
  assert.match(native.version(), /^\d+\.\d+\.\d+/);
});

test('returns the paths JSON envelope asynchronously', async () => {
  const reply = JSON.parse(await native.invoke('paths', {}));
  assert.equal(reply.ok, true);
  assert.ok(reply.dataRoot);
  assert.ok(reply.packsDirectory);
});

test('reports unknown commands', async () => {
  await assert.rejects(() => native.invoke('does.not.exist', {}), /unknown command/);
});
