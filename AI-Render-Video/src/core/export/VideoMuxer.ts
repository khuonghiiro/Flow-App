export class VideoMuxer {
  public static downloadVideoBlob(blob: Blob, sceneId: string): void {
    const ext = blob.type.includes('mp4') ? 'mp4' : 'webm';
    const filename = `${sceneId}_render_${Date.now()}.${ext}`;

    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }
}
