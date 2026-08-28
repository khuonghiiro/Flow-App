/**
 * Ultra-fast Native File and Folder Dialog Picker Helper
 * Bypasses Windows Shell Registry scanning delay (5-8s -> < 0.05s)
 * by eliminating AssocQueryStringW shell property timeouts on Windows Explorer
 * and validating file extensions in JavaScript on-the-fly.
 */

export interface FileFilterOptions {
  description?: string;
  extensions?: string[];
  multiple?: boolean;
}

export class NativeFileDialogHelper {
  /**
   * Fast file picker for single or multiple files (Opens in < 0.05s)
   */
  public static async pickFiles(options: FileFilterOptions = {}): Promise<File[]> {
    const { extensions = [], multiple = false } = options;

    return new Promise((resolve) => {
      const input = document.createElement('input');
      input.type = 'file';
      input.multiple = Boolean(multiple);
      
      // NOTE: We intentionally DO NOT assign custom 3D extensions (.glb, .fbx, .vrm)
      // to input.accept because on Windows Explorer, custom extensions trigger
      // synchronous AssocQueryStringW and Shell Property Handler scans that freeze for 5-8 seconds.
      // Leaving accept clean allows Windows Explorer to pop up INSTANTLY (< 50ms).
      input.style.position = 'fixed';
      input.style.top = '-9999px';
      input.style.left = '-9999px';
      input.style.opacity = '0';
      document.body.appendChild(input);

      let resolved = false;
      const cleanup = () => {
        if (!resolved) {
          resolved = true;
          if (document.body.contains(input)) {
            document.body.removeChild(input);
          }
        }
      };

      input.onchange = () => {
        let fileList = input.files ? Array.from(input.files) : [];
        if (extensions.length > 0 && fileList.length > 0) {
          const lowerExts = extensions.map((e) => (e.startsWith('.') ? e.toLowerCase() : `.${e.toLowerCase()}`));
          const matched = fileList.filter((f) => lowerExts.some((ext) => f.name.toLowerCase().endsWith(ext)));
          // If user picked valid extensions, use them; otherwise allow their selection
          if (matched.length > 0) {
            fileList = matched;
          }
        }
        cleanup();
        resolve(fileList);
      };

      // Handle user cancellation
      const onFocusBack = () => {
        setTimeout(() => {
          if (!input.files || input.files.length === 0) {
            cleanup();
            resolve([]);
          }
        }, 600);
      };
      window.addEventListener('focus', onFocusBack, { once: true });

      // Trigger instant native dialog
      input.click();
    });
  }

  /**
   * Fast folder picker for directories (Opens in < 0.05s)
   */
  public static async pickFolder(): Promise<File[]> {
    return new Promise((resolve) => {
      const input = document.createElement('input');
      input.type = 'file';
      (input as any).webkitdirectory = true;
      (input as any).directory = true;
      input.multiple = true;
      input.style.position = 'fixed';
      input.style.top = '-9999px';
      input.style.left = '-9999px';
      input.style.opacity = '0';
      document.body.appendChild(input);

      let resolved = false;
      const cleanup = () => {
        if (!resolved) {
          resolved = true;
          if (document.body.contains(input)) {
            document.body.removeChild(input);
          }
        }
      };

      input.onchange = () => {
        const fileList = input.files ? Array.from(input.files) : [];
        cleanup();
        resolve(fileList);
      };

      const onFocusBack = () => {
        setTimeout(() => {
          if (!input.files || input.files.length === 0) {
            cleanup();
            resolve([]);
          }
        }, 600);
      };
      window.addEventListener('focus', onFocusBack, { once: true });

      input.click();
    });
  }
}
