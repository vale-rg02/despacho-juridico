/**
 * generate-favicons.mjs
 *
 * Genera todos los formatos de favicon necesarios (PNG en varios tamaños,
 * favicon.ico multi-resolución, apple-touch-icon e íconos de manifest PWA)
 * a partir de un único SVG.
 *
 * Uso:
 *   node generate-favicons.mjs
 *
 * Input esperado:  public/favicon.svg
 * Output generado:
 *   public/favicon-16x16.png
 *   public/favicon-32x32.png
 *   public/favicon-48x48.png
 *   public/apple-touch-icon.png   (180x180)
 *   public/icon-192x192.png       (manifest PWA)
 *   public/icon-512x512.png       (manifest PWA)
 *   public/favicon.ico            (multi-size: 16, 32, 48)
 */

import sharp from "sharp";
import pngToIco from "png-to-ico";
import { readFile, writeFile, mkdir } from "fs/promises";
import { existsSync } from "fs";
import path from "path";

const PUBLIC_DIR = path.resolve("public");
const SVG_PATH = path.join(PUBLIC_DIR, "favicon.svg");

// Tamaños que se generan como PNG individuales
const PNG_SIZES = [16, 32, 48];
const APPLE_TOUCH_SIZE = 180;
const MANIFEST_SIZES = [192, 512];

async function ensureSvgExists() {
  if (!existsSync(SVG_PATH)) {
    throw new Error(
      `No se encontró el archivo SVG en: ${SVG_PATH}\n` +
      `Asegúrate de tener tu favicon.svg dentro de la carpeta "public/".`
    );
  }
}

async function generatePng(size, outputName) {
  const svgBuffer = await readFile(SVG_PATH);
  const outputPath = path.join(PUBLIC_DIR, outputName);

  await sharp(svgBuffer, { density: 300 }) // density alta = mejor calidad al rasterizar
    .resize(size, size)
    .png()
    .toFile(outputPath);

  console.log(`✅ Generado: ${outputName} (${size}x${size})`);
  return outputPath;
}

async function generateIco(pngPaths, outputName = "favicon.ico") {
  const icoBuffer = await pngToIco(pngPaths);
  const outputPath = path.join(PUBLIC_DIR, outputName);
  await writeFile(outputPath, icoBuffer);
  console.log(`✅ Generado: ${outputName} (multi-size: ${PNG_SIZES.join(", ")}px)`);
}

async function main() {
  console.log("🎨 Generando favicons desde SVG...\n");

  await ensureSvgExists();
  await mkdir(PUBLIC_DIR, { recursive: true });

  // 1. Generar PNGs individuales para <link> tags
  const pngPathsForIco = [];
  for (const size of PNG_SIZES) {
    const fileName = `favicon-${size}x${size}.png`;
    const outputPath = await generatePng(size, fileName);
    pngPathsForIco.push(outputPath);
  }

  // 2. Generar apple-touch-icon (para iOS / bookmarks)
  await generatePng(APPLE_TOUCH_SIZE, "apple-touch-icon.png");

  // 3. Generar íconos para el manifest PWA (usados al "Instalar app" y como
  //    ícono de escritorio/taskbar si se instala como app)
  for (const size of MANIFEST_SIZES) {
    await generatePng(size, `icon-${size}x${size}.png`);
  }

  // 4. Combinar PNGs en un solo favicon.ico multi-resolución
  //    (el que usa Windows para el ícono de accesos directos de escritorio)
  await generateIco(pngPathsForIco);

  console.log("\n✨ Listo. Archivos generados en /public.");
}

main().catch((err) => {
  console.error("❌ Error generando favicons:\n", err.message);
  process.exit(1);
});
