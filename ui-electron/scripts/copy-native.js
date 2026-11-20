const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const source = path.join(root, '..', 'rust', 'target', 'release', 'fgm_node.dll');
const target = path.join(root, 'native', 'fgm_node.node');

fs.mkdirSync(path.dirname(target), { recursive: true });
fs.copyFileSync(source, target);
console.log(`native addon: ${target}`);
