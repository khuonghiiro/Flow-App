/**
 * Export Manager & Video Rendering Pipeline
 */
class ExportManager {
  constructor() {
    this.selectedEngine = "flow";
    this.selectedLoopMode = "seamless_phase";
    this.selectedFormat = "mp4";
    this.selectedResolution = 1.0;
    this.selectedFps = 30;

    this.modal = document.getElementById("exportModal");
    this.btnOpen = document.getElementById("btnExportModal");
    this.btnClose = document.getElementById("btnCloseExportModal");
    this.btnStart = document.getElementById("btnStartExport");
    this.progressSection = document.getElementById("exportProgressSection");
    this.resultSection = document.getElementById("exportResultSection");
    this.progressBar = document.getElementById("exportProgressBar");
    this.statusMsg = document.getElementById("exportStatusMessage");
    this.percentText = document.getElementById("exportPercent");
    this.btnDownload = document.getElementById("btnDownloadResult");
  }

  init(getExportPayload) {
    this.getExportPayload = getExportPayload;

    if (this.btnOpen) {
      this.btnOpen.onclick = () => {
        this.resetModal();
        this.modal.classList.add("show");
      };
    }

    if (this.btnClose) {
      this.btnClose.onclick = () => this.modal.classList.remove("show");
    }

    // Interactive button group selectors (data-group & data-val)
    const optionButtons = this.modal ? this.modal.querySelectorAll("[data-group]") : [];
    optionButtons.forEach(btn => {
      btn.onclick = (e) => {
        e.preventDefault();
        const group = btn.dataset.group;
        const val = btn.dataset.val;

        // Deselect other buttons in same group
        this.modal.querySelectorAll(`[data-group="${group}"]`).forEach(b => {
          b.classList.remove("active");
        });
        btn.classList.add("active");

        if (group === "engine") {
          this.selectedEngine = val;
          const isAI = (val === "animatediff" || val === "svd");
          const loopSection = document.getElementById("groupLoopModeSection");
          const promptSection = document.getElementById("aiPromptSection");
          if (loopSection) {
            loopSection.style.opacity = isAI ? "0.4" : "1.0";
            loopSection.style.pointerEvents = isAI ? "none" : "auto";
          }
          if (promptSection) {
            promptSection.style.display = isAI ? "block" : "none";
          }
        } else if (group === "loop") {
          this.selectedLoopMode = val;
        } else if (group === "format") {
          this.selectedFormat = val;
        } else if (group === "resolution") {
          this.selectedResolution = parseFloat(val) || 1.0;
        } else if (group === "fps") {
          this.selectedFps = parseInt(val) || 30;
        }
      };
    });

    if (this.btnStart) {
      this.btnStart.onclick = () => this.startRendering();
    }
  }

  resetModal() {
    this.progressSection.style.display = "none";
    this.resultSection.style.display = "none";
    this.btnStart.style.display = "block";
    this.btnStart.disabled = false;
    this.progressBar.style.width = "0%";
    this.percentText.innerText = "0%";
    this.statusMsg.innerText = "Đang chuẩn bị render...";
  }

  async startRendering() {
    if (!this.getExportPayload) return;
    const payload = this.getExportPayload();
    if (!payload.image) {
      alert("Vui lòng tải lên hoặc chọn ảnh mẫu trước khi render!");
      return;
    }

    payload.format = this.selectedFormat;
    payload.loop_mode = this.selectedLoopMode;
    payload.resolution_scale = this.selectedResolution;
    payload.fps = this.selectedFps;

    this.btnStart.style.display = "none";
    this.progressSection.style.display = "block";
    this.progressBar.style.width = "10%";
    this.percentText.innerText = "10%";
    this.statusMsg.innerText = "Đang gửi tác vụ đến GPU RTX 3060...";

    try {
      let resp;
      if (this.selectedEngine === "animatediff" || this.selectedEngine === "svd") {
        const inputPrompt = document.getElementById("inputAiPrompt");
        const promptText = inputPrompt ? inputPrompt.value : "masterpiece, flowing black hair, silk robes fluttering in wind";
        this.statusMsg.innerText = `Đang xử lý ${this.selectedEngine === 'animatediff' ? 'AnimateDiff Anime' : 'SVD'} trên GPU RTX 3060...`;
        
        resp = await window.apiClient.submitDiffusionAnimation({
          image: payload.image,
          model_type: this.selectedEngine,
          prompt: promptText,
          fps: Math.min(24, this.selectedFps),
          num_frames: 16
        });
      } else {
        resp = await window.apiClient.submitFlowAnimation(payload);
      }
      
      const taskId = resp.task_id;
      this.pollProgress(taskId);
    } catch (err) {
      this.statusMsg.innerText = `Lỗi: ${err.message}`;
      this.btnStart.style.display = "block";
    }
  }

  pollProgress(taskId) {
    let attempts = 0;
    const interval = setInterval(async () => {
      attempts++;
      try {
        const task = await window.apiClient.getTaskStatus(taskId);
        const percent = Math.round(task.progress * 100);
        this.progressBar.style.width = `${Math.max(10, percent)}%`;
        this.percentText.innerText = `${percent}%`;
        this.statusMsg.innerText = task.message || "Đang render khung hình...";

        if (task.status === "completed") {
          clearInterval(interval);
          this.showCompleted(task);
        } else if (task.status === "failed") {
          clearInterval(interval);
          this.statusMsg.innerText = `Lỗi: ${task.error || "Thất bại"}`;
          this.btnStart.style.display = "block";
        }
      } catch (e) {
        if (attempts > 120) {
          clearInterval(interval);
          this.statusMsg.innerText = "Hết thời gian chờ phản hồi từ máy chủ.";
        }
      }
    }, 500);
  }

  showCompleted(task) {
    this.progressSection.style.display = "none";
    this.resultSection.style.display = "block";
    this.btnDownload.href = task.result_url;
    this.btnDownload.innerText = `💾 Tải Video Về (${this.selectedFormat.toUpperCase()})`;
  }
}

window.ExportManager = ExportManager;

