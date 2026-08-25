/**
 * UI Controller & Event Binding Module
 */
class UIController {
  constructor() {
    this.currentTool = "brush";
    this.currentPreset = "gentle_breeze";
  }

  init(onPresetChange, onPhysicsChange, onToolChange) {
    this.onPresetChange = onPresetChange;
    this.onPhysicsChange = onPhysicsChange;
    this.onToolChange = onToolChange;

    this.bindSliders();
    this.bindToolButtons();
    this.bindApiModal();
  }

  populatePresets(presets, onSelect) {
    const grid = document.getElementById("presetsGrid");
    if (!grid) return;

    grid.innerHTML = "";
    presets.forEach(p => {
      const card = document.createElement("div");
      card.className = `preset-card ${p.id === this.currentPreset ? "active" : ""}`;
      card.dataset.id = p.id;
      card.innerHTML = `
        <div class="preset-icon">${p.icon}</div>
        <div class="preset-name">${p.name.split('(')[0].trim()}</div>
        <div class="preset-desc">${p.description}</div>
      `;
      card.onclick = () => {
        document.querySelectorAll(".preset-card").forEach(c => c.classList.remove("active"));
        card.classList.add("active");
        this.currentPreset = p.id;
        
        // Update sliders with preset values
        this.setSliderValues(p);
        if (onSelect) onSelect(p);
      };
      grid.appendChild(card);
    });
  }

  setSliderValues(params) {
    if (params.wind_strength !== undefined) {
      document.getElementById("sliderWindStrength").value = params.wind_strength;
      document.getElementById("valWindStrength").innerText = `${params.wind_strength}x`;
    }
    if (params.wave_frequency !== undefined) {
      document.getElementById("sliderWaveFreq").value = params.wave_frequency;
      document.getElementById("valWaveFreq").innerText = `${params.wave_frequency} Hz`;
    }
    if (params.turbulence !== undefined) {
      document.getElementById("sliderTurbulence").value = params.turbulence;
      document.getElementById("valTurbulence").innerText = `${params.turbulence}`;
    }
    if (params.flutter_scale !== undefined) {
      document.getElementById("sliderFlutter").value = params.flutter_scale;
      document.getElementById("valFlutter").innerText = `${params.flutter_scale}`;
    }
  }

  bindSliders() {
    const sStrength = document.getElementById("sliderWindStrength");
    const sFreq = document.getElementById("sliderWaveFreq");
    const sTurb = document.getElementById("sliderTurbulence");
    const sFlut = document.getElementById("sliderFlutter");
    const sDur = document.getElementById("sliderDuration");
    const sBrush = document.getElementById("sliderBrushSize");

    const notify = () => {
      if (this.onPhysicsChange) {
        this.onPhysicsChange({
          windStrength: parseFloat(sStrength.value),
          waveFrequency: parseFloat(sFreq.value),
          turbulence: parseFloat(sTurb.value),
          flutterScale: parseFloat(sFlut.value),
          duration: parseFloat(sDur.value)
        });
      }
    };

    sStrength.oninput = (e) => {
      document.getElementById("valWindStrength").innerText = `${e.target.value}x`;
      notify();
    };
    sFreq.oninput = (e) => {
      document.getElementById("valWaveFreq").innerText = `${e.target.value} Hz`;
      notify();
    };
    sTurb.oninput = (e) => {
      document.getElementById("valTurbulence").innerText = `${e.target.value}`;
      notify();
    };
    sFlut.oninput = (e) => {
      document.getElementById("valFlutter").innerText = `${e.target.value}`;
      notify();
    };
    sDur.oninput = (e) => {
      document.getElementById("valDuration").innerText = `${e.target.value} s`;
      notify();
    };
    sBrush.oninput = (e) => {
      document.getElementById("valBrushSize").innerText = `${e.target.value} px`;
      if (window.maskPainter) {
        window.maskPainter.brushSize = parseInt(e.target.value);
      }
    };
  }

  bindToolButtons() {
    const tools = ["toolBrush", "toolVector", "toolPin", "toolEraser"];
    tools.forEach(id => {
      const btn = document.getElementById(id);
      if (!btn) return;
      btn.onclick = () => {
        tools.forEach(t => {
          const el = document.getElementById(t);
          if (el) el.classList.remove("active");
        });
        btn.classList.add("active");
        this.currentTool = btn.dataset.tool;
        if (this.onToolChange) this.onToolChange(this.currentTool);
      };
    });
  }

  bindApiModal() {
    const btnOpen = document.getElementById("btnOpenApiModal");
    const btnClose = document.getElementById("btnCloseApiModal");
    const modal = document.getElementById("apiModal");
    const codeBlock = document.getElementById("apiCodeBlock");
    const btnCopy = document.getElementById("btnCopyCode");

    if (btnOpen && modal) {
      btnOpen.onclick = () => {
        this.updateApiCode("js");
        modal.classList.add("show");
      };
    }
    if (btnClose && modal) {
      btnClose.onclick = () => modal.classList.remove("show");
    }

    // Code snippet language tabs
    document.querySelectorAll(".api-tab-btn").forEach(tab => {
      tab.onclick = () => {
        document.querySelectorAll(".api-tab-btn").forEach(t => t.classList.remove("active"));
        tab.classList.add("active");
        this.updateApiCode(tab.dataset.lang);
      };
    });

    if (btnCopy && codeBlock) {
      btnCopy.onclick = () => {
        navigator.clipboard.writeText(codeBlock.innerText);
        btnCopy.innerText = "Copied!";
        setTimeout(() => (btnCopy.innerText = "Copy Code"), 2000);
      };
    }
  }

  updateApiCode(lang) {
    const codeBlock = document.getElementById("apiCodeBlock");
    if (!codeBlock) return;

    if (lang === "js") {
      codeBlock.innerText = `// JavaScript Fetch Example (Port 3979)
const response = await fetch("http://localhost:3979/api/animate/flow", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({
    image: "data:image/png;base64,...",
    mask: "data:image/png;base64,...",
    vectors: [
      { start_x: 0.3, start_y: 0.4, end_x: 0.6, end_y: 0.38, strength: 1.2 }
    ],
    pins: [{ x: 0.5, y: 0.5, radius: 0.08, weight: 1.0 }],
    wind_strength: 1.2,
    wave_frequency: 1.8,
    turbulence: 0.6,
    duration_seconds: 3.0,
    fps: 30,
    format: "mp4",
    loop_mode: "seamless_phase"
  })
});

const data = await response.json();
console.log("Task ID:", data.task_id);
// Query: GET http://localhost:3979/api/tasks/\${data.task_id}`;
    } else if (lang === "python") {
      codeBlock.innerText = `# Python Requests Example (Port 3979)
import requests

payload = {
    "image": "data:image/png;base64,...",
    "mask": "data:image/png;base64,...",
    "vectors": [
        {"start_x": 0.3, "start_y": 0.4, "end_x": 0.6, "end_y": 0.38, "strength": 1.2}
    ],
    "pins": [{"x": 0.5, "y": 0.5, "radius": 0.08, "weight": 1.0}],
    "wind_strength": 1.2,
    "wave_frequency": 1.8,
    "turbulence": 0.6,
    "duration_seconds": 3.0,
    "fps": 30,
    "format": "mp4",
    "loop_mode": "seamless_phase"
}

res = requests.post("http://localhost:3979/api/animate/flow", json=payload)
data = res.json()
print("Task Created:", data["task_id"])`;
    } else if (lang === "curl") {
      codeBlock.innerText = `# cURL Terminal Example (Port 3979)
curl -X POST "http://localhost:3979/api/animate/flow" \\
  -H "Content-Type: application/json" \\
  -d '{
    "image": "data:image/png;base64,...",
    "wind_strength": 1.2,
    "wave_frequency": 1.8,
    "turbulence": 0.6,
    "duration_seconds": 3.0,
    "format": "mp4"
  }'`;
    }
  }
}

window.UIController = UIController;
