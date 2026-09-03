/**
 * API Client for AI Image Animation Server (Port 3979)
 */
class ApiClient {
  constructor(baseUrl = "") {
    this.baseUrl = baseUrl || window.location.origin;
  }

  async getHealth() {
    try {
      const res = await fetch(`${this.baseUrl}/api/health`);
      if (!res.ok) throw new Error("Health check failed");
      return await res.json();
    } catch (e) {
      console.warn("Could not reach API server:", e);
      return null;
    }
  }

  async getPresets() {
    try {
      const res = await fetch(`${this.baseUrl}/api/presets`);
      return await res.json();
    } catch (e) {
      console.error("Failed to fetch presets:", e);
      return [];
    }
  }

  async computePreviewFrame(payload) {
    try {
      const res = await fetch(`${this.baseUrl}/api/preview/frame`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
      });
      if (!res.ok) throw new Error("Preview frame failed");
      return await res.json();
    } catch (e) {
      console.error("Preview frame error:", e);
      return null;
    }
  }

  async submitFlowAnimation(payload) {
    const res = await fetch(`${this.baseUrl}/api/animate/flow`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    if (!res.ok) {
      const errorData = await res.json().catch(() => ({}));
      throw new Error(errorData.detail || "Animation rendering request failed");
    }
    return await res.json();
  }

  async submitDiffusionAnimation(payload) {
    const res = await fetch(`${this.baseUrl}/api/animate/diffusion`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    if (!res.ok) {
      const errorData = await res.json().catch(() => ({}));
      throw new Error(errorData.detail || "Diffusion rendering request failed");
    }
    return await res.json();
  }

  async getTaskStatus(taskId) {
    const res = await fetch(`${this.baseUrl}/api/tasks/${taskId}`);
    if (!res.ok) throw new Error("Task query failed");
    return await res.json();
  }

  async getModelStatus() {
    try {
      const res = await fetch(`${this.baseUrl}/api/model/status`);
      return await res.json();
    } catch (e) {
      console.warn("Failed to get model status:", e);
      return null;
    }
  }

  async loadModel(modelType = "animatediff") {
    const res = await fetch(`${this.baseUrl}/api/model/load?model_type=${encodeURIComponent(modelType)}`, { method: "POST" });
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      throw new Error(err.detail || err.message || "Failed to load model into VRAM");
    }
    return await res.json();
  }

  async unloadModel() {
    const res = await fetch(`${this.baseUrl}/api/model/unload`, { method: "POST" });
    if (!res.ok) throw new Error("Failed to unload model from VRAM");
    return await res.json();
  }

  connectTaskWebSocket(taskId, onMessage, onError) {
    const protocol = window.location.protocol === "https:" ? "wss:" : "ws:";
    const wsUrl = `${protocol}//${window.location.host}/ws/progress/${taskId}`;
    const ws = new WebSocket(wsUrl);

    ws.onmessage = (event) => {
      try {
        const data = JSON.parse(event.data);
        if (onMessage) onMessage(data);
      } catch (err) {
        console.error("WebSocket parse error:", err);
      }
    };

    ws.onerror = (err) => {
      if (onError) onError(err);
    };

    return ws;
  }
}

window.apiClient = new ApiClient();
