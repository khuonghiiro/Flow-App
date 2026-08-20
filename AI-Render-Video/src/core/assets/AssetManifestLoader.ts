import { AssetItem } from '../../ui/AssetBrowserPanel';

export async function fetchLiveAssetManifest(): Promise<AssetItem[]> {
  try {
    const res = await fetch(`/assets/asset_manifest.json?t=${Date.now()}`);
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const manifest = await res.json();

    const items: AssetItem[] = [];

    // 1. Characters: Male (Nam)
    (manifest.characters?.male || []).forEach((c: any) => {
      items.push({
        id: `char_male_${c.id}`,
        name: c.name.replace(/\.[^/.]+$/, ''),
        path: `Assets/${c.relPath}`,
        folder: 'Assets/Characters/Male',
        type: 'character',
        format: c.format,
        size: `${c.sizeMB} MB`,
        gender: 'male',
        description: `Model nhân vật Nam (${c.format}), kèm ảnh tham chiếu 2D`,
        previewColor: '#0284c7',
        previewUrl: c.previewUrl ? `/${c.previewUrl}` : undefined,
        vrmUrl: `/${c.relPath}`,
        tags: ['nam', 'nhân vật', 'male', c.format.toLowerCase()]
      });
    });

    // 2. Characters: Female (Nữ)
    (manifest.characters?.female || []).forEach((c: any) => {
      items.push({
        id: `char_female_${c.id}`,
        name: c.name.replace(/\.[^/.]+$/, ''),
        path: `Assets/${c.relPath}`,
        folder: 'Assets/Characters/Female',
        type: 'character',
        format: c.format,
        size: `${c.sizeMB} MB`,
        gender: 'female',
        description: `Model nhân vật Nữ (${c.format}), kèm ảnh tham chiếu 2D`,
        previewColor: '#ec4899',
        previewUrl: c.previewUrl ? `/${c.previewUrl}` : undefined,
        vrmUrl: `/${c.relPath}`,
        tags: ['nữ', 'nhân vật', 'female', c.format.toLowerCase()]
      });
    });

    // 3. Characters: Base Bodies (Thân hình cơ bản / manekin)
    (manifest.characters?.base_bodies || []).forEach((c: any) => {
      const isMale = c.relPath.includes('/male/') || c.relPath.includes('/man/');
      const isFemale = c.relPath.includes('/female/') || c.relPath.includes('/woman/');
      const subFolder = isMale ? 'Male' : isFemale ? 'Female' : '';

      items.push({
        id: `body_${c.id}`,
        name: c.name.replace(/\.[^/.]+$/, ''),
        path: `Assets/${c.relPath}`,
        folder: subFolder ? `Assets/Characters/Base_Bodies/${subFolder}` : 'Assets/Characters/Base_Bodies',
        type: 'character',
        format: c.format,
        size: `${c.sizeMB} MB`,
        gender: isMale ? 'male' : isFemale ? 'female' : undefined,
        description: `Thân hình cơ bản manekin (${c.format})`,
        previewColor: '#64748b',
        previewUrl: c.previewUrl ? `/${c.previewUrl}` : undefined,
        vrmUrl: `/${c.relPath}`,
        tags: ['manekin', 'thân hình', 'base body']
      });
    });

    // 4. Characters: Costumes (Trang phục Nam / Nữ)
    (manifest.characters?.costumes || []).forEach((c: any) => {
      const isMale = c.relPath.includes('/male/') || c.relPath.includes('/man/');
      const isFemale = c.relPath.includes('/female/') || c.relPath.includes('/woman/');
      const subFolder = isMale ? 'Male' : isFemale ? 'Female' : '';

      items.push({
        id: `costume_${c.id}`,
        name: c.name.replace(/\.[^/.]+$/, ''),
        path: `Assets/${c.relPath}`,
        folder: subFolder ? `Assets/Characters/Costumes/${subFolder}` : 'Assets/Characters/Costumes',
        type: 'character',
        format: c.format,
        size: `${c.sizeMB} MB`,
        gender: isMale ? 'male' : isFemale ? 'female' : undefined,
        description: `Trang phục ${isMale ? 'Nam' : isFemale ? 'Nữ' : ''} (${c.format}), kèm ảnh tham chiếu`,
        previewColor: '#f59e0b',
        previewUrl: c.previewUrl ? `/${c.previewUrl}` : undefined,
        vrmUrl: `/${c.relPath}`,
        tags: ['trang phục', 'costume', isMale ? 'nam' : isFemale ? 'nữ' : 'unisex']
      });
    });

    // 5. Characters: Faces (Khuôn mặt Nam / Nữ)
    (manifest.characters?.faces || []).forEach((c: any) => {
      const isMale = c.relPath.includes('/male/') || c.relPath.includes('/man/');
      const isFemale = c.relPath.includes('/female/') || c.relPath.includes('/woman/');
      const subFolder = isMale ? 'Male' : isFemale ? 'Female' : '';

      items.push({
        id: `face_${c.id}`,
        name: c.name.replace(/\.[^/.]+$/, ''),
        path: `Assets/${c.relPath}`,
        folder: subFolder ? `Assets/Characters/Faces/${subFolder}` : 'Assets/Characters/Faces',
        type: 'character',
        format: c.format,
        size: `${c.sizeMB} MB`,
        gender: isMale ? 'male' : isFemale ? 'female' : undefined,
        description: `Khuôn mặt ${isMale ? 'Nam' : isFemale ? 'Nữ' : ''} (${c.format})`,
        previewColor: '#a855f7',
        previewUrl: c.previewUrl ? `/${c.previewUrl}` : undefined,
        vrmUrl: `/${c.relPath}`,
        tags: ['khuôn mặt', 'face', isMale ? 'nam' : isFemale ? 'nữ' : 'unisex']
      });
    });

    // 6. Characters: Hairstyles (Mái tóc)
    (manifest.characters?.hairstyles || []).forEach((c: any) => {
      items.push({
        id: `hair_${c.id}`,
        name: c.name.replace(/\.[^/.]+$/, ''),
        path: `Assets/${c.relPath}`,
        folder: 'Assets/Characters/Hairstyles',
        type: 'character',
        format: c.format,
        size: `${c.sizeMB} MB`,
        description: `Kiểu tóc (${c.format})`,
        previewColor: '#10b981',
        previewUrl: c.previewUrl ? `/${c.previewUrl}` : undefined,
        vrmUrl: `/${c.relPath}`,
        tags: ['tóc', 'hairstyle']
      });
    });

    // 7. Characters: Beards (Râu)
    (manifest.characters?.beards || []).forEach((c: any) => {
      items.push({
        id: `beard_${c.id}`,
        name: c.name.replace(/\.[^/.]+$/, ''),
        path: `Assets/${c.relPath}`,
        folder: 'Assets/Characters/Beards',
        type: 'character',
        format: c.format,
        size: `${c.sizeMB} MB`,
        description: `Kiểu râu (${c.format})`,
        previewColor: '#78716c',
        previewUrl: c.previewUrl ? `/${c.previewUrl}` : undefined,
        vrmUrl: `/${c.relPath}`,
        tags: ['râu', 'beard']
      });
    });

    // 8. Characters: Accessories (Phụ kiện)
    (manifest.characters?.accessories || []).forEach((c: any) => {
      items.push({
        id: `acc_${c.id}`,
        name: c.name.replace(/\.[^/.]+$/, ''),
        path: `Assets/${c.relPath}`,
        folder: 'Assets/Characters/Accessories',
        type: 'character',
        format: c.format,
        size: `${c.sizeMB} MB`,
        description: `Phụ kiện nhân vật (${c.format})`,
        previewColor: '#ec4899',
        previewUrl: c.previewUrl ? `/${c.previewUrl}` : undefined,
        vrmUrl: `/${c.relPath}`,
        tags: ['phụ kiện', 'accessories']
      });
    });

    // 4. Props
    const propCategories = ['weapons', 'tools', 'consumables', 'furniture', 'buildings', 'nature', 'vehicles', 'legacy'];
    propCategories.forEach((catKey) => {
      const folderName = catKey.charAt(0).toUpperCase() + catKey.slice(1);
      (manifest.props?.[catKey] || []).forEach((p: any) => {
        items.push({
          id: `prop_${p.id}`,
          name: p.name.replace(/\.[^/.]+$/, ''),
          path: `Assets/${p.relPath}`,
          folder: `Assets/Props/${folderName}`,
          type: 'prop',
          format: p.format,
          size: `${p.sizeMB} MB`,
          previewColor: '#d97706',
          previewUrl: p.previewUrl ? `/${p.previewUrl}` : undefined,
          propData: { type: catKey as any, scale: 1.0 },
          tags: ['đạo cụ', catKey, p.format.toLowerCase()]
        });
      });
    });

    // 5. Maps
    (manifest.maps || []).forEach((m: any) => {
      items.push({
        id: `map_${m.id}`,
        name: m.name.replace(/\.[^/.]+$/, ''),
        path: `Assets/${m.relPath}`,
        folder: 'Assets/Maps',
        type: 'map',
        format: m.format,
        size: `${m.sizeMB} MB`,
        previewColor: '#8b5cf6',
        previewUrl: m.previewUrl ? `/${m.previewUrl}` : undefined,
        mapId: m.name,
        tags: ['bản đồ', 'map', '3d']
      });
    });

    // 6. SkyBoxes 360
    (manifest.skyboxes || []).forEach((sb: any) => {
      // Determine folder category from path
      const parts = sb.relPath.split('/');
      const timeFolder = parts[1] || 'General';
      const folderTitle = timeFolder.replace(/_/g, ' ').toUpperCase();

      items.push({
        id: `sky_${sb.id}`,
        name: sb.name.replace(/\.[^/.]+$/, ''),
        path: `Assets/${sb.relPath}`,
        folder: `Assets/SkyBoxs/${folderTitle}`,
        type: 'skybox',
        format: sb.format,
        size: `${sb.sizeMB} MB`,
        previewColor: '#f59e0b',
        previewUrl: `/${sb.relPath}`,
        tags: ['skybox', '360', 'môi trường']
      });
    });

    return items;
  } catch (err) {
    console.warn('Failed to load live asset_manifest.json, falling back to defaults:', err);
    return [];
  }
}
