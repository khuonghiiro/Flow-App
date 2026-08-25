/**
 * Export Manager & Video Rendering Pipeline
 */
class ExportManager {
  constructor() {
    this.selectedFormat = "mp4";
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

    // Format buttons
    ["btnFormatMp4", "btnFormatGif", "btnFormatWebm"].forEach(id => {
      const btn = document.getElementById(id);
      if (!btn) return;
      btn.onclick = () => {
        ["btnFormatMp4", "btnFormatGif", "btnFormatWebm"].forEach(b => {
          document.getElementById(b).classList.remove("active");
        });
        btn.classList.add("active");
        this.selectedFormat = btn.dataset.fmt;
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
    this.btnStart.style.display = "none";
    this.progressSection.style.display = "block";
    this.progressBar.style.width = "15%";
    this.percentText.innerText = "15%";
    this.statusMsg.innerText = "Đang gửi tác vụ đến GPU (RTX 3060)...";

    try {
      const resp = await window.apiClient.submitFlowAnimation(payload);
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
        this.progressBar.style.width = `${percent}%`;
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
        if (attempts > 60) {
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
