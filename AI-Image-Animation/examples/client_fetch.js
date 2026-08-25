/**
 * AI Image Animation - JavaScript Fetch Integration Example
 * Can be embedded in ANY frontend application
 */

const SERVER_BASE = "http://localhost:3979";

async function animateCharacterImage(base64Image, options = {}) {
  // 1. Submit animation task to Port 3979
  const payload = {
    image: base64Image,
    mask: options.mask || null, // Grayscale mask for hair / clothing
    vectors: options.vectors || [
      { start_x: 0.35, start_y: 0.3, end_x: 0.65, end_y: 0.28, strength: 1.2 }
    ],
    pins: options.pins || [
      { x: 0.5, y: 0.45, radius: 0.12, weight: 1.0 } // Freeze face
    ],
    wind_strength: options.windStrength || 1.2,
    wave_frequency: options.waveFrequency || 1.8,
    turbulence: options.turbulence || 0.6,
    duration_seconds: options.duration || 3.0,
    fps: 30,
    format: options.format || "mp4",
    loop_mode: "seamless_phase"
  };

  console.log("Submitting animation request...");
  const response = await fetch(`${SERVER_BASE}/api/animate/flow`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });

  if (!response.ok) {
    throw new Error(`Animation request failed: ${response.statusText}`);
  }

  const { task_id } = await response.json();
  console.log(`Task created with ID: ${task_id}`);

  // 2. Poll task status until complete
  return new Promise((resolve, reject) => {
    const checkInterval = setInterval(async () => {
      try {
        const statusRes = await fetch(`${SERVER_BASE}/api/tasks/${task_id}`);
        const task = await statusRes.json();
        console.log(`Progress: ${(task.progress * 100).toFixed(0)}% - ${task.message}`);

        if (task.status === "completed") {
          clearInterval(checkInterval);
          resolve({
            videoUrl: `${SERVER_BASE}${task.result_url}`,
            gifUrl: task.gif_url ? `${SERVER_BASE}${task.gif_url}` : null,
            duration: task.duration_seconds
          });
        } else if (task.status === "failed") {
          clearInterval(checkInterval);
          reject(new Error(task.error || "Rendering failed"));
        }
      } catch (err) {
        clearInterval(checkInterval);
        reject(err);
      }
    }, 500);
  });
}

// Export for module systems or browser window
if (typeof module !== "undefined") {
  module.exports = { animateCharacterImage };
}
