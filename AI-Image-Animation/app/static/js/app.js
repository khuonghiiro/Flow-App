/**
 * Main Application Bootstrapper & Event Pipeline
 */
document.addEventListener("DOMContentLoaded", async () => {
  // Elements
  const imageCanvas = document.getElementById("imageCanvas");
  const maskCanvas = document.getElementById("maskCanvas");
  const vectorCanvas = document.getElementById("vectorCanvas");
  const previewCanvas = document.getElementById("previewCanvas");
  const dropzoneOverlay = document.getElementById("dropzoneOverlay");
  const fileInput = document.getElementById("fileInput");

  // Instances
  const maskPainter = new MaskPainter(maskCanvas);
  const vectorTools = new VectorTools(vectorCanvas);
  const canvasEngine = new CanvasEngine(imageCanvas, previewCanvas);
  const uiController = new UIController();
  const exportManager = new ExportManager();

  window.maskPainter = maskPainter;
  window.vectorTools = vectorTools;

  let currentImageElement = null;

  // Initialize UI & Presets
  uiController.init(
    (preset) => canvasEngine.setPhysics(preset),
    (physics) => canvasEngine.setPhysics(physics),
    (tool) => {
      maskPainter.isEraser = tool === "eraser";
    }
  );

  // Fetch presets from backend API
  const presets = await window.apiClient.getPresets();
  uiController.populatePresets(presets, (p) => canvasEngine.setPhysics(p));

  // Model & VRAM Manager UI Bindings
  const btnLoadModel = document.getElementById("btnLoadModel");
  const btnUnloadModel = document.getElementById("btnUnloadModel");
  const modelStatusBadge = document.getElementById("modelStatusBadge");
  const vramUsedText = document.getElementById("vramUsedText");
  const vramBarFill = document.getElementById("vramBarFill");

  async function updateVRAMDisplay() {
    const health = await window.apiClient.getHealth();
    if (health) {
      const gpuText = document.getElementById("gpuStatusText");
      if (gpuText) {
        gpuText.innerText = `${health.device_name} (${health.free_vram_gb}GB Free)`;
      }
      if (vramUsedText) {
        vramUsedText.innerText = `${health.used_vram_gb} GB / ${health.total_vram_gb || 12} GB`;
      }
      if (vramBarFill) {
        const total = health.total_vram_gb || 12;
        const pct = Math.min(100, Math.round((health.used_vram_gb / total) * 100));
        vramBarFill.style.width = `${Math.max(5, pct)}%`;
      }
    }
  }

  if (btnLoadModel) {
    btnLoadModel.onclick = async () => {
      modelStatusBadge.className = "status-badge loading";
      modelStatusBadge.innerText = "⏳ Đang Nạp VRAM...";
      try {
        const res = await window.apiClient.loadModel();
        modelStatusBadge.className = "status-badge";
        modelStatusBadge.innerText = "🟢 Đã Nạp VRAM";
        await updateVRAMDisplay();
      } catch (e) {
        modelStatusBadge.className = "status-badge offline";
        modelStatusBadge.innerText = "⚠️ Lỗi Nạp";
      }
    };
  }

  if (btnUnloadModel) {
    btnUnloadModel.onclick = async () => {
      modelStatusBadge.className = "status-badge loading";
      modelStatusBadge.innerText = "⏳ Đang Xả VRAM...";
      try {
        await window.apiClient.unloadModel();
        modelStatusBadge.className = "status-badge offline";
        modelStatusBadge.innerText = "⚪ Chưa Nạp (0 GB)";
        await updateVRAMDisplay();
      } catch (e) {
        modelStatusBadge.className = "status-badge offline";
        modelStatusBadge.innerText = "⚪ Đã Xả Bộ Nhớ";
      }
    };
  }

  // Initial VRAM check
  updateVRAMDisplay();

  // Load Image Handler
  function loadImageSource(img, fileName = "character.png") {
    currentImageElement = img;
    dropzoneOverlay.style.display = "none";

    // Update Sidebar Upload Box / Preview
    const uploadBox = document.getElementById("sidebarUploadBox");
    const previewContainer = document.getElementById("sidebarImagePreview");
    const thumbImg = document.getElementById("sidebarThumbImg");
    const imgName = document.getElementById("sidebarImgName");
    const imgDim = document.getElementById("sidebarImgDim");

    if (uploadBox) uploadBox.style.display = "none";
    if (previewContainer) previewContainer.style.display = "flex";
    if (thumbImg) thumbImg.src = img.src;
    if (imgName) imgName.innerText = fileName;
    if (imgDim) imgDim.innerText = `${img.naturalWidth || img.width} × ${img.naturalHeight || img.height} px`;

    canvasEngine.loadImage(img);
    const w = imageCanvas.width;
    const h = imageCanvas.height;

    const wrapper = document.getElementById("canvasWrapper");
    if (wrapper) {
      wrapper.style.width = `${w}px`;
      wrapper.style.height = `${h}px`;
    }

    maskPainter.setSize(w, h);
    vectorTools.setSize(w, h);

    // Add default wind vector if empty (e.g. hair breeze blowing to the right)
    if (vectorTools.vectors.length === 0) {
      vectorTools.vectors.push({
        start_x: 0.35,
        start_y: 0.3,
        end_x: 0.65,
        end_y: 0.28,
        strength: 1.2
      });
      // Add anchor pin in center for face/head
      vectorTools.pins.push({
        x: 0.5,
        y: 0.45,
        radius: 0.12,
        weight: 1.0
      });
      vectorTools.redraw();
    }
  }

  // Clear / Reset Image
  function resetImageState() {
    currentImageElement = null;
    dropzoneOverlay.style.display = "flex";
    const uploadBox = document.getElementById("sidebarUploadBox");
    const previewContainer = document.getElementById("sidebarImagePreview");
    if (uploadBox) uploadBox.style.display = "flex";
    if (previewContainer) previewContainer.style.display = "none";
    canvasEngine.pause();
    maskPainter.clear();
    vectorTools.clearVectors();
    vectorTools.clearPins();
  }

  // Drag and drop & File Picker bindings
  const sidebarUploadBox = document.getElementById("sidebarUploadBox");
  if (sidebarUploadBox) {
    sidebarUploadBox.onclick = () => fileInput.click();
    sidebarUploadBox.ondragover = (e) => { e.preventDefault(); sidebarUploadBox.classList.add("drag-over"); };
    sidebarUploadBox.ondragleave = () => sidebarUploadBox.classList.remove("drag-over");
    sidebarUploadBox.ondrop = (e) => {
      e.preventDefault();
      sidebarUploadBox.classList.remove("drag-over");
      if (e.dataTransfer.files && e.dataTransfer.files[0]) {
        handleImageFile(e.dataTransfer.files[0]);
      }
    };
  }

  document.getElementById("btnChangeImage").onclick = () => fileInput.click();
  document.getElementById("btnClearImage").onclick = () => resetImageState();

  dropzoneOverlay.onclick = (e) => {
    if (e.target.tagName !== "BUTTON") fileInput.click();
  };

  function handleImageFile(file) {
    if (!file) return;
    const reader = new FileReader();
    reader.onload = (ev) => {
      const img = new Image();
      img.onload = () => loadImageSource(img, file.name);
      img.src = ev.target.result;
    };
    reader.readAsDataURL(file);
  }

  fileInput.onchange = (e) => handleImageFile(e.target.files[0]);

  // Sample Images Buttons
  document.querySelectorAll("#btnSampleAnime").forEach(btn => {
    btn.onclick = (e) => { e.stopPropagation(); loadDemoSample("anime"); };
  });
  document.querySelectorAll("#btnSamplePortrait").forEach(btn => {
    btn.onclick = (e) => { e.stopPropagation(); loadDemoSample("portrait"); };
  });

  function loadDemoSample(type) {
    const tempCanvas = document.createElement("canvas");
    tempCanvas.width = 600;
    tempCanvas.height = 750;
    const ctx = tempCanvas.getContext("2d");

    // Dynamic artistic gradient background
    const grad = ctx.createLinearGradient(0, 0, 600, 750);
    grad.addColorStop(0, type === "anime" ? "#1e1b4b" : "#18181b");
    grad.addColorStop(1, type === "anime" ? "#312e81" : "#09090b");
    ctx.fillStyle = grad;
    ctx.fillRect(0, 0, 600, 750);

    // Draw stylized character silhouette with flowing hair & clothes
    ctx.fillStyle = type === "anime" ? "#f43f5e" : "#e2e8f0";
    ctx.beginPath();
    ctx.arc(300, 260, 90, 0, Math.PI * 2); // Head
    ctx.fill();

    // Flowing hair strands
    ctx.fillStyle = type === "anime" ? "#fb7185" : "#94a3b8";
    ctx.beginPath();
    ctx.moveTo(220, 240);
    ctx.bezierCurveTo(120, 180, 80, 360, 140, 480);
    ctx.bezierCurveTo(200, 400, 240, 320, 250, 300);
    ctx.fill();

    ctx.beginPath();
    ctx.moveTo(380, 240);
    ctx.bezierCurveTo(480, 180, 540, 360, 480, 490);
    ctx.bezierCurveTo(420, 410, 370, 320, 360, 300);
    ctx.fill();

    // Flowing Cloak / Fabric
    ctx.fillStyle = type === "anime" ? "#6366f1" : "#3b82f6";
    ctx.beginPath();
    ctx.moveTo(220, 340);
    ctx.lineTo(380, 340);
    ctx.bezierCurveTo(460, 520, 520, 680, 440, 740);
    ctx.lineTo(160, 740);
    ctx.bezierCurveTo(80, 680, 140, 520, 220, 340);
    ctx.fill();

    const img = new Image();
    img.onload = () => loadImageSource(img);
    img.src = tempCanvas.toDataURL("image/png");
  }

  // Pointer Interaction on Vector Canvas (Top Layer)
  vectorCanvas.onpointerdown = (e) => {
    const rect = vectorCanvas.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const y = e.clientY - rect.top;
    const tool = uiController.currentTool;

    if (tool === "brush" || tool === "eraser") {
      maskPainter.startStroke(x, y);
    } else if (tool === "vector") {
      vectorTools.startVector(x, y);
    } else if (tool === "pin") {
      vectorTools.addPin(x, y);
    }
  };

  window.onpointermove = (e) => {
    const rect = vectorCanvas.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const y = e.clientY - rect.top;
    const tool = uiController.currentTool;

    if (tool === "brush" || tool === "eraser") {
      maskPainter.continueStroke(x, y);
    } else if (tool === "vector") {
      vectorTools.updateVector(x, y);
    }
  };

  window.onpointerup = () => {
    maskPainter.endStroke();
    vectorTools.endVector();
  };

  // Toolbar Actions
  const btnClearVec = document.getElementById("btnClearVectors");
  if (btnClearVec) btnClearVec.onclick = () => vectorTools.clearVectors();

  const btnClearMsk = document.getElementById("btnClearMask");
  if (btnClearMsk) btnClearMsk.onclick = () => maskPainter.clear();

  const btnResetImg = document.getElementById("btnResetImage");
  if (btnResetImg) {
    btnResetImg.onclick = () => resetImageState();
  }

  // HUD Toggles
  const btnShowMask = document.getElementById("hudShowMask");
  if (btnShowMask) {
    btnShowMask.onclick = () => {
      btnShowMask.classList.toggle("active");
      maskPainter.toggleVisibility(btnShowMask.classList.contains("active"));
    };
  }

  const btnShowVectors = document.getElementById("hudShowVectors");
  if (btnShowVectors) {
    btnShowVectors.onclick = () => {
      btnShowVectors.classList.toggle("active");
      vectorTools.toggleVisibility(btnShowVectors.classList.contains("active"));
    };
  }

  // Timeline Controls
  const btnPlayPause = document.getElementById("btnPlayPause");
  const playIcon = document.getElementById("playIcon");
  const timelineSlider = document.getElementById("timelineSlider");
  const timeDisplay = document.getElementById("timeDisplay");

  btnPlayPause.onclick = () => {
    if (canvasEngine.isPlaying) {
      canvasEngine.pause();
      playIcon.innerText = "▶️";
    } else {
      canvasEngine.play();
      playIcon.innerText = "⏸️";
    }
  };

  timelineSlider.oninput = (e) => {
    const val = parseFloat(e.target.value);
    canvasEngine.pause();
    playIcon.innerText = "▶️";
    canvasEngine.setPhase(val);
    timeDisplay.innerText = `${(val * canvasEngine.duration).toFixed(1)}s / ${canvasEngine.duration.toFixed(1)}s`;
  };

  canvasEngine.onPhaseUpdate = (phase) => {
    timelineSlider.value = phase.toFixed(2);
    timeDisplay.innerText = `${(phase * canvasEngine.duration).toFixed(1)}s / ${canvasEngine.duration.toFixed(1)}s`;
  };

  // Export Payload Supplier
  exportManager.init(() => {
    if (!currentImageElement) return { image: null };

    // Export image as clean Base64
    const tempCanvas = document.createElement("canvas");
    tempCanvas.width = currentImageElement.naturalWidth || currentImageElement.width;
    tempCanvas.height = currentImageElement.naturalHeight || currentImageElement.height;
    const tempCtx = tempCanvas.getContext("2d");
    tempCtx.drawImage(currentImageElement, 0, 0);
    const base64Image = tempCanvas.toDataURL("image/png");

    return {
      image: base64Image,
      mask: maskPainter.getMaskBase64(),
      vectors: vectorTools.vectors,
      pins: vectorTools.pins,
      preset: uiController.currentPreset,
      wind_strength: canvasEngine.windStrength,
      wave_frequency: canvasEngine.waveFrequency,
      turbulence: canvasEngine.turbulence,
      flutter_scale: canvasEngine.flutterScale,
      duration_seconds: canvasEngine.duration,
      fps: 30,
      loop_mode: "seamless_phase"
    };
  });
});
