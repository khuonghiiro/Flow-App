import { AssetScanner } from '../core/assets/AssetScanner';

export class CatalogExporter {
  public static exportCatalogForPrompt(): string {
    const catalog = AssetScanner.getCatalog();
    return JSON.stringify(catalog, null, 2);
  }
}
