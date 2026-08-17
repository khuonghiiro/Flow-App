// State Management
let currentEngine = "faster-whisper";
let selectedAudioFile = null;
let currentSegments = [];
let currentAudioMeta = {};
let currentViewMode = "bilingual"; // "bilingual" | "translation" | "source"
let hardwareInfo = null;
let modelStatusMap = {};

// Language Flag Mapping for Visual Display
const LANG_FLAG_MAP = {
  "vi": { flag: "🇻🇳", name: "Tiếng Việt", tag: "VN" },
  "en": { flag: "🇺🇸", name: "Tiếng Anh", tag: "EN" },
  "zh": { flag: "🇨🇳", name: "Tiếng Trung", tag: "CN" },
  "ja": { flag: "🇯🇵", name: "Tiếng Nhật", tag: "JA" },
  "ko": { flag: "🇰🇷", name: "Tiếng Hàn", tag: "KO" },
  "fr": { flag: "🇫🇷", name: "Tiếng Pháp", tag: "FR" },
  "de": { flag: "🇩🇪", name: "Tiếng Đức", tag: "DE" },
  "es": { flag: "🇪🇸", name: "Tiếng Tây Ban Nha", tag: "ES" },
  "ru": { flag: "🇷🇺", name: "Tiếng Nga", tag: "RU" },
  "th": { flag: "🇹🇭", name: "Tiếng Thái", tag: "TH" },
  "id": { flag: "🇮🇩", name: "Tiếng Indonesia", tag: "ID" },
  "pt": { flag: "🇵🇹", name: "Tiếng Bồ Đào Nha", tag: "PT" },
  "it": { flag: "🇮🇹", name: "Tiếng Ý", tag: "IT" },
  "ar": { flag: "🇸🇦", name: "Tiếng Ả Rập", tag: "AR" },
  "hi": { flag: "🇮🇳", name: "Tiếng Hindi", tag: "HI" },
  "tr": { flag: "🇹🇷", name: "Tiếng Thổ Nhĩ Kỳ", tag: "TR" },
  "nl": { flag: "🇳🇱", name: "Tiếng Hà Lan", tag: "NL" }
};

document.addEventListener("DOMContentLoaded", () => {
  initTheme();
  initHardwareInfo();
  initModelsList();
  initVramActiveTracker();
  initEngineSelector();
  initDropzone();
  initStudioActions();
  initApiCopy();
});

// ==========================================================================
// 1. Theme Management (Dark & Light Mode)
// ==========================================================================
function initTheme() {
  const savedTheme = localStorage.getItem("flowmy_studio_theme") || "dark";
  applyTheme(savedTheme);

  const themeBtn = document.getElementById("themeToggleBtn");
  if (themeBtn) {
    themeBtn.addEventListener("click", () => {
      const currentTheme = document.documentElement.getAttribute("data-theme") || "dark";
      const newTheme = currentTheme === "dark" ? "light" : "dark";
      applyTheme(newTheme);
      localStorage.setItem("flowmy_studio_theme", newTheme);
      showToast(`Đã chuyển sang ${newTheme === "dark" ? "🌙 Giao diện Tối" : "☀️ Giao diện Sáng"}`, "info");
    });
  }
}

function applyTheme(theme) {
  document.documentElement.setAttribute("data-theme", theme);
  const iconEl = document.getElementById("themeIcon");
  const labelEl = document.getElementById("themeLabel");
  if (iconEl && labelEl) {
    if (theme === "dark") {
      iconEl.textContent = "🌙";
      labelEl.textContent = "Giao diện Tối";
    } else {
      iconEl.textContent = "☀️";
      labelEl.textContent = "Giao diện Sáng";
    }
  }
}

// ==========================================================================
// 2. Hardware Advisor & VRAM Active Tracker
// ==========================================================================
async function initHardwareInfo() {
  try {
    const res = await fetch("/api/hardware");
    if (!res.ok) return;
    hardwareInfo = await res.json();

    document.getElementById("gpuName").textContent = hardwareInfo.gpu_name;
    document.getElementById("vramSize").textContent = hardwareInfo.vram_gb > 0 ? `${hardwareInfo.vram_gb} GB` : "CPU";
    document.getElementById("cpuThreads").textContent = `${hardwareInfo.cpu_threads} Threads`;
    document.getElementById("ramSize").textContent = `${hardwareInfo.ram_gb} GB`;

    document.getElementById("advisorDesc").textContent = hardwareInfo.advice_message;
    document.getElementById("concurrencyBadge").textContent = `⚡ Khuyến nghị: ${hardwareInfo.recommended_concurrency} luồng song song`;

    if (hardwareInfo.recommended_engine) {
      selectEngine(hardwareInfo.recommended_engine);
    }
  } catch (err) {
    console.error("Hardware fetch error:", err);
  }
}

async function initVramActiveTracker() {
  try {
    const res = await fetch("/api/models/active");
    if (!res.ok) return;
    const data = await res.json();
    updateVramBadge(data);
  } catch (e) {
    console.warn("VRAM active tracker error:", e);
  }
}

function updateVramBadge(data) {
  const badge = document.getElementById("vramActiveBadge");
  const textEl = document.getElementById("vramActiveText");
  if (!badge || !textEl) return;

  if (data && data.has_model && data.active_model) {
    const m = data.active_model;
    const dev = (m.device === "cuda" || m.device === "auto") ? "GPU" : "CPU";
    textEl.textContent = `⚡ ${m.engine} (${m.model_size}) [${dev}]`;
    badge.className = "vram-active-badge";
    badge.title = `Model đang nạp sẵn trong VRAM: ${m.engine} - ${m.model_size} (${m.compute_type})`;
  } else {
    textEl.textContent = "⚡ Chưa nạp model vào VRAM";
    badge.className = "vram-active-badge empty";
    badge.title = "Chưa có model nào nạp trong bộ nhớ RAM/VRAM";
  }
}

// ==========================================================================
// 3. Engine Selector Cards
// ==========================================================================
function initEngineSelector() {
  const cards = document.querySelectorAll(".engine-card");
  cards.forEach(card => {
    card.addEventListener("click", () => {
      const eng = card.getAttribute("data-engine");
      selectEngine(eng);
    });
  });
}

function selectEngine(engineName) {
  currentEngine = engineName;
  document.querySelectorAll(".engine-card").forEach(c => {
    c.classList.toggle("active", c.getAttribute("data-engine") === engineName);
  });

  const activeBadge = document.getElementById("activeEngineBadge");
  if (activeBadge) {
    const names = {
      "faster-whisper": "Engine: Faster-Whisper",
      "whisperx": "Engine: WhisperX",
      "sensevoice": "Engine: SenseVoice",
      "seamless-m4t": "Engine: SeamlessM4T"
    };
    activeBadge.textContent = names[engineName] || `Engine: ${engineName}`;
  }

  const modelSizeSelect = document.getElementById("modelSizeSelect");
  if (engineName === "sensevoice") {
    modelSizeSelect.innerHTML = `<option value="base" selected>SenseVoice-Small (Base - Siêu nhẹ CPU)</option>`;
  } else if (engineName === "seamless-m4t") {
    modelSizeSelect.innerHTML = `
      <option value="medium" selected>SeamlessM4T Medium (1.2GB VRAM)</option>
      <option value="large-v3">SeamlessM4T v2 Large (4.5GB VRAM)</option>
    `;
  } else {
    modelSizeSelect.innerHTML = `
      <option value="tiny">Tiny (39M params - Nhanh nhất)</option>
      <option value="base">Base (74M params - Nhẹ)</option>
      <option value="small" selected>Small (244M params - Cân bằng nhất)</option>
      <option value="medium">Medium (769M params - Chuẩn cao)</option>
      <option value="large-v3">Large-v3 (1.5B params - Tốt nhất)</option>
      <option value="large-v3-turbo">Large-v3 Turbo (Tối ưu nhất)</option>
    `;
  }

  updateModelOptionsWithStatus();
  updateModelExistBadge();
  updateApiSnippet();
}

function onModelSizeChanged() {
  updateModelExistBadge();
  updateApiSnippet();
}

// ==========================================================================
// 4. Model Status & Preload Controls
// ==========================================================================
async function initModelsList() {
  try {
    const res = await fetch("/api/models");
    if (!res.ok) return;
    const data = await res.json();

    modelStatusMap = data.status_map || {};

    document.getElementById("modelsDirText").textContent = data.models_dir;
    document.getElementById("totalModelSize").textContent = `${data.total_size_mb} MB`;

    if (data.active_model) {
      updateVramBadge({ has_model: true, active_model: data.active_model });
    }

    updateEngineCardBadges();
    updateModelOptionsWithStatus();
    updateModelExistBadge();

    // Render installed models list
    const listEl = document.getElementById("installedModelsList");
    if (!data.installed_models || data.installed_models.length === 0) {
      listEl.innerHTML = `<div class="empty-model-state">📭 Chưa có model nào được lưu trong thư mục. Nhấn nút <strong>⚡ Nạp Model</strong> hoặc chuyển đổi audio để nạp tự động.</div>`;
      return;
    }

    listEl.innerHTML = "";
    data.installed_models.forEach(m => {
      const item = document.createElement("div");
      item.className = "model-item";

      const engineName = m.engine || "AI Engine";
      const engineClass = engineName.toLowerCase().replace(/[^a-z0-9]/g, "-");
      const modelDisplayName = m.model_name || m.name || "AI Model";
      const modelPathEscaped = encodeURIComponent(m.path || "");

      item.innerHTML = `
        <div class="model-item-left">
          <span class="model-engine-badge badge-${engineClass}">${engineName}</span>
          <div class="model-item-info">
            <div class="model-item-name" title="${m.path || ''}">${modelDisplayName}</div>
            <div class="model-item-meta">
              <span class="meta-pill">💾 <strong>${m.size_mb || 0} MB</strong></span>
              <span class="meta-pill">🕒 ${m.modified || "N/A"}</span>
            </div>
          </div>
        </div>
        <button class="btn-delete" onclick="deleteModel('${modelPathEscaped}')" title="Xóa model khỏi ổ cứng để giải phóng dung lượng">
          <span>🗑</span>
          <span>Xóa</span>
        </button>
      `;
      listEl.appendChild(item);
    });
  } catch (err) {
    console.error("Models fetch error:", err);
  }
}

window.deleteModel = async function(encodedPath) {
  const modelPath = decodeURIComponent(encodedPath);
  if (!confirm(`Bạn có chắc chắn muốn xóa model này khỏi ổ cứng để giải phóng dung lượng?\n\nĐường dẫn:\n${modelPath}`)) {
    return;
  }

  try {
    const res = await fetch("/api/models/delete", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ model_path: modelPath, path: modelPath })
    });

    const data = await res.json();
    if (res.ok && data.status === "success") {
      showToast("🗑️ Đã xóa model khỏi ổ cứng thành công!", "success");
      await initModelsList();
      await initVramActiveTracker();
    } else {
      showToast(`⚠️ Không thể xóa model: ${data.detail || "Thư mục đang được sử dụng"}`, "error");
    }
  } catch (err) {
    showToast(`❌ Lỗi kết nối khi xóa: ${err.message}`, "error");
  }
};

function updateEngineCardBadges() {
  const engines = ["faster-whisper", "whisperx", "sensevoice", "seamless-m4t"];
  engines.forEach(eng => {
    const badge = document.getElementById(`cardStatus-${eng}`);
    if (!badge) return;
    const isReady = !!modelStatusMap[eng];
    if (isReady) {
      badge.textContent = "🟢 Đã có model";
      badge.className = "card-status-badge ready";
    } else {
      badge.textContent = "⚪ Chưa tải";
      badge.className = "card-status-badge not-ready";
    }
  });
}

function updateModelOptionsWithStatus() {
  const select = document.getElementById("modelSizeSelect");
  if (!select) return;

  const engineMap = modelStatusMap[currentEngine] || {};

  Array.from(select.options).forEach(opt => {
    const isDownloaded = !!engineMap[opt.value];
    const cleanText = opt.text.replace(/^[🟢⚪]\s*/, "");
    if (isDownloaded) {
      opt.text = `🟢 ${cleanText}`;
    } else {
      opt.text = `⚪ ${cleanText}`;
    }
  });
}

function updateModelExistBadge() {
  const badge = document.getElementById("modelExistBadge");
  const select = document.getElementById("modelSizeSelect");
  if (!badge || !select) return;

  const currentSize = select.value;
  const engineMap = modelStatusMap[currentEngine] || {};
  const isDownloaded = !!engineMap[currentSize];

  if (isDownloaded) {
    badge.textContent = "🟢 Đã có sẵn trên máy";
    badge.className = "model-exist-badge ready";
  } else {
    badge.textContent = "⚪ Chưa tải về máy";
    badge.className = "model-exist-badge not-ready";
  }
}

function updateModalSteps(stage, pct) {
  const chipInit = document.getElementById("stepChip-init");
  const chipDownload = document.getElementById("stepChip-download");
  const chipLoad = document.getElementById("stepChip-load");
  const chipReady = document.getElementById("stepChip-ready");

  [chipInit, chipDownload, chipLoad, chipReady].forEach(c => {
    if (c) c.className = "step-chip";
  });

  if (stage === "init") {
    if (chipInit) chipInit.classList.add("active");
  } else if (stage === "downloading") {
    if (chipInit) chipInit.classList.add("completed");
    if (chipDownload) chipDownload.classList.add("active");
  } else if (stage === "loading") {
    if (chipInit) chipInit.classList.add("completed");
    if (chipDownload) chipDownload.classList.add("completed");
    if (chipLoad) chipLoad.classList.add("active");
  } else if (stage === "ready" || pct >= 100) {
    if (chipInit) chipInit.classList.add("completed");
    if (chipDownload) chipDownload.classList.add("completed");
    if (chipLoad) chipLoad.classList.add("completed");
    if (chipReady) chipReady.classList.add("completed");
  }
}

// 4.1 Preload / Download Model Action with Real-time % SSE Stream
function preloadCurrentModel() {
  const modelSize = document.getElementById("modelSizeSelect").value;
  const device = document.getElementById("deviceSelect").value;
  const computeType = document.getElementById("computeTypeSelect").value;

  const overlay = document.getElementById("loadingModalOverlay");
  const modalTitle = document.getElementById("loadingModalTitle");
  const modalPercent = document.getElementById("modalPercent");
  const modalProgressFill = document.getElementById("modalProgressFill");
  const modalDesc = document.getElementById("loadingModalDesc");
  const modalDetail = document.getElementById("modalDetail");

  modalTitle.textContent = `Đang Nạp Model AI: ${currentEngine} (${modelSize})`;
  modalPercent.textContent = "0%";
  modalProgressFill.style.width = "0%";
  modalDesc.textContent = "Đang kết nối tới máy chủ và dọn dẹp VRAM...";
  modalDetail.textContent = "Khởi tạo kết nối...";
  updateModalSteps("init", 0);
  overlay.classList.add("active");

  let currentDisplayPct = 0;
  let targetPct = 5;

  const tickerInterval = setInterval(() => {
    if (currentDisplayPct < targetPct) {
      currentDisplayPct += 1;
      modalPercent.textContent = `${currentDisplayPct}%`;
      modalProgressFill.style.width = `${currentDisplayPct}%`;
    }
  }, 25);

  const url = `/api/models/load-stream?engine=${encodeURIComponent(currentEngine)}&model_size=${encodeURIComponent(modelSize)}&device=${encodeURIComponent(device)}&compute_type=${encodeURIComponent(computeType)}`;
  const evtSource = new EventSource(url);

  evtSource.onmessage = (event) => {
    try {
      const data = JSON.parse(event.data);
      const rawPct = Math.min(Math.max(data.percent || 0, 0), 100);
      targetPct = rawPct;

      if (data.message) modalDesc.textContent = data.message;
      if (data.detail) modalDetail.textContent = data.detail;
      if (data.stage) updateModalSteps(data.stage, rawPct);

      if (data.status === "completed" || rawPct >= 100) {
        evtSource.close();
        targetPct = 100;
        currentDisplayPct = 100;
        clearInterval(tickerInterval);
        modalPercent.textContent = "100%";
        modalProgressFill.style.width = "100%";
        updateModalSteps("ready", 100);
        setTimeout(() => {
          overlay.classList.remove("active");
          showToast(`✅ Nạp thành công model ${modelSize} (${currentEngine})! Sẵn sàng sử dụng.`, "success");
          initModelsList();
          initVramActiveTracker();
        }, 900);
      } else if (data.status === "error") {
        evtSource.close();
        clearInterval(tickerInterval);
        modalPercent.textContent = "❌";
        modalDesc.textContent = data.message || "Lỗi nạp model.";
        setTimeout(() => {
          overlay.classList.remove("active");
          showToast(`❌ ${data.message}`, "error");
        }, 3000);
      }
    } catch (e) {
      console.error("SSE parse error:", e);
    }
  };

  evtSource.onerror = (err) => {
    console.error("SSE connection error:", err);
    evtSource.close();
    clearInterval(tickerInterval);
    setTimeout(async () => {
      await initModelsList();
      await initVramActiveTracker();
      overlay.classList.remove("active");
    }, 1500);
  };
}

async function deleteModel(path) {
  if (!confirm(`Bạn có chắc chắn muốn xóa model tại:\n${path}?`)) return;
  try {
    const res = await fetch("/api/models/delete", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ model_path: path })
    });
    if (res.ok) {
      showToast("✅ Đã xóa model khỏi ổ cứng thành công!", "success");
      initModelsList();
    }
  } catch (err) {
    showToast("❌ Lỗi xóa model: " + err.message, "error");
  }
}

// ==========================================================================
// 5. Translation Toggle Switch Logic & Standalone Translation
// ==========================================================================
function onTranslateToggleChange() {
  const toggle = document.getElementById("translateToggle");
  const body = document.getElementById("translationBody");
  const desc = document.getElementById("translationSubDesc");

  if (toggle.checked) {
    body.style.display = "block";
    desc.textContent = "Đang bật dịch: Tự động trích xuất timeline gốc ➜ Dịch song ngữ sang ngôn ngữ đích.";
  } else {
    body.style.display = "none";
    desc.textContent = "Đang tắt dịch: Xuất phụ đề theo đúng ngôn ngữ gốc nói trong audio.";
  }

  updateApiSnippet();
}

// Standalone Dịch Ngay Action (Translate current timeline without re-running audio)
async function translateCurrentTimeline() {
  if (!currentSegments || currentSegments.length === 0) {
    showToast("⚠️ Chưa có danh sách timeline phụ đề để dịch!", "error");
    return;
  }

  const targetLang = document.getElementById("targetLangSelect").value;
  const btnTranslate = document.getElementById("btnTranslateNow");
  const srcLang = currentAudioMeta.language || "auto";

  btnTranslate.disabled = true;
  btnTranslate.innerHTML = `⏳ Đang dịch sang ${targetLang.toUpperCase()}...`;

  // Set visual translating state in timeline
  document.querySelectorAll(".lang-text-tgt").forEach(el => {
    el.innerHTML = `<span class="translating-pulse">⏳ Đang dịch...</span>`;
  });

  try {
    const res = await fetch("/api/translate-segments", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        segments: currentSegments,
        src_lang: srcLang,
        target_lang: targetLang
      })
    });

    if (!res.ok) {
      const err = await res.json();
      throw new Error(err.detail || "Lỗi dịch phụ đề.");
    }

    const data = await res.json();
    currentSegments = data.segments || [];
    currentAudioMeta.translated = true;
    currentAudioMeta.target_lang = targetLang;
    currentAudioMeta.target_lang_name = data.target_lang_name;

    renderTimeline(currentSegments, currentAudioMeta);
    showToast(`🌐 Đã dịch xong toàn bộ timeline sang ${data.target_lang_name}!`, "success");
  } catch (err) {
    showToast(`❌ Lỗi dịch thuật: ${err.message}`, "error");
    renderTimeline(currentSegments, currentAudioMeta);
  } finally {
    btnTranslate.disabled = false;
    btnTranslate.innerHTML = `<span>🌐</span> Dịch Ngay (Từ Timeline Hiện Tại)`;
  }
}

// ==========================================================================
// 6. Audio Dropzone & Sync Player
// ==========================================================================
function initDropzone() {
  const dropzone = document.getElementById("dropzone");
  const fileInput = document.getElementById("audioFileInput");
  const audioPlayer = document.getElementById("audioPlayer");
  const playerBox = document.getElementById("audioPlayerBox");
  const playerFileName = document.getElementById("playerFileName");

  dropzone.addEventListener("click", () => fileInput.click());

  dropzone.addEventListener("dragover", (e) => {
    e.preventDefault();
    dropzone.classList.add("dragover");
  });

  dropzone.addEventListener("dragleave", () => {
    dropzone.classList.remove("dragover");
  });

  dropzone.addEventListener("drop", (e) => {
    e.preventDefault();
    dropzone.classList.remove("dragover");
    if (e.dataTransfer.files.length > 0) {
      loadAudioFile(e.dataTransfer.files[0]);
    }
  });

  fileInput.addEventListener("change", () => {
    if (fileInput.files.length > 0) {
      loadAudioFile(fileInput.files[0]);
    }
  });

  function loadAudioFile(file) {
    selectedAudioFile = file;
    document.getElementById("dropzoneText").textContent = file.name;
    document.getElementById("dropzoneSub").textContent = `${(file.size / (1024 * 1024)).toFixed(2)} MB • Đã sẵn sàng xử lý`;

    if (playerFileName) playerFileName.textContent = file.name;

    const url = URL.createObjectURL(file);
    audioPlayer.src = url;
    playerBox.classList.add("active");
  }

  // Highlight timeline row when audio plays
  audioPlayer.addEventListener("timeupdate", () => {
    const curTime = audioPlayer.currentTime;
    document.querySelectorAll(".timeline-row").forEach(row => {
      const start = parseFloat(row.getAttribute("data-start"));
      const end = parseFloat(row.getAttribute("data-end"));
      if (curTime >= start && curTime <= end) {
        row.classList.add("active");
      } else {
        row.classList.remove("active");
      }
    });
  });
}

// ==========================================================================
// 7. Process: Two-Phase Subtitle Extraction & Bilingual Translation
// ==========================================================================
function initStudioActions() {
  const btnProcess = document.getElementById("btnProcess");
  const timelineContainer = document.getElementById("timelineContainer");

  btnProcess.addEventListener("click", async () => {
    if (!selectedAudioFile) {
      showToast("⚠️ Vui lòng chọn hoặc kéo thả file audio vào Studio trước!", "error");
      return;
    }

    const isTranslate = document.getElementById("translateToggle").checked;
    const targetLang = document.getElementById("targetLangSelect").value;
    const modelSize = document.getElementById("modelSizeSelect").value;
    const device = document.getElementById("deviceSelect").value;
    const computeType = document.getElementById("computeTypeSelect").value;

    btnProcess.disabled = true;
    btnProcess.innerHTML = `⏳ 1/2: Đang nhận diện &amp; trích xuất mốc Timeline (${currentEngine})...`;
    timelineContainer.innerHTML = `<div class="empty-timeline">🎙️ Giai đoạn 1: AI đang phân tích âm thanh, phát hiện ngôn ngữ và bóc tách mốc thời gian...</div>`;

    try {
      // Phase 1: Transcribe original speech
      const formData = new FormData();
      formData.append("file", selectedAudioFile);
      formData.append("engine", currentEngine);
      formData.append("model_size", modelSize);
      formData.append("enable_translate", false); // Extract source timeline first
      formData.append("target_lang", targetLang);
      formData.append("device", device);
      formData.append("compute_type", computeType);

      const res = await fetch("/api/transcribe", {
        method: "POST",
        body: formData
      });

      if (!res.ok) {
        const err = await res.json();
        throw new Error(err.detail || "Lỗi xử lý audio.");
      }

      const data = await res.json();
      currentSegments = data.segments || [];
      currentAudioMeta = {
        language: data.language,
        language_name: data.language_name,
        language_probability: data.language_probability,
        duration: data.duration,
        translated: false
      };

      // Render Stage 1: Source Timeline immediately!
      renderTimeline(currentSegments, currentAudioMeta);
      initModelsList();
      initVramActiveTracker();

      // Phase 2: If translation enabled and target language differs from detected language
      const detectedLang = (data.language || "en").toLowerCase().substring(0, 2);
      if (isTranslate && targetLang && targetLang.toLowerCase() !== detectedLang) {
        btnProcess.innerHTML = `🌐 2/2: Đang dịch thuật song ngữ sang ${targetLang.toUpperCase()}...`;

        // Mark all translated lines as loading
        document.querySelectorAll(".lang-text-tgt").forEach(el => {
          el.innerHTML = `<span class="translating-pulse">⏳ Đang dịch song ngữ...</span>`;
        });

        const transRes = await fetch("/api/translate-segments", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            segments: currentSegments,
            src_lang: detectedLang,
            target_lang: targetLang
          })
        });

        if (transRes.ok) {
          const transData = await transRes.json();
          currentSegments = transData.segments || currentSegments;
          currentAudioMeta.translated = true;
          currentAudioMeta.target_lang = targetLang;
          currentAudioMeta.target_lang_name = transData.target_lang_name;
          renderTimeline(currentSegments, currentAudioMeta);
          showToast(`🎉 Hoàn tất! Tách lời ${data.language_name} và dịch sang ${transData.target_lang_name} thành công!`, "success");
        } else {
          showToast(`⚠️ Tách lời thành công nhưng dịch gặp lỗi. Bạn có thể bấm 'Dịch Ngay' để thử lại.`, "warning");
        }
      } else {
        showToast(`🎉 Tách lời thoại thành công! Phát hiện: ${data.language_name || data.language}`, "success");
      }
    } catch (err) {
      showToast(`❌ Lỗi: ${err.message}`, "error");
      timelineContainer.innerHTML = `<div class="empty-timeline" style="color: #f87171;">Lỗi xử lý: ${err.message}</div>`;
    } finally {
      btnProcess.disabled = false;
      btnProcess.innerHTML = `🚀 Bắt Đầu Chuyển Đổi &amp; Tách Phụ Đề`;
    }
  });
}

// ==========================================================================
// 8. Timeline Rendering with Bilingual Presentation
// ==========================================================================
function setTimelineViewMode(mode) {
  currentViewMode = mode;
  document.querySelectorAll(".view-mode-btn").forEach(b => b.classList.remove("active"));
  const activeBtn = document.getElementById(`mode${mode.charAt(0).toUpperCase() + mode.slice(1)}`);
  if (activeBtn) activeBtn.classList.add("active");
  renderTimeline(currentSegments, currentAudioMeta);
}

function renderTimeline(segments, responseData) {
  const container = document.getElementById("timelineContainer");
  const stats = document.getElementById("timelineStats");
  const viewModesContainer = document.getElementById("timelineViewModes");

  const langCode = (responseData.language || "auto").toLowerCase().substring(0, 2);
  const langInfo = LANG_FLAG_MAP[langCode] || { flag: "🌐", name: responseData.language_name || langCode.toUpperCase(), tag: langCode.toUpperCase() };
  const probPercent = responseData.language_probability ? `(${(responseData.language_probability * 100).toFixed(1)}% tin cậy)` : "";

  // Update Detected Language Banner & Pill
  const pill = document.getElementById("detectedLangPill");
  const flagEl = document.getElementById("detectedLangFlag");
  const textEl = document.getElementById("detectedLangText");
  if (pill && flagEl && textEl) {
    pill.style.display = "inline-flex";
    flagEl.textContent = langInfo.flag;
    textEl.textContent = `${langInfo.name} ${probPercent}`;
  }

  const banner = document.getElementById("detectedLangBanner");
  const bannerFlag = document.getElementById("bannerFlag");
  const bannerTitle = document.getElementById("bannerLangTitle");
  const bannerDesc = document.getElementById("bannerLangDesc");
  const bannerTag = document.getElementById("bannerTranslateTag");

  if (banner && bannerTitle) {
    banner.style.display = "flex";
    bannerFlag.textContent = langInfo.flag;
    bannerTitle.textContent = `Ngôn ngữ audio phát hiện: ${langInfo.name} (${langCode.toUpperCase()})`;
    bannerDesc.textContent = `Độ tin cậy: ${responseData.language_probability ? (responseData.language_probability * 100).toFixed(1) + "%" : "Chuẩn"} • Thời lượng: ${responseData.duration || 0}s`;

    if (responseData.translated && responseData.target_lang) {
      const tgtInfo = LANG_FLAG_MAP[responseData.target_lang] || { flag: "🌐", name: responseData.target_lang, tag: responseData.target_lang.toUpperCase() };
      bannerTag.textContent = `Đã dịch sang ${tgtInfo.flag} ${tgtInfo.name}`;
      bannerTag.style.color = "var(--accent-primary)";
      bannerTag.style.background = "rgba(59, 130, 246, 0.15)";
    } else {
      bannerTag.textContent = "Bản gốc audio (Chưa dịch)";
      bannerTag.style.color = "#10b981";
      bannerTag.style.background = "rgba(16, 185, 129, 0.15)";
    }
  }

  if (!segments || segments.length === 0) {
    container.innerHTML = `<div class="empty-timeline">Không phát hiện câu nói nào trong file audio.</div>`;
    stats.textContent = "0 câu";
    if (viewModesContainer) viewModesContainer.style.display = "none";
    return;
  }

  stats.textContent = `${segments.length} câu`;
  if (viewModesContainer) viewModesContainer.style.display = "flex";
  container.innerHTML = "";

  const tgtLangCode = responseData.target_lang || "vi";
  const tgtInfo = LANG_FLAG_MAP[tgtLangCode] || { flag: "🌐", name: tgtLangCode.toUpperCase(), tag: tgtLangCode.toUpperCase() };

  segments.forEach((seg, idx) => {
    const row = document.createElement("div");
    row.className = "timeline-row";
    row.setAttribute("data-start", seg.start);
    row.setAttribute("data-end", seg.end);

    // Col 1: Time Stamps & Precise Duration
    const timeCol = document.createElement("div");
    timeCol.className = "timeline-time-col";

    const timeStampPill = document.createElement("div");
    timeStampPill.className = "time-stamp-pill";
    timeStampPill.textContent = `${formatTimeSec(seg.start)} ➜ ${formatTimeSec(seg.end)}`;

    const durationText = document.createElement("div");
    durationText.className = "time-duration-text";
    const durationVal = seg.duration || roundNum(seg.end - seg.start);
    durationText.textContent = `⏱️ Dài: ${durationVal}s`;

    timeCol.appendChild(timeStampPill);
    timeCol.appendChild(durationText);

    // Col 2: Content Column with Bilingual Display
    const contentCol = document.createElement("div");
    contentCol.className = "timeline-content-col";

    const srcText = seg.original_text || seg.text || "";
    const tgtText = seg.translated_text || (seg.translations && seg.translations[tgtLangCode]) || (responseData.translated ? seg.text : "");

    // 1. Source Language Row (if mode is bilingual or source)
    if (currentViewMode === "bilingual" || currentViewMode === "source") {
      const srcRow = document.createElement("div");
      srcRow.className = "lang-row";
      srcRow.innerHTML = `
        <span class="lang-tag-pill lang-tag-src">${langInfo.flag} ${langInfo.tag}</span>
        <span class="lang-text-src" contenteditable="true" title="Chỉnh sửa câu gốc">${escapeHtml(srcText)}</span>
      `;
      const srcTextEl = srcRow.querySelector(".lang-text-src");
      srcTextEl.addEventListener("blur", () => {
        currentSegments[idx].original_text = srcTextEl.textContent.trim();
        if (!currentSegments[idx].translations) currentSegments[idx].translations = {};
        currentSegments[idx].translations[langCode] = srcTextEl.textContent.trim();
      });
      contentCol.appendChild(srcRow);
    }

    // 2. Translation Language Row (if mode is bilingual or translation, and translation exists)
    if ((currentViewMode === "bilingual" || currentViewMode === "translation") && (tgtText || responseData.translated)) {
      const tgtRow = document.createElement("div");
      tgtRow.className = "lang-row";
      tgtRow.innerHTML = `
        <span class="lang-tag-pill lang-tag-tgt">${tgtInfo.flag} ${tgtInfo.tag}</span>
        <span class="lang-text-tgt" contenteditable="true" title="Chỉnh sửa câu dịch">${escapeHtml(tgtText || "⏳ Đang dịch...")}</span>
      `;
      const tgtTextEl = tgtRow.querySelector(".lang-text-tgt");
      tgtTextEl.addEventListener("blur", () => {
        currentSegments[idx].translated_text = tgtTextEl.textContent.trim();
        currentSegments[idx].text = tgtTextEl.textContent.trim();
        if (!currentSegments[idx].translations) currentSegments[idx].translations = {};
        currentSegments[idx].translations[tgtLangCode] = tgtTextEl.textContent.trim();
      });
      contentCol.appendChild(tgtRow);
    }

    row.appendChild(timeCol);
    row.appendChild(contentCol);

    // Audio seek on click
    row.addEventListener("click", (e) => {
      if (!e.target.isContentEditable) {
        const audio = document.getElementById("audioPlayer");
        if (audio && audio.src) {
          audio.currentTime = seg.start;
          audio.play();
        }
      }
    });

    container.appendChild(row);
  });
}

function escapeHtml(str) {
  if (!str) return "";
  return str.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

function roundNum(num) {
  return Math.round(num * 100) / 100;
}

function formatTimeSec(sec) {
  const m = Math.floor(sec / 60);
  const s = Math.floor(sec % 60);
  const ms = Math.floor((sec % 1) * 100);
  return `${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}.${ms.toString().padStart(2, "0")}`;
}

function formatTimeSrt(sec) {
  const h = Math.floor(sec / 3600);
  const m = Math.floor((sec % 3600) / 60);
  const s = Math.floor(sec % 60);
  const ms = Math.floor((sec % 1) * 1000);
  return `${h.toString().padStart(2, "0")}:${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")},${ms.toString().padStart(3, "0")}`;
}

function formatTimeAss(sec) {
  const h = Math.floor(sec / 3600);
  const m = Math.floor((sec % 3600) / 60);
  const s = Math.floor(sec % 60);
  const cs = Math.floor((sec % 1) * 100);
  return `${h}:${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}.${cs.toString().padStart(2, "0")}`;
}

// ==========================================================================
// 9. Multi-format & Multi-language Subtitle Exporters
// ==========================================================================

// 9.1 Export Standard Multilingual JSON for App / FlowMy Import
function exportMultilingualJSON() {
  if (!currentSegments || currentSegments.length === 0) {
    showToast("⚠️ Chưa có phụ đề để xuất file!", "error");
    return;
  }

  const srcLang = currentAudioMeta.language || "auto";
  const tgtLang = currentAudioMeta.target_lang || "vi";

  const exportData = {
    version: "1.0",
    engine: currentEngine,
    source_language: srcLang,
    source_language_name: currentAudioMeta.language_name || srcLang,
    total_segments: currentSegments.length,
    total_duration_seconds: currentAudioMeta.duration || 0,
    created_at: new Date().toISOString(),
    subtitles: currentSegments.map((seg, i) => {
      const transMap = seg.translations || {};
      if (seg.original_text) transMap[srcLang] = seg.original_text;
      if (seg.translated_text) transMap[tgtLang] = seg.translated_text;

      return {
        id: seg.id || (i + 1),
        start: seg.start,
        end: seg.end,
        duration: seg.duration || roundNum(seg.end - seg.start),
        start_time: formatTimeSrt(seg.start),
        end_time: formatTimeSrt(seg.end),
        text: seg.translated_text || seg.text,
        original_text: seg.original_text || seg.text,
        translations: transMap
      };
    })
  };

  downloadFile(JSON.stringify(exportData, null, 2), `subtitles_multilingual_${Date.now()}.json`, "application/json");
  showToast("📦 Đã xuất file JSON đa ngôn ngữ chuẩn App thành công!", "success");
}

// 9.2 Export Bilingual SRT (Line 1: Original text, Line 2: Translated text)
function exportBilingualSRT() {
  if (!currentSegments || currentSegments.length === 0) {
    showToast("⚠️ Chưa có phụ đề để xuất file!", "error");
    return;
  }

  let content = "";
  currentSegments.forEach((seg, i) => {
    content += `${i + 1}\n`;
    content += `${formatTimeSrt(seg.start)} --> ${formatTimeSrt(seg.end)}\n`;
    if (seg.original_text && seg.translated_text && seg.original_text !== seg.translated_text) {
      content += `${seg.original_text}\n${seg.translated_text}\n\n`;
    } else {
      content += `${seg.text}\n\n`;
    }
  });

  downloadFile(content, `subtitles_bilingual_${Date.now()}.srt`, "text/plain");
  showToast("📄 Đã xuất file SRT Song Ngữ thành công!", "success");
}

// 9.3 Export Bilingual ASS with Color Styles
function exportBilingualASS() {
  if (!currentSegments || currentSegments.length === 0) {
    showToast("⚠️ Chưa có phụ đề để xuất file!", "error");
    return;
  }

  let content = `[Script Info]
Title: FlowMy Bilingual Subtitles
ScriptType: v4.00+
PlayResX: 1920
PlayResY: 1080

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Default,Arial,22,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,2,2,10,10,15,1
Style: Translated,Arial,24,&H0000D7FF,&H000000FF,&H00000000,&H00000000,1,0,0,0,100,100,0,0,1,2,2,2,10,10,48,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
`;

  currentSegments.forEach((seg) => {
    const startT = formatTimeAss(seg.start);
    const endT = formatTimeAss(seg.end);
    if (seg.original_text && seg.translated_text && seg.original_text !== seg.translated_text) {
      content += `Dialogue: 0,${startT},${endT},Translated,,0,0,0,,${seg.translated_text}\n`;
      content += `Dialogue: 0,${startT},${endT},Default,,0,0,0,,${seg.original_text}\n`;
    } else {
      content += `Dialogue: 0,${startT},${endT},Default,,0,0,0,,${seg.text}\n`;
    }
  });

  downloadFile(content, `subtitles_bilingual_${Date.now()}.ass`, "text/plain");
  showToast("🎨 Đã xuất file ASS Đa Sắc Song Ngữ thành công!", "success");
}

// 9.4 Export Single Language SRT
function exportSingleSRT(type = "translated") {
  if (!currentSegments || currentSegments.length === 0) {
    showToast("⚠️ Chưa có phụ đề để xuất file!", "error");
    return;
  }

  let content = "";
  currentSegments.forEach((seg, i) => {
    content += `${i + 1}\n`;
    content += `${formatTimeSrt(seg.start)} --> ${formatTimeSrt(seg.end)}\n`;
    const textOut = (type === "source") ? (seg.original_text || seg.text) : (seg.translated_text || seg.text);
    content += `${textOut}\n\n`;
  });

  const label = type === "source" ? "goc" : "dich";
  downloadFile(content, `subtitles_${label}_${Date.now()}.srt`, "text/plain");
  showToast(`💾 Đã xuất file SRT ${type === "source" ? "Bản Gốc" : "Bản Dịch"} thành công!`, "success");
}

// 9.5 Export Plain Text
function exportTxtFile() {
  if (!currentSegments || currentSegments.length === 0) {
    showToast("⚠️ Chưa có phụ đề để xuất file!", "error");
    return;
  }

  let content = "";
  currentSegments.forEach((seg) => {
    content += `[${formatTimeSec(seg.start)} ➜ ${formatTimeSec(seg.end)} (Dài: ${seg.duration || roundNum(seg.end - seg.start)}s)]\n`;
    if (seg.original_text) content += `Gốc: ${seg.original_text}\n`;
    if (seg.translated_text) content += `Dịch: ${seg.translated_text}\n`;
    if (!seg.original_text && !seg.translated_text) content += `${seg.text}\n`;
    content += `\n`;
  });

  downloadFile(content, `subtitles_${Date.now()}.txt`, "text/plain");
  showToast("📝 Đã xuất file TXT thành công!", "success");
}

function downloadFile(content, filename, mimeType) {
  const blob = new Blob([content], { type: mimeType });
  const a = document.createElement("a");
  a.href = URL.createObjectURL(blob);
  a.download = filename;
  a.click();
}

// ==========================================================================
// 10. FlowMy API Snippet Sync & Copy
// ==========================================================================
function updateApiSnippet() {
  const isTranslate = document.getElementById("translateToggle")?.checked || false;
  const targetLang = document.getElementById("targetLangSelect")?.value || "vi";
  const modelSize = document.getElementById("modelSizeSelect")?.value || "small";

  const jsonCode = `{\n  "audio_path": "{{linkAudio}}",\n  "chunkIndex": {{chunkIndex}},\n  "engine": "${currentEngine}",\n  "model_size": "${modelSize}",\n  "enable_translate": ${isTranslate},\n  "target_lang": "${targetLang}"\n}`;
  const codeEl = document.getElementById("apiJsonSnippet");
  if (codeEl) codeEl.textContent = jsonCode;
}

function initApiCopy() {
  document.getElementById("btnCopyApi")?.addEventListener("click", () => {
    const code = document.getElementById("apiJsonSnippet").textContent;
    navigator.clipboard.writeText(code);
    showToast("📋 Đã sao chép cấu hình JSON cho HttpRequestNode!", "success");
  });
}

// ==========================================================================
// 11. Toast Notification System
// ==========================================================================
function showToast(message, type = "info") {
  const container = document.getElementById("toastContainer");
  if (!container) return;

  const toast = document.createElement("div");
  toast.className = `toast toast-${type}`;
  toast.innerHTML = `<span>${message}</span>`;

  container.appendChild(toast);

  setTimeout(() => {
    toast.style.opacity = "0";
    toast.style.transform = "translateX(50px)";
    toast.style.transition = "all 0.3s ease";
    setTimeout(() => toast.remove(), 300);
  }, 4000);
}

// ==========================================================================
// 12. Main Navigation Tabs (Studio vs Swagger Explorer)
// ==========================================================================
function switchMainTab(tab) {
  const tabStudio = document.getElementById("tabBtnStudio");
  const tabSwagger = document.getElementById("tabBtnSwagger");
  const viewStudio = document.getElementById("viewStudio");
  const viewSwagger = document.getElementById("viewSwagger");

  if (tab === "swagger") {
    tabStudio?.classList.remove("active");
    tabSwagger?.classList.add("active");
    viewStudio?.classList.remove("active");
    viewSwagger?.classList.add("active");
    try { localStorage.setItem("flowmy_main_tab", "swagger"); } catch (e) {}
  } else {
    tabSwagger?.classList.remove("active");
    tabStudio?.classList.add("active");
    viewSwagger?.classList.remove("active");
    viewStudio?.classList.add("active");
    try { localStorage.setItem("flowmy_main_tab", "studio"); } catch (e) {}
  }
}
window.switchMainTab = switchMainTab;

// ==========================================================================
// 13. Interactive Swagger Explorer Logic
// ==========================================================================
function toggleSwaggerCard(cardId) {
  const card = document.getElementById(cardId);
  if (card) card.classList.toggle("expanded");
}
window.toggleSwaggerCard = toggleSwaggerCard;

function expandAllSwaggerCards(expand) {
  document.querySelectorAll(".swagger-card").forEach(card => {
    if (expand) card.classList.add("expanded");
    else card.classList.remove("expanded");
  });
}
window.expandAllSwaggerCards = expandAllSwaggerCards;

function filterSwaggerEndpoints() {
  const q = document.getElementById("swaggerSearchInput")?.value.toLowerCase().trim() || "";
  document.querySelectorAll(".swagger-card").forEach(card => {
    const text = card.textContent.toLowerCase();
    if (!q || text.includes(q)) {
      card.style.display = "block";
    } else {
      card.style.display = "none";
    }
  });
}
window.filterSwaggerEndpoints = filterSwaggerEndpoints;

function setSwaggerTranscribeMode(mode) {
  const btnJson = document.getElementById("swModeJson-transcribe");
  const btnForm = document.getElementById("swModeForm-transcribe");
  const cJson = document.getElementById("swJsonContainer-transcribe");
  const cForm = document.getElementById("swFormContainer-transcribe");

  if (mode === "form") {
    btnJson?.classList.remove("active");
    btnForm?.classList.add("active");
    if (cJson) cJson.style.display = "none";
    if (cForm) cForm.style.display = "block";
  } else {
    btnForm?.classList.remove("active");
    btnJson?.classList.add("active");
    if (cForm) cForm.style.display = "none";
    if (cJson) cJson.style.display = "block";
  }
}
window.setSwaggerTranscribeMode = setSwaggerTranscribeMode;

function resetSwaggerTranscribeJson() {
  const el = document.getElementById("swJsonInput-transcribe");
  if (el) {
    el.value = JSON.stringify({
      audio_path: "D:\\UngDung_PC\\Flow-App\\audio_sample.mp3",
      chunkIndex: 0,
      engine: "faster-whisper",
      model_size: "small",
      enable_translate: true,
      target_lang: "vi",
      device: "auto",
      compute_type: "default"
    }, null, 2);
    showToast("🔄 Đã khôi phục mẫu JSON /api/transcribe!", "info");
  }
}
window.resetSwaggerTranscribeJson = resetSwaggerTranscribeJson;

function resetSwaggerTranslateJson() {
  const el = document.getElementById("swJsonInput-translate");
  if (el) {
    el.value = JSON.stringify({
      src_lang: "en",
      target_lang: "vi",
      segments: [
        {
          id: 1,
          start: 1.2,
          end: 4.5,
          duration: 3.3,
          text: "Hello world, welcome to FlowMy subtitle studio.",
          original_text: "Hello world, welcome to FlowMy subtitle studio."
        },
        {
          id: 2,
          start: 8.0,
          end: 11.2,
          duration: 3.2,
          text: "This is a powerful bilingual translation tool.",
          original_text: "This is a powerful bilingual translation tool."
        }
      ]
    }, null, 2);
    showToast("🔄 Đã khôi phục mẫu JSON /api/translate-segments!", "info");
  }
}
window.resetSwaggerTranslateJson = resetSwaggerTranslateJson;

function copyText(text) {
  navigator.clipboard.writeText(text);
  showToast("📋 Đã sao chép vào bộ nhớ đệm!", "success");
}
window.copyText = copyText;

function copyElementText(elId) {
  const el = document.getElementById(elId);
  if (el) {
    navigator.clipboard.writeText(el.value || el.textContent);
    showToast("📋 Đã sao chép nội dung!", "success");
  }
}
window.copyElementText = copyElementText;

function copySwaggerCurl(key) {
  let curl = "";
  if (key === "transcribe") {
    const isForm = document.getElementById("swModeForm-transcribe")?.classList.contains("active");
    if (isForm) {
      const fileInput = document.getElementById("swFileInput-transcribe");
      const audioPath = document.getElementById("swAudioPathInput-transcribe")?.value.trim() || "";
      const engine = document.getElementById("swEngineInput-transcribe")?.value || "faster-whisper";
      const modelSize = document.getElementById("swModelSizeInput-transcribe")?.value || "small";
      const enableTranslate = document.getElementById("swTranslateInput-transcribe")?.value || "true";
      const targetLang = document.getElementById("swTargetLangInput-transcribe")?.value || "vi";
      const device = document.getElementById("swDeviceInput-transcribe")?.value || "auto";
      const computeType = document.getElementById("swComputeTypeInput-transcribe")?.value || "default";

      const fileParam = (fileInput && fileInput.files && fileInput.files.length > 0)
        ? `  -F "file=@${fileInput.files[0].name}" \\\n`
        : `  -F "file=@D:/path/to/audio.mp3" \\\n`;
      const pathParam = audioPath ? `  -F "audio_path=${audioPath}" \\\n` : "";

      curl = `curl -X POST "http://127.0.0.1:8765/api/transcribe" \\\n${fileParam}${pathParam}  -F "engine=${engine}" \\\n  -F "model_size=${modelSize}" \\\n  -F "enable_translate=${enableTranslate}" \\\n  -F "target_lang=${targetLang}" \\\n  -F "device=${device}" \\\n  -F "compute_type=${computeType}"`;
    } else {
      const body = document.getElementById("swJsonInput-transcribe")?.value || "{}";
      curl = `curl -X POST "http://127.0.0.1:8765/api/transcribe" \\\n  -H "Content-Type: application/json" \\\n  -d '${body.replace(/\n/g, " ")}'`;
    }
  } else if (key === "translate") {
    const body = document.getElementById("swJsonInput-translate")?.value || "{}";
    curl = `curl -X POST "http://127.0.0.1:8765/api/translate-segments" \\\n  -H "Content-Type: application/json" \\\n  -d '${body.replace(/\n/g, " ")}'`;
  }
  copyText(curl);
}
window.copySwaggerCurl = copySwaggerCurl;

function copySwaggerTranscribeUrl() {
  copyText("http://127.0.0.1:8765/api/transcribe");
}
window.copySwaggerTranscribeUrl = copySwaggerTranscribeUrl;

function onSwaggerFormChanged() {
  // Sync form inputs to JSON textarea
  const audioPath = document.getElementById("swAudioPathInput-transcribe")?.value || "D:\\UngDung_PC\\Flow-App\\audio_sample.mp3";
  const engine = document.getElementById("swEngineInput-transcribe")?.value || "faster-whisper";
  const modelSize = document.getElementById("swModelSizeInput-transcribe")?.value || "small";
  const enableTranslate = document.getElementById("swTranslateInput-transcribe")?.value === "true";
  const targetLang = document.getElementById("swTargetLangInput-transcribe")?.value || "vi";
  const device = document.getElementById("swDeviceInput-transcribe")?.value || "auto";
  const computeType = document.getElementById("swComputeTypeInput-transcribe")?.value || "default";

  const jsonEl = document.getElementById("swJsonInput-transcribe");
  if (jsonEl) {
    jsonEl.value = JSON.stringify({
      audio_path: audioPath,
      chunkIndex: 0,
      engine: engine,
      model_size: modelSize,
      enable_translate: enableTranslate,
      target_lang: targetLang,
      device: device,
      compute_type: computeType
    }, null, 2);
  }
}
window.onSwaggerFormChanged = onSwaggerFormChanged;

function copySwaggerCurlGet(path) {
  copyText(`curl -X GET "http://127.0.0.1:8765${path}"`);
}
window.copySwaggerCurlGet = copySwaggerCurlGet;

function copySwaggerCurlJsonPost(path, inputId) {
  const body = document.getElementById(inputId)?.value || "{}";
  copyText(`curl -X POST "http://127.0.0.1:8765${path}" \\\n  -H "Content-Type: application/json" \\\n  -d '${body.replace(/\n/g, " ")}'`);
}
window.copySwaggerCurlJsonPost = copySwaggerCurlJsonPost;

// ==========================================================================
// 14. Swagger API Live Execution Handlers
// ==========================================================================
async function executeSwaggerTranscribe() {
  const btn = document.getElementById("swBtnExec-transcribe");
  const panel = document.getElementById("swResponsePanel-transcribe");
  const statusPill = document.getElementById("swStatusPill-transcribe");
  const timePill = document.getElementById("swTimePill-transcribe");
  const bodyEl = document.getElementById("swResponseBody-transcribe");
  const isForm = document.getElementById("swModeForm-transcribe")?.classList.contains("active");

  btn.disabled = true;
  btn.textContent = "⏳ Đang thực thi...";
  panel.style.display = "block";
  bodyEl.textContent = "Đang gửi yêu cầu và đợi phản hồi từ AI Server (Tự động nạp model vào GPU nếu chưa có)...";

  const t0 = performance.now();
  try {
    let res;
    if (isForm) {
      const fileInput = document.getElementById("swFileInput-transcribe");
      const audioPath = document.getElementById("swAudioPathInput-transcribe")?.value.trim() || "";

      if ((!fileInput.files || fileInput.files.length === 0) && !audioPath) {
        throw new Error("Vui lòng chọn một tệp âm thanh hoặc nhập đường dẫn file (audio_path) trước khi bấm Thực Thi!");
      }

      const formData = new FormData();
      if (fileInput.files && fileInput.files.length > 0) {
        formData.append("file", fileInput.files[0]);
      }
      if (audioPath) {
        formData.append("audio_path", audioPath);
      }
      formData.append("engine", document.getElementById("swEngineInput-transcribe")?.value || "faster-whisper");
      formData.append("model_size", document.getElementById("swModelSizeInput-transcribe")?.value || "small");
      formData.append("enable_translate", document.getElementById("swTranslateInput-transcribe")?.value === "true");
      formData.append("target_lang", document.getElementById("swTargetLangInput-transcribe")?.value || "vi");
      formData.append("device", document.getElementById("swDeviceInput-transcribe")?.value || "auto");
      formData.append("compute_type", document.getElementById("swComputeTypeInput-transcribe")?.value || "default");

      res = await fetch("/api/transcribe", { method: "POST", body: formData });
    } else {
      const jsonStr = document.getElementById("swJsonInput-transcribe")?.value || "{}";
      const payload = JSON.parse(jsonStr);
      res = await fetch("/api/transcribe", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
      });
    }

    const elapsed = Math.round(performance.now() - t0);
    const data = await res.json();

    statusPill.textContent = `${res.status} ${res.statusText || (res.ok ? "OK" : "Error")}`;
    statusPill.className = `swagger-status-pill status-${res.ok ? "200" : "500"}`;
    timePill.textContent = `⚡ ${elapsed}ms`;
    bodyEl.textContent = JSON.stringify(data, null, 2);

    if (res.ok) {
      showToast(`🎉 /api/transcribe thành công trong ${elapsed}ms!`, "success");
      initVramActiveTracker();
      initModelsList();
    } else {
      showToast(`⚠️ Server trả về lỗi: ${data.detail || res.statusText}`, "warning");
    }
  } catch (err) {
    const elapsed = Math.round(performance.now() - t0);
    statusPill.textContent = `Lỗi Thực Thi`;
    statusPill.className = `swagger-status-pill status-500`;
    timePill.textContent = `⚡ ${elapsed}ms`;
    bodyEl.textContent = `Lỗi thực thi: ${err.message}`;
    showToast(`❌ Lỗi: ${err.message}`, "error");
  } finally {
    btn.disabled = false;
    btn.textContent = "⚡ Thực Thi API (Execute)";
  }
}
window.executeSwaggerTranscribe = executeSwaggerTranscribe;

async function executeSwaggerTranslate() {
  const btn = document.getElementById("swBtnExec-translate");
  const panel = document.getElementById("swResponsePanel-translate");
  const statusPill = document.getElementById("swStatusPill-translate");
  const timePill = document.getElementById("swTimePill-translate");
  const bodyEl = document.getElementById("swResponseBody-translate");

  btn.disabled = true;
  btn.textContent = "⏳ Đang dịch...";
  panel.style.display = "block";
  bodyEl.textContent = "Đang gửi mảng timeline đến NLLB AI Engine...";

  const t0 = performance.now();
  try {
    const jsonStr = document.getElementById("swJsonInput-translate")?.value || "{}";
    const payload = JSON.parse(jsonStr);

    const res = await fetch("/api/translate-segments", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });

    const elapsed = Math.round(performance.now() - t0);
    const data = await res.json();

    statusPill.textContent = `${res.status} ${res.statusText || (res.ok ? "OK" : "Error")}`;
    statusPill.className = `swagger-status-pill status-${res.ok ? "200" : "500"}`;
    timePill.textContent = `⚡ ${elapsed}ms`;
    bodyEl.textContent = JSON.stringify(data, null, 2);

    if (res.ok) {
      showToast(`🎉 Dịch timeline thành công trong ${elapsed}ms!`, "success");
    }
  } catch (err) {
    const elapsed = Math.round(performance.now() - t0);
    statusPill.textContent = `Lỗi Client`;
    statusPill.className = `swagger-status-pill status-500`;
    timePill.textContent = `⚡ ${elapsed}ms`;
    bodyEl.textContent = `Lỗi: ${err.message}`;
    showToast(`❌ Lỗi: ${err.message}`, "error");
  } finally {
    btn.disabled = false;
    btn.textContent = "⚡ Execute (Thực Thi API)";
  }
}
window.executeSwaggerTranslate = executeSwaggerTranslate;

async function executeSwaggerSimpleGet(path, cardKey) {
  const panel = document.getElementById(`swResponsePanel-${cardKey}`);
  const statusPill = document.getElementById(`swStatusPill-${cardKey}`);
  const timePill = document.getElementById(`swTimePill-${cardKey}`);
  const bodyEl = document.getElementById(`swResponseBody-${cardKey}`);

  panel.style.display = "block";
  bodyEl.textContent = `Đang gọi GET ${path}...`;

  const t0 = performance.now();
  try {
    const res = await fetch(path);
    const elapsed = Math.round(performance.now() - t0);
    const data = await res.json();

    statusPill.textContent = `${res.status} ${res.statusText || (res.ok ? "OK" : "Error")}`;
    statusPill.className = `swagger-status-pill status-${res.ok ? "200" : "500"}`;
    timePill.textContent = `⚡ ${elapsed}ms`;
    bodyEl.textContent = JSON.stringify(data, null, 2);

    if (res.ok) showToast(`✅ GET ${path} (${elapsed}ms)`, "success");
  } catch (err) {
    const elapsed = Math.round(performance.now() - t0);
    statusPill.textContent = `Lỗi Kết Nối`;
    statusPill.className = `swagger-status-pill status-500`;
    timePill.textContent = `⚡ ${elapsed}ms`;
    bodyEl.textContent = `Lỗi: ${err.message}`;
    showToast(`❌ Lỗi: ${err.message}`, "error");
  }
}
window.executeSwaggerSimpleGet = executeSwaggerSimpleGet;

async function executeSwaggerJsonPost(path, inputId, cardKey) {
  const panel = document.getElementById(`swResponsePanel-${cardKey}`);
  const statusPill = document.getElementById(`swStatusPill-${cardKey}`);
  const timePill = document.getElementById(`swTimePill-${cardKey}`);
  const bodyEl = document.getElementById(`swResponseBody-${cardKey}`);

  panel.style.display = "block";
  bodyEl.textContent = `Đang gọi POST ${path}...`;

  const t0 = performance.now();
  try {
    const jsonStr = document.getElementById(inputId)?.value || "{}";
    const payload = JSON.parse(jsonStr);

    const res = await fetch(path, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });

    const elapsed = Math.round(performance.now() - t0);
    const data = await res.json();

    statusPill.textContent = `${res.status} ${res.statusText || (res.ok ? "OK" : "Error")}`;
    statusPill.className = `swagger-status-pill status-${res.ok ? "200" : "500"}`;
    timePill.textContent = `⚡ ${elapsed}ms`;
    bodyEl.textContent = JSON.stringify(data, null, 2);

    if (res.ok) {
      showToast(`✅ POST ${path} thành công (${elapsed}ms)!`, "success");
      initModelsList();
      initVramActiveTracker();
    }
  } catch (err) {
    const elapsed = Math.round(performance.now() - t0);
    statusPill.textContent = `Lỗi Request`;
    statusPill.className = `swagger-status-pill status-500`;
    timePill.textContent = `⚡ ${elapsed}ms`;
    bodyEl.textContent = `Lỗi: ${err.message}`;
    showToast(`❌ Lỗi: ${err.message}`, "error");
  }
}
window.executeSwaggerJsonPost = executeSwaggerJsonPost;


