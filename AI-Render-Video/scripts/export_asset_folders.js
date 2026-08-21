/**
 * export_asset_folders.js
 *
 * Scans the assets/ directory and exports a complete tree list of all folder paths
 * and their names into assets/ASSET_FOLDERS_TREE.txt and prints to console.
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const rootDir = path.resolve(__dirname, '../assets');
const outputFile = path.join(rootDir, 'ASSET_FOLDERS_TREE.txt');

console.log('====================================================');
console.log(' 📋 FLOWMY - XUẤT DANH SÁCH TOÀN BỘ THƯ MỤC ASSETS');
console.log('====================================================\n');

function scanDirectoryTree(dirPath, prefix = '') {
  if (!fs.existsSync(dirPath)) return [];
  const entries = fs.readdirSync(dirPath, { withFileTypes: true });
  const folders = entries.filter(e => e.isDirectory() && !e.name.startsWith('.'));
  const results = [];

  for (let i = 0; i < folders.length; i++) {
    const folder = folders[i];
    const isLast = i === folders.length - 1;
    const currentPrefix = prefix + (isLast ? '└── ' : '├── ');
    const nextPrefix = prefix + (isLast ? '    ' : '│   ');
    const fullPath = path.join(dirPath, folder.name);
    const relPath = path.relative(rootDir, fullPath).replace(/\\/g, '/');

    results.push({
      name: folder.name,
      relPath: relPath,
      treeDisplay: `${currentPrefix}${folder.name}/`
    });

    const subResults = scanDirectoryTree(fullPath, nextPrefix);
    results.push(...subResults);
  }

  return results;
}

const allFolders = scanDirectoryTree(rootDir);

let textOutput = `FLOWMY ASSET FOLDERS STRUCTURE\nGenerated: ${new Date().toISOString()}\nTotal Folders: ${allFolders.length}\n\n`;
textOutput += `assets/\n`;
for (const f of allFolders) {
  textOutput += `${f.treeDisplay}\n`;
}

textOutput += `\n\nDANH SÁCH ĐƯỜNG DẪN TƯƠNG ĐỐI:\n`;
for (const f of allFolders) {
  textOutput += ` - assets/${f.relPath}\n`;
}

fs.writeFileSync(outputFile, textOutput, 'utf-8');

console.log(`assets/`);
for (const f of allFolders) {
  console.log(f.treeDisplay);
}

console.log(`\n✅ Đã xuất danh sách ${allFolders.length} thư mục ra tệp:`);
console.log(`   👉 assets/ASSET_FOLDERS_TREE.txt\n`);
