import sharp from 'sharp'
import path from 'path'

const SRC = 'C:\\Users\\YRG\\Desktop\\shopkeeper\\shopkeeper-icon.png.png'
const OUT = path.resolve(import.meta.dirname, '..', 'public')

async function main() {
  const meta = await sharp(SRC).metadata()
  console.log('source:', meta.width, meta.height, meta.format)

  // Sample from the top-edge midpoint (inside the green fill, clear of both the white margin
  // and the rounded-corner curve) to get the real background green for the maskable safe-fill.
  const midTop = Math.round(meta.width / 2)
  const sampleY = Math.round(meta.height * 0.03)
  const corner = await sharp(SRC).extract({ left: midTop, top: sampleY, width: 1, height: 1 }).raw().toBuffer()
  const [r, g, b] = corner
  console.log('sampled color:', r, g, b)

  // Standard PWA icons - direct resize, the source is already a clean rounded-square icon.
  await sharp(SRC).resize(192, 192).png().toFile(path.join(OUT, 'pwa-192x192.png'))
  await sharp(SRC).resize(512, 512).png().toFile(path.join(OUT, 'pwa-512x512.png'))
  await sharp(SRC).resize(180, 180).png().toFile(path.join(OUT, 'apple-touch-icon.png'))
  await sharp(SRC).resize(48, 48).png().toFile(path.join(OUT, 'favicon.png'))

  // Maskable: OS applies its own mask (circle/squircle/rounded-square) to this image, so it
  // must be full-bleed edge-to-edge color with the logo inset into the center ~80% safe zone -
  // NOT the as-is icon (which already has its own rounded corners + white margin baked in,
  // which would show as jagged white corners peeking out from under the OS mask).
  const bg = { r, g, b, alpha: 1 }
  const inset = Math.round(512 * 0.76) // logo occupies most of the canvas, safely inside the 80% safe zone
  // Auto-trim barely moved (soft/anti-aliased edge, not solid white), so crop the source's
  // own margin manually by percentage instead - determined visually against the source render.
  const marginPct = 0.1
  const cropSize = Math.round(meta.width * (1 - 2 * marginPct))
  const cropOffset = Math.round(meta.width * marginPct)
  const cropped = await sharp(SRC)
    .extract({ left: cropOffset, top: cropOffset, width: cropSize, height: cropSize })
    .toBuffer()
  const logo = await sharp(cropped).resize(inset, inset).toBuffer()
  await sharp({ create: { width: 512, height: 512, channels: 4, background: bg } })
    .composite([{ input: logo, gravity: 'center' }])
    .png()
    .toFile(path.join(OUT, 'pwa-maskable-512x512.png'))

  // Small in-app UI chrome (sidebar/login header, ~32-44px) - the full icon's wordmark and
  // tagline are illegible at that size and redundant next to "The Shop Keeper" text already
  // rendered beside it, so crop to just the bag+chart glyph on its gradient background.
  await sharp(SRC)
    .extract({ left: 197, top: 50, width: 860, height: 860 })
    .resize(256, 256)
    .png()
    .toFile(path.join(OUT, 'logo-mark.png'))

  console.log('done')
}

main().catch((err) => {
  console.error(err)
  process.exit(1)
})
