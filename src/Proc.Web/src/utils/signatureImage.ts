/**
 * Xử lý ảnh chữ ký / logo ngay trên trình duyệt trước khi tải lên (lát IN1 – design.md mục 20.4): không cần thư viện ảnh trả phí, máy chủ chỉ kiểm
 * định dạng, cỡ. Chữ ký chụp / scan trên giấy trắng được:
 * 1. Xoay (nếu chụp ngang), thu về tối đa 1600 điểm cạnh dài cho nhanh.
 * 2. Tách nền: độ sáng nền = phân vị 90 % độ sáng các điểm ảnh (giấy hơi xám, chụp tối vẫn đúng); điểm tối hơn ngưỡng (nền − độ lệch) là nét mực,
 *    độ trong suốt tăng dần theo độ tối (mép nét mềm, không răng cưa). Độ lệch theo mức "Tách nền" 1–10: mức cao giữ cả nét mờ.
 *    Ví dụ nền 235, mức 6 → độ lệch 40 → ngưỡng 195; điểm sáng 175 có alpha = (195 − 175) / 40 × 1,6 = 0,8.
 * 3. Làm đậm nét mờ (alpha × 1,6), đổi màu mực (xanh bút bi / đen / giữ màu gốc).
 * 4. Cắt sát nét chữ (chừa 6 điểm), thu về tối đa 600 × 240 điểm – in ô ký cao 15 mm vẫn nét (≈ 400 dpi).
 */

/** Màu mực sau khi tách nền. */
export type InkColor = 'blue' | 'black' | 'original'

/** Tùy chọn xử lý chữ ký. */
export interface SignatureOptions {
  ink: InkColor
  /** Mức tách nền 1–10 (mặc định 6). */
  sensitivity: number
  /** Góc xoay theo chiều kim đồng hồ. */
  rotate: 0 | 90 | 180 | 270
}

/** Màu mực xanh mặc định – như bút bi ký trên giấy (người dùng đồng ý 18/11/2026). */
const INK: Record<Exclude<InkColor, 'original'>, [number, number, number]> = {
  blue: [0x1a, 0x3a, 0x9c],
  black: [0x14, 0x14, 0x14],
}

/**
 * Đọc file ảnh thành HTMLImageElement.
 * @param file File PNG/JPG người dùng chọn.
 * @returns Ảnh đã tải xong.
 * @throws Error khi file không phải ảnh trình duyệt đọc được.
 */
export function loadImage(file: File): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const url = URL.createObjectURL(file)
    const image = new Image()
    image.onload = () => {
      URL.revokeObjectURL(url)
      resolve(image)
    }
    image.onerror = () => {
      URL.revokeObjectURL(url)
      reject(new Error('Không đọc được ảnh – hãy chọn file PNG hoặc JPG.'))
    }
    image.src = url
  })
}

/** Vẽ ảnh đã xoay, cạnh dài tối đa maxSide, vào canvas mới. */
function drawRotated(image: HTMLImageElement, rotate: number, maxSide: number): HTMLCanvasElement {
  const scale = Math.min(1, maxSide / Math.max(image.naturalWidth, image.naturalHeight))
  const w = Math.round(image.naturalWidth * scale)
  const h = Math.round(image.naturalHeight * scale)
  const swap = rotate === 90 || rotate === 270
  const canvas = document.createElement('canvas')
  canvas.width = swap ? h : w
  canvas.height = swap ? w : h
  const ctx = canvas.getContext('2d')!
  ctx.translate(canvas.width / 2, canvas.height / 2)
  ctx.rotate((rotate * Math.PI) / 180)
  ctx.drawImage(image, -w / 2, -h / 2, w, h)
  return canvas
}

/** Thu canvas về khung tối đa maxW × maxH (không phóng to). */
function fit(source: HTMLCanvasElement, sx: number, sy: number, sw: number, sh: number, maxW: number, maxH: number): HTMLCanvasElement {
  const scale = Math.min(1, maxW / sw, maxH / sh)
  const canvas = document.createElement('canvas')
  canvas.width = Math.max(1, Math.round(sw * scale))
  canvas.height = Math.max(1, Math.round(sh * scale))
  const ctx = canvas.getContext('2d')!
  ctx.imageSmoothingQuality = 'high'
  ctx.drawImage(source, sx, sy, sw, sh, 0, 0, canvas.width, canvas.height)
  return canvas
}

/**
 * Tách nền, đổi màu mực, cắt sát, thu nhỏ chữ ký.
 * @param image Ảnh gốc.
 * @param options Tùy chọn.
 * @returns Canvas PNG nền trong suốt; null khi không tìm thấy nét chữ nào (ảnh trắng hoặc mức tách nền quá thấp).
 */
export function processSignature(image: HTMLImageElement, options: SignatureOptions): HTMLCanvasElement | null {
  const canvas = drawRotated(image, options.rotate, 1600)
  const ctx = canvas.getContext('2d')!
  const data = ctx.getImageData(0, 0, canvas.width, canvas.height)
  const px = data.data
  const count = canvas.width * canvas.height
  const lum = new Uint8Array(count)
  const histogram = new Uint32Array(256)
  for (let i = 0; i < count; i++) {
    const p = i * 4
    // Điểm ảnh vốn trong suốt (PNG) coi như giấy trắng.
    const l = px[p + 3] < 16 ? 255 : Math.round(0.299 * px[p] + 0.587 * px[p + 1] + 0.114 * px[p + 2])
    lum[i] = l
    histogram[l]++
  }
  let seen = 0
  let background = 255
  for (let l = 0; l < 256; l++) {
    seen += histogram[l]
    if (seen >= count * 0.9) {
      background = l
      break
    }
  }

  const delta = 70 - (Math.min(10, Math.max(1, options.sensitivity)) - 1) * 5.5
  const threshold = background - delta
  const ink = options.ink === 'original' ? null : INK[options.ink]
  let minX = canvas.width
  let minY = canvas.height
  let maxX = -1
  let maxY = -1
  for (let i = 0; i < count; i++) {
    const p = i * 4
    const alpha = lum[i] >= threshold ? 0 : Math.min(1, ((threshold - lum[i]) / delta) * 1.6)
    if (ink) [px[p], px[p + 1], px[p + 2]] = ink
    px[p + 3] = Math.round(alpha * 255)
    if (px[p + 3] > 24) {
      const x = i % canvas.width
      const y = (i - x) / canvas.width
      if (x < minX) minX = x
      if (x > maxX) maxX = x
      if (y < minY) minY = y
      if (y > maxY) maxY = y
    }
  }
  if (maxX < 0) return null
  ctx.putImageData(data, 0, 0)
  const pad = 6
  const sx = Math.max(0, minX - pad)
  const sy = Math.max(0, minY - pad)
  return fit(canvas, sx, sy, Math.min(canvas.width, maxX + pad + 1) - sx, Math.min(canvas.height, maxY + pad + 1) - sy, 600, 240)
}

/**
 * Logo: chỉ thu về tối đa 600 điểm mỗi cạnh, giữ nền (PNG trong suốt giữ nguyên).
 * @param image Ảnh gốc.
 * @returns Canvas.
 */
export function processLogo(image: HTMLImageElement): HTMLCanvasElement {
  const source = drawRotated(image, 0, 4000)
  return fit(source, 0, 0, source.width, source.height, 600, 600)
}

/**
 * Canvas → file PNG để tải lên.
 * @param canvas Canvas đã xử lý.
 * @param name Tên file.
 * @returns File PNG.
 */
export function canvasToPng(canvas: HTMLCanvasElement, name: string): Promise<File> {
  return new Promise((resolve, reject) =>
    canvas.toBlob((blob) => (blob ? resolve(new File([blob], name, { type: 'image/png' })) : reject(new Error('Không tạo được ảnh PNG.'))), 'image/png'),
  )
}
