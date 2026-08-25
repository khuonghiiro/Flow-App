/**
 * Multi-Angle Output Gallery & Export Manager
 */
class GalleryManager {
  constructor(containerId) {
    this.container = document.getElementById(containerId);
    this.frames = [];
    this.onSelectFrame = () => {};
  }

  setSelectCallback(callback) {
    this.onSelectFrame = callback;
  }

  renderGallery(frames) {
    this.frames = frames || [];
    if (!this.container) return;

    this.container.innerHTML = '';
    if (this.frames.length === 0) {
      this.container.innerHTML = '<div style="color: var(--text-dim); padding: 20px; text-align: center;">Chưa có góc xoay nào được tạo.</div>';
      return;
    }

    this.frames.forEach((frame, idx) => {
      const card = document.createElement('div');
      card.className = `gallery-card ${idx === 0 ? 'active' : ''}`;
      card.id = `gallery-card-${idx}`;

      card.innerHTML = `
        <img class="gallery-thumb" src="${frame.image_base64}" alt="Angle ${frame.azimuth_deg}°" />
        <div class="gallery-label">${Math.round(frame.azimuth_deg)}° (#${idx + 1})</div>
      `;

      card.addEventListener('click', () => {
        this.highlightFrame(idx);
        this.onSelectFrame(idx);
      });

      this.container.appendChild(card);
    });
  }

  highlightFrame(index) {
    const cards = this.container.querySelectorAll('.gallery-card');
    cards.forEach((card, idx) => {
      if (idx === index) {
        card.classList.add('active');
        card.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'center' });
      } else {
        card.classList.remove('active');
      }
    });
  }

  downloadSingleFrame(index) {
    if (!this.frames || !this.frames[index]) return;
    const frame = this.frames[index];
    const link = document.createElement('a');
    link.href = frame.image_base64;
    link.download = `angle_${index + 1}_${Math.round(frame.azimuth_deg)}deg.png`;
    link.click();
  }
}

window.GalleryManager = GalleryManager;
