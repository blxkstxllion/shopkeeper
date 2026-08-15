// One-off icon generator: rasterizes the brand mark into the square PNGs a PWA
// manifest needs. Not part of the build - run manually if the brand mark ever
// changes. `sharp` is a devDependency solely for this script.
//
// Uses only the solid outer silhouette path from public/favicon.svg, not the full
// file - the full file's blur filters/mask don't survive librsvg's rasterizer
// (renders as stray black clipping bars), but the silhouette alone is the
// recognizable logo shape and rasterizes cleanly.
import sharp from 'sharp'
import { fileURLToPath } from 'node:url'
import path from 'node:path'

const dir = path.dirname(fileURLToPath(import.meta.url))
const bg = '#1a0533' // dark purple from the logo's own palette, not pure black

const silhouette = `<svg xmlns="http://www.w3.org/2000/svg" width="48" height="46" viewBox="0 0 48 46">
  <path fill="#a855f7" d="M25.946 44.938c-.664.845-2.021.375-2.021-.698V33.937a2.26 2.26 0 0 0-2.262-2.262H10.287c-.92 0-1.456-1.04-.92-1.788l7.48-10.471c1.07-1.497 0-3.578-1.842-3.578H1.237c-.92 0-1.456-1.04-.92-1.788L10.013.474c.214-.297.556-.474.92-.474h28.894c.92 0 1.456 1.04.92 1.788l-7.48 10.471c-1.07 1.498 0 3.579 1.842 3.579h11.377c.943 0 1.473 1.088.89 1.83L25.947 44.94z"/>
</svg>`

async function makeIcon(size, outName, { padding = 0 } = {}) {
  const inner = size - padding * 2
  const mark = await sharp(Buffer.from(silhouette))
    .resize(inner, inner, { fit: 'contain', background: { r: 0, g: 0, b: 0, alpha: 0 } })
    .toBuffer()
  await sharp({
    create: { width: size, height: size, channels: 4, background: bg },
  })
    .composite([{ input: mark, top: padding, left: padding }])
    .png()
    .toFile(path.join(dir, '../public', outName))
  console.log('wrote', outName)
}

await makeIcon(192, 'pwa-192x192.png', { padding: 24 })
await makeIcon(512, 'pwa-512x512.png', { padding: 64 })
// Maskable needs generous safe-zone padding (icon content within the center ~80%).
await makeIcon(512, 'pwa-maskable-512x512.png', { padding: 96 })
await makeIcon(180, 'apple-touch-icon.png', { padding: 20 })
