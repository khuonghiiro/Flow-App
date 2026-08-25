/**
 * Main Web UI Controller for AI 360 Image Rotate
 */
document.addEventListener('DOMContentLoaded', () => {
  // Elements
  const imageInput = document.getElementById('image-upload-input');
  const dropzone = document.getElementById('upload-dropzone');
  const previewImg = document.getElementById('preview-image');
  const btnRotateSingle = document.getElementById('btn-rotate-single');
  const btnRotateTurntable = document.getElementById('btn-rotate-turntable');
  const btnRemoveBg = document.getElementById('btn-remove-bg');
  const btnAutoSpin = document.getElementById('btn-auto-spin');
  const btnExportZip = document.getElementById('btn-export-zip');
  const btnExportGif = document.getElementById('btn-export-gif');
  const btnExportSheet = document.getElementById('btn-export-sheet');
  const presetButtons = document.querySelectorAll('.btn-preset');

  // Model Manager Elements
  const selectAiModel = document.getElementById('select-ai-model');
  const btnLoadModel = document.getElementById('btn-load-model');
  const btnUnloadModel = document.getElementById('btn-unload-model');
  const modelStatusText = document.getElementById('model-status-text');
  const modelProgressPercent = document.getElementById('model-progress-percent');
  const modelProgressBar = document.getElementById('model-progress-bar');

  // Sliders & values
  const sliderAzimuth = document.getElementById('slider-azimuth');
  const valAzimuth = document.getElementById('val-azimuth');
  const sliderElevation = document.getElementById('slider-elevation');
  const valElevation = document.getElementById('val-elevation');
  const sliderFrames = document.getElementById('slider-frames');
  const valFrames = document.getElementById('val-frames');
  const chkRemoveBg = document.getElementById('chk-remove-bg');

  // Status badges
  const gpuBadge = document.getElementById('gpu-status-badge');
  const vramText = document.getElementById('vram-usage-text');
  const activeTabBtns = document.querySelectorAll('.tab-btn');

  // Application State
  let currentImageBase64 = null;
  let lastTurntableResult = null;
  let modelPollInterval = null;

  // Initialize Viewers & Gizmos
  const viewer = new TurntableViewer('turntable-stage', 'turntable-display-img', 'hud-angle-indicator');
  const gallery = new GalleryManager('gallery-container');
  window.galleryManager = gallery;

  gallery.setSelectCallback((idx) => {
    viewer.updateDisplay(idx);
  });

  const gizmo = new OrbitGizmo('orbit-gizmo-canvas', (az, el) => {
    sliderAzimuth.value = az;
    valAzimuth.textContent = `${az}°`;
    sliderElevation.value = el;
    valElevation.textContent = `${el}°`;
  });

  // Helper to set angle
  const setAngle = (az, el = 0) => {
    sliderAzimuth.value = az;
    valAzimuth.textContent = `${az}°`;
    sliderElevation.value = el;
    valElevation.textContent = `${el}°`;
    gizmo.setAngles(az, el);
  };

  // Model Loading Handlers
  const pollModelProgress = async () => {
    try {
      const data = await apiClient.getModelStatus();
      modelStatusText.textContent = data.status_text || 'Đang xử lý...';
      modelProgressPercent.textContent = `${data.loading_progress || 0}%`;
      modelProgressBar.style.width = `${data.loading_progress || 0}%`;

      if (data.is_loaded) {
        btnLoadModel.disabled = false;
        btnLoadModel.innerHTML = '✅ Model Đã Sẵn Sàng Trên GPU';
        btnLoadModel.classList.remove('btn-primary');
        btnLoadModel.classList.add('btn-secondary');
        if (modelPollInterval) clearInterval(modelPollInterval);
      } else if (!data.is_loading) {
        btnLoadModel.disabled = false;
        btnLoadModel.innerHTML = '📥 Nạp Model Vào VRAM GPU';
        if (modelPollInterval) clearInterval(modelPollInterval);
      }
    } catch (e) {
      console.warn('Poll error:', e);
    }
  };

  btnLoadModel.addEventListener('click', async () => {
    btnLoadModel.disabled = true;
    btnLoadModel.innerHTML = '⏳ Đang Tải & Nạp Model...';
    try {
      await apiClient.loadModel(selectAiModel.value);
      if (modelPollInterval) clearInterval(modelPollInterval);
      modelPollInterval = setInterval(pollModelProgress, 600);
    } catch (err) {
      alert(`Lỗi nạp model: ${err.message}`);
      btnLoadModel.disabled = false;
      btnLoadModel.innerHTML = '📥 Nạp Model Vào VRAM GPU';
    }
  });

  btnUnloadModel.addEventListener('click', async () => {
    btnUnloadModel.disabled = true;
    try {
      await apiClient.unloadModel();
      modelProgressBar.style.width = '0%';
      modelProgressPercent.textContent = '0%';
      modelStatusText.textContent = 'Đã giải phóng VRAM (Chế độ chờ)';
      btnLoadModel.disabled = false;
      btnLoadModel.innerHTML = '📥 Nạp Model Vào VRAM GPU';
      btnLoadModel.classList.add('btn-primary');
      btnLoadModel.classList.remove('btn-secondary');
    } catch (err) {
      alert(`Lỗi giải phóng VRAM: ${err.message}`);
    } finally {
      btnUnloadModel.disabled = false;
    }
  });

  // Slider event listeners
  sliderAzimuth.addEventListener('input', (e) => {
    const val = parseFloat(e.target.value);
    valAzimuth.textContent = `${val}°`;
    gizmo.setAngles(val, parseFloat(sliderElevation.value));
  });

  sliderElevation.addEventListener('input', (e) => {
    const val = parseFloat(e.target.value);
    valElevation.textContent = `${val}°`;
    gizmo.setAngles(parseFloat(sliderAzimuth.value), val);
  });

  sliderFrames.addEventListener('input', (e) => {
    valFrames.textContent = `${e.target.value} frames`;
  });

  // Preset Buttons Click (e.g. 180° Sau Lưng)
  presetButtons.forEach(btn => {
    btn.addEventListener('click', async () => {
      const az = parseFloat(btn.dataset.az);
      const el = parseFloat(btn.dataset.el || 0);
      setAngle(az, el);

      if (currentImageBase64) {
        btnRotateSingle.click();
      }
    });
  });

  // Image Upload Handlers
  const handleFile = (file) => {
    if (!file || !file.type.startsWith('image/')) return;
    const reader = new FileReader();
    reader.onload = (e) => {
      currentImageBase64 = e.target.result;
      previewImg.src = currentImageBase64;
      previewImg.style.display = 'block';
      dropzone.querySelector('.dropzone-icon').style.display = 'none';
      dropzone.querySelector('.dropzone-text').style.display = 'none';
      dropzone.querySelector('.dropzone-subtext').style.display = 'none';

      viewer.loadFrames([{ frame_index: 0, azimuth_deg: 0, image_base64: currentImageBase64 }]);
    };
    reader.readAsDataURL(file);
  };

  dropzone.addEventListener('click', () => {
    imageInput.value = '';
    imageInput.click();
  });
  imageInput.addEventListener('change', (e) => {
    if (e.target.files && e.target.files.length > 0) {
      handleFile(e.target.files[0]);
    }
  });

  dropzone.addEventListener('dragover', (e) => { e.preventDefault(); dropzone.classList.add('dragover'); });
  dropzone.addEventListener('dragleave', () => dropzone.classList.remove('dragover'));
  dropzone.addEventListener('drop', (e) => {
    e.preventDefault();
    dropzone.classList.remove('dragover');
    if (e.dataTransfer.files.length) handleFile(e.dataTransfer.files[0]);
  });

  // Load sample character preview (Humanoid 3D character with front and back details)
  const loadDemoSample = () => {
    const canvas = document.createElement('canvas');
    canvas.width = 512;
    canvas.height = 512;
    const ctx = canvas.getContext('2d');

    // Draw stylized 3D character preview
    ctx.clearRect(0, 0, 512, 512);
    
    // Body & Jacket
    ctx.fillStyle = '#1e293b';
    ctx.beginPath();
    ctx.roundRect(160, 260, 192, 210, [20, 20, 0, 0]);
    ctx.fill();

    // Jacket Zipper & Collar
    ctx.fillStyle = '#00f2fe';
    ctx.fillRect(252, 260, 8, 210);

    // Head
    const headGrad = ctx.createRadialGradient(256, 170, 20, 256, 170, 90);
    headGrad.addColorStop(0, '#fed7aa');
    headGrad.addColorStop(1, '#ea580c');
    ctx.fillStyle = headGrad;
    ctx.beginPath();
    ctx.arc(256, 170, 75, 0, Math.PI * 2);
    ctx.fill();

    // Hair
    ctx.fillStyle = '#0f172a';
    ctx.beginPath();
    ctx.arc(256, 145, 80, Math.PI, 0);
    ctx.fill();

    // Eyes
    ctx.fillStyle = '#00f2fe';
    ctx.beginPath();
    ctx.arc(225, 175, 8, 0, Math.PI * 2);
    ctx.arc(287, 175, 8, 0, Math.PI * 2);
    ctx.fill();

    const sampleB64 = canvas.toDataURL('image/png');
    currentImageBase64 = sampleB64;
    previewImg.src = sampleB64;
    previewImg.style.display = 'block';
    dropzone.querySelector('.dropzone-icon').style.display = 'none';
    dropzone.querySelector('.dropzone-text').style.display = 'none';
    dropzone.querySelector('.dropzone-subtext').style.display = 'none';

    viewer.loadFrames([{ frame_index: 0, azimuth_deg: 0, image_base64: sampleB64 }]);
  };

  document.getElementById('btn-load-sample')?.addEventListener('click', loadDemoSample);

  // Single Angle Rotate (e.g. 180° Back View)
  btnRotateSingle.addEventListener('click', async () => {
    if (!currentImageBase64) {
      alert('Vui lòng tải lên ảnh hoặc bấm "Dùng ảnh mẫu" trước!');
      return;
    }

    const az = parseFloat(sliderAzimuth.value);
    const el = parseFloat(sliderElevation.value);
    const angleName = az === 180 ? 'Sau Lưng (180°)' : `${az}°`;

    btnRotateSingle.disabled = true;
    btnRotateSingle.textContent = `⏳ Đang render AI góc ${angleName}...`;

    try {
      const res = await apiClient.rotateSingle({
        image_base64: currentImageBase64,
        azimuth_deg: az,
        elevation_deg: el,
        remove_background: chkRemoveBg.checked
      });

      if (res.frames && res.frames.length > 0) {
        viewer.loadFrames(res.frames);
        gallery.renderGallery(res.frames);
      }
    } catch (err) {
      alert(`Lỗi khi tạo góc xoay: ${err.message}`);
    } finally {
      btnRotateSingle.disabled = false;
      btnRotateSingle.textContent = '🎯 Render Góc Đã Chọn (Single Novel View)';
    }
  });

  // 360 Turntable Generation
  btnRotateTurntable.addEventListener('click', async () => {
    if (!currentImageBase64) {
      alert('Vui lòng tải lên ảnh hoặc bấm "Dùng ảnh mẫu" trước!');
      return;
    }

    btnRotateTurntable.disabled = true;
    btnRotateTurntable.textContent = '⏳ Đang render toàn bộ vòng 360° AI (RTX 3060)...';

    try {
      const numFrames = parseInt(sliderFrames.value, 10);
      const el = parseFloat(sliderElevation.value);
      const res = await apiClient.rotateTurntable({
        image_base64: currentImageBase64,
        num_frames: numFrames,
        elevation_deg: el,
        remove_background: chkRemoveBg.checked,
        generate_gif: true,
        generate_spritesheet: true
      });

      lastTurntableResult = res;
      viewer.loadFrames(res.frames);
      gallery.renderGallery(res.frames);
      viewer.startAutoSpin();
      btnAutoSpin.classList.add('active');

      btnExportZip.disabled = false;
      btnExportGif.disabled = !res.gif_base64;
      btnExportSheet.disabled = !res.spritesheet_base64;
    } catch (err) {
      alert(`Lỗi khi tạo 360 turntable: ${err.message}`);
    } finally {
      btnRotateTurntable.disabled = false;
      btnRotateTurntable.textContent = '🔄 Render Full Vòng Xoay 360° (Turntable Suite)';
    }
  });

  // Background Removal
  btnRemoveBg.addEventListener('click', async () => {
    if (!currentImageBase64) return;
    btnRemoveBg.disabled = true;
    btnRemoveBg.textContent = '⏳ Tách nền...';
    try {
      const res = await apiClient.removeBackground(currentImageBase64);
      currentImageBase64 = res.image_base64;
      previewImg.src = currentImageBase64;
      viewer.loadFrames([{ frame_index: 0, azimuth_deg: 0, image_base64: currentImageBase64 }]);
    } catch (err) {
      alert(`Lỗi tách nền: ${err.message}`);
    } finally {
      btnRemoveBg.disabled = false;
      btnRemoveBg.textContent = '✨ Tách Nền (Rembg)';
    }
  });

  // Auto Spin Toggle
  btnAutoSpin.addEventListener('click', () => {
    const isSpinning = viewer.toggleAutoSpin();
    btnAutoSpin.classList.toggle('active', isSpinning);
  });

  // Export handlers
  btnExportZip.addEventListener('click', () => {
    if (lastTurntableResult && lastTurntableResult.zip_url) {
      window.open(lastTurntableResult.zip_url, '_blank');
    }
  });

  btnExportGif.addEventListener('click', () => {
    if (lastTurntableResult && lastTurntableResult.gif_base64) {
      const link = document.createElement('a');
      link.href = lastTurntableResult.gif_base64;
      link.download = 'turntable_360.gif';
      link.click();
    }
  });

  btnExportSheet.addEventListener('click', () => {
    if (lastTurntableResult && lastTurntableResult.spritesheet_base64) {
      const link = document.createElement('a');
      link.href = lastTurntableResult.spritesheet_base64;
      link.download = 'spritesheet_360.png';
      link.click();
    }
  });

  // Tab switching
  activeTabBtns.forEach(btn => {
    btn.addEventListener('click', () => {
      activeTabBtns.forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      const target = btn.dataset.tab;

      document.getElementById('view-turntable').style.display = target === 'turntable' ? 'flex' : 'none';
      document.getElementById('view-gallery').style.display = target === 'gallery' ? 'block' : 'none';
      document.getElementById('view-api-docs').style.display = target === 'api-docs' ? 'block' : 'none';
    });
  });

  // System Hardware & Model Status Poller
  const updateSystemStatus = async () => {
    try {
      const data = await apiClient.getSystemStatus();
      if (data.gpu_name) {
        gpuBadge.innerHTML = `<span class="dot-indicator"></span> ${data.gpu_name}`;
        vramText.textContent = `VRAM: ${data.vram_used_gb}GB / ${data.vram_total_gb}GB`;
      }
    } catch (e) {
      gpuBadge.innerHTML = `<span class="dot-indicator"></span> Port 3978`;
    }
  };

  updateSystemStatus();
  pollModelProgress();
  setInterval(updateSystemStatus, 8000);
});
