// State
let currentEngine = "faster-whisper";
let selectedAudioFile = null;
let currentSegments = [];
let hardwareInfo = null;

document.addEventListener("DOMContentLoaded", () => {
  initHardwareInfo();
  initModelsList();
  initEngineSelector();
  initDropzone();
  initStudioActions();
  initApiCopy();
});

// 1. Fetch Hardware & Concurrency Advice
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

    // Highlight recommended card
    if (hardwareInfo.recommended_engine) {
      selectEngine(hardwareInfo.recommended_engine);
    }
  } catch (err) {
    console.error("Hardware fetch error:", err);
  }
}

// 2. Engine Selector Cards
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

  const modelSizeSelect = document.getElementById("modelSizeSelect");
  if (engineName === "sensevoice") {
    modelSizeSelect.innerHTML = `<option value="base" selected>SenseVoice-Small (Base - Siêu nhẹ)</option>`;
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

  updateApiSnippet();
}

// 3. Audio Dropzone & Player
function initDropzone() {
  const dropzone = document.getElementById("dropzone");
  const fileInput = document.getElementById("audioFileInput");
  const audioPlayer = document.getElementById("audioPlayer");
  const playerBox = document.getElementById("audioPlayerBox");

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
    document.getElementById("dropzoneSub").textContent = `${(file.size / (1024 * 1024)).toFixed(2)} MB - Đã sẵn sàng`;

    const url = URL.createObjectURL(file);
    audioPlayer.src = url;
    playerBox.classList.add("active");
  }

  // Sync timeline row highlight with audio playback
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

// 4. Studio Process & Transcribe
function initStudioActions() {
  const btnProcess = document.getElementById("btnProcess");
  const timelineContainer = document.getElementById("timelineContainer");

  btnProcess.addEventListener("click", async () => {
    if (!selectedAudioFile) {
      alert("Vui lòng chọn hoặc kéo thả file audio vào Studio trước!");
      return;
    }

    const targetLang = document.getElementById("targetLangSelect").value;
    const modelSize = document.getElementById("modelSizeSelect").value;
    const device = document.getElementById("deviceSelect").value;
    const computeType = document.getElementById("computeTypeSelect").value;

    btnProcess.disabled = true;
    btnProcess.innerHTML = `⏳ Đang nhận diện &amp; dịch thuật (${currentEngine})...`;
    timelineContainer.innerHTML = `<div class="empty-timeline">🎙️ Đang phân tích âm thanh và tạo phụ đề... Vui lòng đợi trong giây lát.</div>`;

    try {
      const formData = new FormData();
      formData.append("file", selectedAudioFile);
      formData.append("engine", currentEngine);
      formData.append("model_size", modelSize);
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

      renderTimeline(currentSegments, data.language);
      initModelsList(); // Update model cache list
    } catch (err) {
      alert(`❌ Lỗi: ${err.message}`);
      timelineContainer.innerHTML = `<div class="empty-timeline" style="color: #f87171;">Lỗi xử lý: ${err.message}</div>`;
    } finally {
      btnProcess.disabled = false;
      btnProcess.innerHTML = `🚀 Bắt Đầu Chuyển Đổi &amp; Dịch Phụ Đề`;
    }
  });

  // Export buttons
  document.getElementById("btnExportSrt").addEventListener("click", () => exportSubtitles("srt"));
  document.getElementById("btnExportAss").addEventListener("click", () => exportSubtitles("ass"));
  document.getElementById("btnExportJson").addEventListener("click", () => exportSubtitles("json"));
}

function renderTimeline(segments, lang) {
  const container = document.getElementById("timelineContainer");
  const stats = document.getElementById("timelineStats");

  if (!segments || segments.length === 0) {
    container.innerHTML = `<div class="empty-timeline">Không phát hiện được câu nói nào trong audio.</div>`;
    stats.textContent = "0 câu";
    return;
  }

  stats.textContent = `${segments.length} câu | Ngôn ngữ: ${(lang || "Tự động").toUpperCase()}`;
  container.innerHTML = "";

  segments.forEach((seg, idx) => {
    const row = document.createElement("div");
    row.className = "timeline-row";
    row.setAttribute("data-start", seg.start);
    row.setAttribute("data-end", seg.end);

    const timeCol = document.createElement("div");
    timeCol.className = "time-stamp";
    timeCol.textContent = `${formatTimeSec(seg.start)}`;

    const textCol = document.createElement("div");
    textCol.className = "subtitle-text";
    textCol.contentEditable = "true";
    textCol.textContent = seg.text;
    textCol.addEventListener("blur", () => {
      currentSegments[idx].text = textCol.textContent.trim();
    });

    if (seg.original_text && seg.original_text !== seg.text) {
      const orig = document.createElement("div");
      orig.className = "subtitle-original";
      orig.textContent = `Gốc: ${seg.original_text}`;
      textCol.appendChild(orig);
    }

    row.appendChild(timeCol);
    row.appendChild(textCol);

    // Click to seek audio
    row.addEventListener("click", (e) => {
      if (e.target !== textCol) {
        const audio = document.getElementById("audioPlayer");
        audio.currentTime = seg.start;
        audio.play();
      }
    });

    container.appendChild(row);
  });
}

function formatTimeSec(sec) {
  const m = Math.floor(sec / 60);
  const s = Math.floor(sec % 60);
  const ms = Math.floor((sec % 1) * 100);
  return `${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}.${ms.toString().padStart(2, "0")}`;
}

// 5. Export Subtitles
function exportSubtitles(format) {
  if (!currentSegments || currentSegments.length === 0) {
    alert("Chưa có phụ đề để xuất file!");
    return;
  }

  let content = "";
  let mimeType = "text/plain";
  let ext = format;

  if (format === "srt") {
    currentSegments.forEach((seg, i) => {
      content += `${i + 1}\n`;
      content += `${formatTimeSrt(seg.start)} --> ${formatTimeSrt(seg.end)}\n`;
      content += `${seg.text}\n\n`;
    });
  } else if (format === "ass") {
    content = `[Script Info]\nTitle: FlowMy Subtitles\nScriptType: v4.00+\n[V4+ Styles]\nFormat: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\nStyle: Default,Arial,20,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,2,2,10,10,10,1\n[Events]\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\n`;
    currentSegments.forEach((seg) => {
      content += `Dialogue: 0,${formatTimeAss(seg.start)},${formatTimeAss(seg.end)},Default,,0,0,0,,${seg.text}\n`;
    });
  } else if (format === "json") {
    content = JSON.stringify({ chunkIndex: 0, segments: currentSegments }, null, 2);
    mimeType = "application/json";
  }

  const blob = new Blob([content], { type: mimeType });
  const a = document.createElement("a");
  a.href = URL.createObjectURL(blob);
  a.download = `subtitles_${Date.now()}.${ext}`;
  a.click();
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

// 6. Model Cache Manager
async function initModelsList() {
  try {
    const res = await fetch("/api/models");
    if (!res.ok) return;
    const data = await res.json();

    document.getElementById("modelsDirText").textContent = data.models_dir;
    document.getElementById("totalModelSize").textContent = `${data.total_size_mb} MB`;

    const listEl = document.getElementById("installedModelsList");
    if (!data.installed_models || data.installed_models.length === 0) {
      listEl.innerHTML = `<div style="font-size:11px; color:#64748b;">Chưa có model nào tải về. Model sẽ tự động tải khi chạy lần đầu.</div>`;
      return;
    }

    listEl.innerHTML = "";
    data.installed_models.forEach(m => {
      const item = document.createElement("div");
      item.className = "model-list-item";
      item.innerHTML = `
        <div>
          <strong style="color:#60a5fa;">${m.model_name}</strong> 
          <span style="color:#64748b; font-size:10.5px;">(${m.engine})</span>
          <div style="font-size:10.5px; color:#94a3b8;">Dung lượng: ${m.size_mb} MB</div>
        </div>
        <button class="btn btn-danger btn-sm" onclick="deleteModel('${m.path.replace(/\\/g, "\\\\")}')">🗑 Xóa</button>
      `;
      listEl.appendChild(item);
    });
  } catch (err) {
    console.error("Models fetch error:", err);
  }
}

async function deleteModel(path) {
  if (!confirm(`Bạn có chắc chắn muốn xóa model tại: ${path}?`)) return;
  try {
    const res = await fetch("/api/models/delete", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ model_path: path })
    });
    if (res.ok) {
      alert("✅ Đã xóa model thành công!");
      initModelsList();
    }
  } catch (err) {
    alert("❌ Lỗi xóa model: " + err.message);
  }
}

// 7. FlowMy API Snippet Sync
function updateApiSnippet() {
  const targetLang = document.getElementById("targetLangSelect")?.value || "vi";
  const modelSize = document.getElementById("modelSizeSelect")?.value || "small";
  const jsonCode = `{\n  "audio_path": "{{linkAudio}}",\n  "chunkIndex": {{chunkIndex}},\n  "target_lang": "${targetLang}",\n  "engine": "${currentEngine}",\n  "model_size": "${modelSize}"\n}`;
  const codeEl = document.getElementById("apiJsonSnippet");
  if (codeEl) codeEl.textContent = jsonCode;
}

function initApiCopy() {
  document.getElementById("btnCopyApi")?.addEventListener("click", () => {
    const code = document.getElementById("apiJsonSnippet").textContent;
    navigator.clipboard.writeText(code);
    alert("📋 Đã sao chép cấu hình JSON cho HttpRequestNode!");
  });
}
