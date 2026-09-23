const fs = require('fs');
const cheerio = require('cheerio');

const SRC = 'g:/flywire/docs/Download Data.html';
const OUT = 'g:/flywire/docs/Download Data.md';

const BR = '\u0001'; // sentinel for <br>, restored to a Markdown hard break at the end

const html = fs.readFileSync(SRC, 'utf8');
const $ = cheerio.load(html, { decodeEntities: true });

const SKIP = new Set(['script', 'style', 'noscript', 'template', 'link', 'meta', 'button']);

function slugify(s) {
  return s
    .toLowerCase()
    .replace(/&/g, ' and ')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}

const modalAnchors = {};
$('div.modal').each((i, el) => {
  const $m = $(el);
  const id = $m.attr('id');
  const title = clean($m.find('.modal-title').first().text());
  if (id && title) modalAnchors[id] = slugify(title);
});

function clean(s) {
  return s
    .replace(/\u00a0/g, ' ')
    .replace(/[ \t]+/g, ' ')
    .replace(/ *\n */g, '\n')
    .replace(/\n{3,}/g, '\n\n')
    .replace(/^[\s\u0001]+/, '')
    .replace(/[\s\u0001]+$/, '');
}

function isFaIcon(node) {
  return node.type === 'tag' && node.name === 'i' && /fa[-a-z]/.test(node.attribs?.class || '');
}

function inline(node) {
  if (node.type === 'text') return node.data.replace(/\u00a0/g, ' ').replace(/\s+/g, ' ');
  if (node.type !== 'tag') return '';
  const name = node.name.toLowerCase();
  if (SKIP.has(name)) return '';
  if (name === 'br') return BR;
  const inner = () => (node.children || []).map(inline).join('');
  switch (name) {
    case 'b':
    case 'strong': {
      const t = inner().trim();
      return t ? '**' + t + '**' : '';
    }
    case 'em':
    case 'i': {
      if (isFaIcon(node)) return '';
      const t = inner().trim();
      return t ? '*' + t + '*' : '';
    }
    case 'a': {
      const text = inner().replace(/\s+/g, ' ').trim();
      let href = node.attribs?.href || '';
      const target = node.attribs?.['data-target'] || '';
      if (target.startsWith('#')) {
        const anchor = modalAnchors[target.slice(1)];
        if (anchor) href = '#' + anchor;
      }
      if (!href) return text;
      return '[' + (text || href) + '](' + href + ')';
    }
    default:
      return inner();
  }
}

function collectTr(node, rows) {
  for (const child of node.children || []) {
    if (child.type !== 'tag') continue;
    if (child.name === 'tr') rows.push(child);
    else if (child.name === 'thead' || child.name === 'tbody' || child.name === 'tfoot') collectTr(child, rows);
  }
}

function renderTable(node, out) {
  const rows = [];
  collectTr(node, rows);
  if (!rows.length) return;
  const cellsOf = (r) =>
    (r.children || [])
      .filter((c) => c.type === 'tag' && (c.name === 'td' || c.name === 'th'))
      .map((c) =>
        inline(c)
          .split(BR)
          .join(' ')
          .replace(/\s+/g, ' ')
          .replace(/\|/g, '\\|')
          .trim()
      );
  const hasTh = rows.some((r) => (r.children || []).some((c) => c.name === 'th'));
  const body = rows.map(cellsOf);
  const width = Math.max(...body.map((r) => r.length));
  const pad = (r) => {
    const a = r.slice();
    while (a.length < width) a.push('');
    return a;
  };
  const lines = [];
  lines.push('| ' + Array(width).fill('').join(' | ') + ' |');
  lines.push('| ' + Array(width).fill('---').join(' | ') + ' |');
  for (let i = hasTh ? 1 : 0; i < body.length; i++) lines.push('| ' + pad(body[i]).join(' | ') + ' |');
  out.push(lines.join('\n'));
}

function renderList(node, out, ordered) {
  let i = 1;
  for (const li of node.children || []) {
    if (li.type !== 'tag' || li.name !== 'li') continue;
    const t = clean(inline(li));
    if (t) out.push((ordered ? i++ + '. ' : '- ') + t);
  }
}

function renderBlocks(node, out) {
  let buf = '';
  const flush = () => {
    const t = clean(buf);
    if (t) out.push(t);
    buf = '';
  };
  for (const child of node.children || []) {
    if (child.type === 'text') {
      buf += inline(child);
      continue;
    }
    if (child.type !== 'tag') continue;
    const name = child.name.toLowerCase();
    const cls = child.attribs?.class || '';
    if (SKIP.has(name)) continue;
    if (name === 'br') {
      buf += BR;
      continue;
    }
    if (name === 'table') {
      flush();
      renderTable(child, out);
      continue;
    }
    if (name === 'hr') {
      flush();
      out.push('---');
      continue;
    }
    if (/^h[1-6]$/.test(name)) {
      flush();
      out.push('#'.repeat(Number(name[1])) + ' ' + clean(inline(child)));
      continue;
    }
    if (name === 'a' && /\bbtn\b/.test(cls)) {
      flush();
      const href = child.attribs?.href || '';
      const text = clean(inline(child));
      if (/file info/i.test(text)) out.push('- File info: <' + href + '>');
      else if (child.attribs?.download !== undefined) out.push('- Download: ' + text);
      else if (text) out.push('- ' + text);
      continue;
    }
    if (name === 'ul' || name === 'ol') {
      flush();
      renderList(child, out, name === 'ol');
      continue;
    }
    if (name === 'div' || name === 'p' || name === 'form' || name === 'section') {
      if (/\bform-check\b/.test(cls)) continue;
      if (/u-inline-005/.test(cls)) {
        flush();
        const note = clean(inline(child)).replace(/\*\*(.+?)\*\*/g, '`$1`');
        if (note) out.push('> ' + note);
        continue;
      }
      if (
        /\bcard\b/.test(cls) &&
        child.children.some((c) => c.type === 'tag' && c.name === 'a' && /\bcard-header\b/.test(c.attribs?.class || ''))
      ) {
        flush();
        renderCard(child, out);
        continue;
      }
      flush();
      renderBlocks(child, out);
      continue;
    }
    buf += inline(child);
  }
  flush();
}

function renderCard(card, out) {
  const header = card.children.find(
    (c) => c.type === 'tag' && c.name === 'a' && /\bcard-header\b/.test(c.attribs?.class || '')
  );
  if (header) out.push('## ' + clean($(header).text()));
  const collapse = card.children.find(
    (c) => c.type === 'tag' && c.name === 'div' && /\bcollapse\b/.test(c.attribs?.class || '')
  );
  const body = collapse && collapse.children.find((c) => c.type === 'tag' && /\bcard-body\b/.test(c.attribs?.class || ''));
  if (body) renderBlocks(body, out);
}

const out = [];

const info = $('#info').first();
out.push('# ' + clean(info.children('.card-header').first().text()));

const datasetBar = clean($('#dataset-bar').text());
if (datasetBar) out.push('> ' + datasetBar);
out.push('> Source: <https://codex.flywire.ai/api/download?dataset=fafb>');

renderBlocks(info.children('.card-body').first().get(0), out);

const modalities = $('#contentBody div.modal');
if (modalities.length) {
  out.push('# Appendix: Annotation & Tag Definitions');
  modalities.each((i, el) => {
    const $m = $(el);
    const title = clean($m.find('.modal-title').first().text());
    if (title) out.push('## ' + title);
    const body = $m.find('.modal-body').first();
    if (body.length) renderBlocks(body.get(0), out);
  });
}

let md = out.join('\n\n');
md = md
  .replace(/Dear\s*\*\*謝桂綱\*\*\s*,\s*if you plan/, 'If you plan')
  .replace(/Dear\s*謝桂綱\s*,\s*if you plan/, 'If you plan')
  .replace(/\u0001/g, '  \n') // restore hard line breaks
  .replace(/\n{3,}/g, '\n\n')
  .replace(/[\u0001]+$/g, '')
  .replace(/\n+$/, '\n');

fs.writeFileSync(OUT, md, 'utf8');
console.log('written:', OUT, md.length, 'chars,', md.split('\n').length, 'lines');
