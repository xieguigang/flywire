/**
 * analyze-png.cjs —— 检查公式 PNG 的像素统计（透明比例 / 前景色 / 内容包围盒）
 */
'use strict';
const fs = require('fs');
const { PNG } = require('pngjs');

for (const p of process.argv.slice(2)) {
  const png = PNG.sync.read(fs.readFileSync(p));
  const width = png.width, height = png.height, data = png.data;
  let opaque = 0, dark = 0, light = 0;
  let minX = width, maxX = 0, minY = height, maxY = 0;
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const idx = (y * width + x) * 4;
      const a = data[idx + 3];
      if (a > 200) {
        opaque++;
        const sum = data[idx] + data[idx + 1] + data[idx + 2];
        if (sum < 150) dark++; else if (sum > 600) light++;
        if (x < minX) minX = x; if (x > maxX) maxX = x;
        if (y < minY) minY = y; if (y > maxY) maxY = y;
      }
    }
  }
  const pct = (opaque / (width * height) * 100).toFixed(2);
  const name = p.replace(/\\/g, '/').split('/').slice(-2).join('/');
  console.log(name + ': ' + width + 'x' + height + ', opaque=' + pct + '%, dark=' + dark + ', light=' + light + ', box=[' + minX + ',' + minY + ']-[' + maxX + ',' + maxY + ']');
}
