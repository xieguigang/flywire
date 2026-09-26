/**
 * render-formulas.cjs
 *
 * 从 docs/ 下的三篇博客文章中提取块级公式（$$...$$），
 * 用 MathJax 渲染为 SVG，再用 resvg-js 栅格化为透明底 PNG。
 *
 * 用法（NODE_PATH 指向 node 工作区的 node_modules）：
 *   NODE_PATH=C:\Users\Administrator\.workbuddy\binaries\node\workspace\node_modules node render-formulas.cjs
 */
'use strict';

const fs = require('fs');
const path = require('path');

const { mathjax } = require('mathjax-full/js/mathjax.js');
const { TeX } = require('mathjax-full/js/input/tex.js');
const { SVG } = require('mathjax-full/js/output/svg.js');
const { liteAdaptor } = require('mathjax-full/js/adaptors/liteAdaptor.js');
const { RegisterHTMLHandler } = require('mathjax-full/js/handlers/html.js');
const { AllPackages } = require('mathjax-full/js/input/tex/AllPackages.js');
const { Resvg } = require('@resvg/resvg-js');

// ---------------------------------------------------------------------------
// 配置：三篇文章 -> 输出文件夹 + 每条公式的章节标注
// ---------------------------------------------------------------------------

const DOCS_DIR = 'G:/flywire/docs';
const OUT_ROOT = 'G:/flywire/docs/formulas';

// 数学字体度量：1ex = 16px（em 约 32px），后续 zoom 2x 输出高清图
const EX_PX = 16;

const TARGETS = [
  {
    md: '01-当你的大脑只有0和1-脉冲神经网络.md',
    outDir: '01-脉冲神经网络',
    captions: [
      '第二章：传统人工神经网络的神经元',
      '3.1 节：LIF 模型（连续形式）',
      '3.1 节：LIF 模型（离散化更新公式）',
      '3.1 节：发放判定与重置',
      '4.1 节：发放率（Rate Coding）的定义',
    ],
  },
  {
    md: '02-139255个神经元和她的通话记录-果蝇全脑连接组FAFB-v783.md',
    outDir: '02-果蝇全脑连接组',
    captions: [
      '第四章：LIF 更新公式（邻居浇水项）',
      '第四章：发放判定与重置',
      '第二步：突触权重的结构归一化',
    ],
  },
  {
    md: '03-给果蝇通电让它替你玩贪吃蛇-缸中脑与数字永生.md',
    outDir: '03-缸中脑与数字永生',
    captions: [
      '1.3 节：平均发放率与活跃比例',
      '2.2 节：食物通道的距离衰减',
      '2.2 节：感觉神经元的注入电流',
      '2.4 节：运动神经元的滑动窗放电率',
      '2.4 节：运动组方向打分',
    ],
  },
];

// ---------------------------------------------------------------------------
// MathJax 初始化（liteAdaptor：无需浏览器 DOM）
// ---------------------------------------------------------------------------

const adaptor = liteAdaptor();
RegisterHTMLHandler(adaptor);

const texJax = new TeX({ packages: AllPackages, formatError: (jax, err) => {
  console.error('  [tex error]', err.message);
  return jax.formatError(err);
}});
const svgJax = new SVG({ fontCache: 'none' });
const doc = mathjax.document('', { InputJax: texJax, OutputJax: svgJax });

/** 提取文章中按顺序出现的全部块级公式 */
function extractDisplayFormulas(mdPath) {
  const text = fs.readFileSync(mdPath, 'utf-8');
  const out = [];
  const re = /\$\$([\s\S]+?)\$\$/g;
  let m;
  while ((m = re.exec(text)) !== null) {
    out.push(m[1].trim());
  }
  return out;
}

/** MathJax LaTeX -> 独立 SVG 字符串（显式 px 宽高，CJK 文本补字体族） */
function latexToSvg(latex) {
  const node = doc.convert(latex, {
    display: true,
    em: EX_PX * 2,
    ex: EX_PX,
    containerWidth: 120 * EX_PX * 2,
  });

  let svg = adaptor.innerHTML(node);
  if (!svg.includes('<svg')) {
    throw new Error('MathJax did not produce an SVG (compile error?)');
  }

  // 把 width/height 的 ex 单位换算成 px，交给 resvg 使用
  const wMatch = svg.match(/width="([\d.]+)ex"/);
  const hMatch = svg.match(/height="([\d.]+)ex"/);
  const vbMatch = svg.match(/viewBox="0 0 ([\d.]+) ([\d.]+)"/);

  let wPx, hPx;
  if (wMatch && hMatch) {
    wPx = parseFloat(wMatch[1]) * EX_PX;
    hPx = parseFloat(hMatch[1]) * EX_PX;
  } else if (vbMatch) {
    // viewBox 单位为 1/1000 em；1em = 2ex = 2*EX_PX px
    wPx = (parseFloat(vbMatch[1]) / 1000) * EX_PX * 2;
    hPx = (parseFloat(vbMatch[2]) / 1000) * EX_PX * 2;
  } else {
    throw new Error('cannot determine SVG size');
  }

  // 去掉 ex 单位的 width/height 与 style（usvg 解析不了 ex），换成显式 px
  svg = svg
    .replace(/\swidth="[\d.]+ex"/, ` width="${wPx.toFixed(2)}"`)
    .replace(/\sheight="[\d.]+ex"/, ` height="${hPx.toFixed(2)}"`)
    .replace(/\sstyle="[^"]*"/, '')
    .replace(/\smargin="[^"]*"/, '');

  // 中文 \text 内容会以 <text> 形式输出：显式指定系统中文字体
  // （注意：MathJax 的部分 <text> 已自带 font-family，重复添加会导致 resvg 解析失败）
  svg = svg.replace(/<text (?![^>]*font-family=)/g, '<text font-family="Microsoft YaHei" ');

  return svg;
}

/** SVG -> 透明底 PNG（2x 放大输出） */
function svgToPng(svg) {
  const resvg = new Resvg(svg, {
    fitTo: { mode: 'zoom', value: 2 },
    background: 'rgba(0,0,0,0)',
    font: {
      loadSystemFonts: true,
      defaultFontFamily: 'Microsoft YaHei',
    },
  });
  return resvg.render().asPng();
}

// ---------------------------------------------------------------------------
// 主流程
// ---------------------------------------------------------------------------

let total = 0, ok = 0;

for (const target of TARGETS) {
  const mdPath = path.join(DOCS_DIR, target.md);
  const outDir = path.join(OUT_ROOT, target.outDir);
  fs.mkdirSync(outDir, { recursive: true });

  const formulas = extractDisplayFormulas(mdPath);
  console.log(`\n== ${target.md}: ${formulas.length} display formula(s)`);

  const rows = ['| 图片 | 对应章节 | LaTeX 源码 |', '| --- | --- | --- |'];

  formulas.forEach((latex, i) => {
    total += 1;
    const name = `formula-${String(i + 1).padStart(2, '0')}.png`;
    try {
      const svg = latexToSvg(latex);
      const png = svgToPng(svg);
      fs.writeFileSync(path.join(outDir, name), png);
      ok += 1;
      const cap = target.captions[i] || '';
      rows.push(`| \`${name}\` | ${cap} | \`${latex.replace(/\|/g, '\\|').replace(/\n/g, ' ')}\` |`);
      console.log(`  [ok] ${name} (${cap || 'n/a'})`);
    } catch (err) {
      console.error(`  [FAIL] ${name}: ${err.message}`);
      rows.push(`| \`${name}\` | **渲染失败** | \`${latex.replace(/\|/g, '\\|').replace(/\n/g, ' ')}\` |`);
    }
  });

  const index = [
    `# 公式图片索引：${target.outDir}`,
    '',
    `来源文章：[\`${target.md}\`](../${target.md})，共 ${formulas.length} 条块级公式。`,
    '所有 PNG 均为透明底、2x 高清输出，可直接内嵌到网页中。',
    '',
    ...rows,
    '',
  ].join('\n');

  fs.writeFileSync(path.join(outDir, 'index.md'), index, 'utf-8');
}

console.log(`\ndone: ${ok}/${total} formula(s) rendered to ${OUT_ROOT}`);
if (ok !== total) process.exit(1);
