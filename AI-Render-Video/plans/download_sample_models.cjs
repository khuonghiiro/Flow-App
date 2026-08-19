const https = require('https');
const fs = require('fs');
const path = require('path');

const downloads = [
  {
    name: 'Sample VRM Avatar (Anime Character)',
    url: 'https://raw.githubusercontent.com/pixiv/three-vrm/master/packages/three-vrm/examples/models/VRM1_Constraint_Twist_Sample.vrm',
    dest: 'assets/characters/sample_avatar.vrm',
  },
  {
    name: 'Duck Prop (Khronos glTF Standard)',
    url: 'https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Models/master/2.0/Duck/glTF-Binary/Duck.glb',
    dest: 'assets/props/duck_prop.glb',
  },
  {
    name: 'Lantern Prop (Khronos glTF PBR)',
    url: 'https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Models/master/2.0/Lantern/glTF-Binary/Lantern.glb',
    dest: 'assets/props/lantern_prop.glb',
  },
];

function downloadFile(item) {
  return new Promise((resolve, reject) => {
    const destPath = path.resolve(__dirname, item.dest);
    const dir = path.dirname(destPath);
    if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });

    console.log(`⏳ Đang tải: ${item.name}...`);
    const file = fs.createWriteStream(destPath);

    https
      .get(item.url, (response) => {
        if (response.statusCode >= 300 && response.statusCode < 400 && response.headers.location) {
          https.get(response.headers.location, (redirectRes) => {
            redirectRes.pipe(file);
            file.on('finish', () => {
              file.close();
              const sizeMB = (fs.statSync(destPath).size / (1024 * 1024)).toFixed(2);
              console.log(`✅ Đã tải xong: ${item.dest} (${sizeMB} MB)`);
              resolve();
            });
          }).on('error', reject);
          return;
        }

        response.pipe(file);
        file.on('finish', () => {
          file.close();
          const sizeMB = (fs.statSync(destPath).size / (1024 * 1024)).toFixed(2);
          console.log(`✅ Đã tải xong: ${item.dest} (${sizeMB} MB)`);
          resolve();
        });
      })
      .on('error', (err) => {
        fs.unlink(destPath, () => {});
        reject(err);
      });
  });
}

async function main() {
  console.log('🚀 Bắt đầu tải bộ tài nguyên 3D mẫu...');
  for (const item of downloads) {
    try {
      await downloadFile(item);
    } catch (e) {
      console.error(`❌ Lỗi tải ${item.name}:`, e.message);
    }
  }
  console.log('🎉 Hoàn tất tải toàn bộ tài nguyên mẫu!');
}

main();
