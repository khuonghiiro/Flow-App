import { AssetItem } from '../../ui/AssetBrowserPanel';

export async function fetchLiveAssetManifest(): Promise<AssetItem[]> {
  try {
    const res = await fetch(`/assets/asset_manifest.json?t=${Date.now()}`);
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const manifest = await res.json();

    const items: AssetItem[] = [];

    // 1. Dynamic Characters (any folder in manifest.characters)
    for (const [folderKey, charList] of Object.entries(manifest.characters || {})) {
      if (!Array.isArray(charList)) continue;
      charList.forEach((c: any) => {
        const pathLower = (c.relPath || '').toLowerCase();
        let gender: 'male' | 'female' | undefined = undefined;
        if (pathLower.includes('/male/') || pathLower.includes('/man/') || pathLower.includes('/nam/')) gender = 'male';
        else if (pathLower.includes('/female/') || pathLower.includes('/woman/') || pathLower.includes('/nu/')) gender = 'female';

        items.push({
          id: `char_${folderKey}_${c.id || c.name}`,
          name: c.name ? c.name.replace(/\.[^/.]+$/, '') : 'Tài nguyên',
          path: `Assets/${c.relPath}`,
          folder: `Assets/Characters/${folderKey.charAt(0).toUpperCase() + folderKey.slice(1)}`,
          type: 'character',
          format: c.format || 'GLB',
          size: `${c.sizeMB || '0.00'} MB`,
          gender,
          description: `Tài nguyên ${folderKey} (${c.format || 'GLB'})`,
          previewColor: gender === 'female' ? '#ec4899' : '#0284c7',
          previewUrl: c.previewUrl ? (c.previewUrl.startsWith('/') ? c.previewUrl : `/${c.previewUrl}`) : undefined,
          vrmUrl: `/${c.relPath}`,
          tags: ['nhân vật', folderKey, c.format ? c.format.toLowerCase() : 'glb'],
        });
      });
    }

    // 2. Dynamic Props (any category in manifest.props)
    for (const [catKey, propList] of Object.entries(manifest.props || {})) {
      if (!Array.isArray(propList)) continue;
      const folderName = catKey.charAt(0).toUpperCase() + catKey.slice(1);
      propList.forEach((p: any) => {
        items.push({
          id: `prop_${p.id || p.name}`,
          name: p.name ? p.name.replace(/\.[^/.]+$/, '') : 'Đạo cụ',
          path: `Assets/${p.relPath}`,
          folder: `Assets/Props/${folderName}`,
          type: 'prop',
          format: p.format || 'GLB',
          size: `${p.sizeMB || '0.00'} MB`,
          previewColor: '#d97706',
          previewUrl: p.previewUrl ? (p.previewUrl.startsWith('/') ? p.previewUrl : `/${p.previewUrl}`) : undefined,
          propData: { type: catKey as any, scale: 1.0 },
          tags: ['đạo cụ', catKey, p.format ? p.format.toLowerCase() : 'glb'],
        });
      });
    }

    // 3. Maps
    (manifest.maps || []).forEach((m: any) => {
      items.push({
        id: `map_${m.id || m.name}`,
        name: m.name ? m.name.replace(/\.[^/.]+$/, '') : 'Bản đồ',
        path: `Assets/${m.relPath}`,
        folder: 'Assets/Maps',
        type: 'map',
        format: m.format || 'GLB',
        size: `${m.sizeMB || '0.00'} MB`,
        previewColor: '#8b5cf6',
        previewUrl: m.previewUrl ? (m.previewUrl.startsWith('/') ? m.previewUrl : `/${m.previewUrl}`) : undefined,
        mapId: m.name,
        tags: ['bản đồ', 'map', '3d'],
      });
    });

    // 4. SkyBoxes 360
    (manifest.skyboxes || []).forEach((sb: any) => {
      const parts = (sb.relPath || '').split('/');
      const timeFolder = parts[1] || 'General';
      const folderTitle = timeFolder.replace(/_/g, ' ').toUpperCase();

      items.push({
        id: `sky_${sb.id || sb.name}`,
        name: sb.name ? sb.name.replace(/\.[^/.]+$/, '') : 'Skybox',
        path: `Assets/${sb.relPath}`,
        folder: `Assets/SkyBoxs/${folderTitle}`,
        type: 'skybox',
        format: sb.format || 'PNG',
        size: `${sb.sizeMB || '0.00'} MB`,
        previewColor: '#f59e0b',
        previewUrl: `/${sb.relPath}`,
        tags: ['skybox', '360', 'môi trường'],
      });
    });

    return items;
  } catch (err) {
    console.warn('Failed to load live asset_manifest.json, falling back to defaults:', err);
    return [];
  }
}
