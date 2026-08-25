/**
 * API Client for AI Image 360 Rotate Server (Port 3978)
 */
class ApiClient {
  constructor(baseUrl = '') {
    this.baseUrl = baseUrl || window.location.origin;
  }

  async getSystemStatus() {
    try {
      const res = await fetch(`${this.baseUrl}/api/system/status`);
      if (!res.ok) throw new Error(`HTTP error ${res.status}`);
      return await res.json();
    } catch (err) {
      console.error('[API] getSystemStatus failed:', err);
      throw err;
    }
  }

  async getModelStatus() {
    try {
      const res = await fetch(`${this.baseUrl}/api/system/model-status`);
      if (!res.ok) throw new Error(`HTTP error ${res.status}`);
      return await res.json();
    } catch (err) {
      console.error('[API] getModelStatus failed:', err);
      throw err;
    }
  }

  async loadModel(modelId) {
    try {
      const res = await fetch(`${this.baseUrl}/api/system/load-model`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ model_id: modelId })
      });
      if (!res.ok) throw new Error(`HTTP error ${res.status}`);
      return await res.json();
    } catch (err) {
      console.error('[API] loadModel failed:', err);
      throw err;
    }
  }

  async unloadModel() {
    try {
      const res = await fetch(`${this.baseUrl}/api/system/unload-model`, { method: 'POST' });
      if (!res.ok) throw new Error(`HTTP error ${res.status}`);
      return await res.json();
    } catch (err) {
      console.error('[API] unloadModel failed:', err);
      throw err;
    }
  }

  async rotateSingle(payload) {
    try {
      const res = await fetch(`${this.baseUrl}/api/rotate/single`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });
      if (!res.ok) {
        const errorData = await res.json().catch(() => ({}));
        throw new Error(errorData.detail || `Server error ${res.status}`);
      }
      return await res.json();
    } catch (err) {
      console.error('[API] rotateSingle failed:', err);
      throw err;
    }
  }

  async rotateTurntable(payload) {
    try {
      const res = await fetch(`${this.baseUrl}/api/rotate/turntable`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });
      if (!res.ok) {
        const errorData = await res.json().catch(() => ({}));
        throw new Error(errorData.detail || `Server error ${res.status}`);
      }
      return await res.json();
    } catch (err) {
      console.error('[API] rotateTurntable failed:', err);
      throw err;
    }
  }

  async removeBackground(imageBase64, targetSize = 512) {
    try {
      const res = await fetch(`${this.baseUrl}/api/preprocess/remove-bg`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ image_base64: imageBase64, target_size: targetSize })
      });
      if (!res.ok) throw new Error(`HTTP error ${res.status}`);
      return await res.json();
    } catch (err) {
      console.error('[API] removeBackground failed:', err);
      throw err;
    }
  }

  async cleanVram() {
    try {
      const res = await fetch(`${this.baseUrl}/api/system/clean-vram`, { method: 'POST' });
      return await res.json();
    } catch (err) {
      console.error('[API] cleanVram failed:', err);
      throw err;
    }
  }
}

window.apiClient = new ApiClient();
